using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using PlanetGen.Core;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using PlanetGen.FieldGen2.Graph.Nodes.IO;
using PlanetGen.FieldGen2.Graph.Nodes.Outputs;
using PlanetGen.FieldGen2.Graph.Types;
using PlanetGen.FieldGen2.Types;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.noise; // Use snoise directly
using Random = Unity.Mathematics.Random;
using UnityEngine.Profiling; // Added for profiling

namespace PlanetGen.FieldGen2
{
    [Serializable]
    public class GraphLayer
    {
        [Required] public GeneratorGraph graph;

        [Range(0f, 1f)] [Tooltip("Base weight for this layer's influence")]
        public float baseWeight = 1f;

        [Range(1f, 1000f)] [Tooltip("Random seed for this layer's weight distribution")]
        public float weightSeed = 0f; // Used for unique noise offset for this layer

        [NonSerialized] public NativeArray<float> weightMask;
        public float seed;
        public bool IsValid => graph != null;

        /// <summary>
        /// Validate this graph layer configuration
        /// </summary>
        public ValidationResult Validate()
        {
            return ParameterValidator.Create().ValidateNotNull(graph, nameof(graph))
                .ValidateRange(baseWeight, 0f, 1f, nameof(baseWeight))
                .ValidatePositive(weightSeed, nameof(weightSeed))
                .WarnIf(baseWeight < 0.1f, "Very low base weight may lead to negligible influence from this layer")
                .WarnIf(baseWeight > 0.9f, "Very high base weight may dominate other layers")
                .Build();
        }

        /// <summary>
        /// Validate that this layer's graph has the required output nodes
        /// </summary>
        public ValidationResult ValidateGraphStructure()
        {
            if (graph == null)
                return new ValidationResult(new[] { "Graph is null" });
            var validator = ParameterValidator.Create();

            var vectorOutputs = graph.nodes.OfType<VectorOutputNode>().ToList();
            var rasterOutputs = graph.nodes.OfType<RasterOutputNode>().ToList();

            if (vectorOutputs.Count != 1 || rasterOutputs.Count != 1)
            {
                validator.ValidateCustom(
                    false,
                    $"Graph must have exactly one VectorOutputNode and one RasterOutputNode, found {vectorOutputs.Count} vector and {rasterOutputs.Count} raster outputs."
                );
            }

            foreach (var node in graph.nodes)
            {
                if (node == null)
                {
                    validator.ValidateCustom(false, "Graph contains a null node");
                    continue;
                }
            }

            return validator.Build();
        }
    }

    public class FieldGenMain : MonoBehaviour
    {
        #region Events

        public event Action OnVectorDataGenerated;
        public event Action OnPlanetDataGenerated;
        public event Action<FieldData> OnDataReady;

        #endregion

        #region Inspector parameters

        [TriggerFieldRegen] [TriggerMaskRegen]
        public int fieldRes = 512;
        
        [Header("Sequential Vector Processing")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        [TriggerMaskRegen]
        public List<GraphLayer> graphLayers = new();

        [Header("Initial Circle")] [TriggerFieldRegen]
        public int circleVertexCount = 64;

        [TriggerFieldRegen] public float circleRadius = 0.5f;

        [Header("Dominance Mask Generation")] 



        [Range(0.1f, 10f)] [Tooltip("Frequency of the noise patterns for dominance.")] [TriggerMaskRegen]
        public float dominanceNoiseFrequency = 2f;

        [Range(1f, 100f)]
        [Tooltip("How sharply influence drops off for non-dominant graphs. Higher = more distinct regions.")]
        [TriggerMaskRegen]
        public float dominanceSharpness = 20f;

        [TriggerMaskRegen] public uint dominanceMasterSeed = 123;

        // [Header("Raster")] public int textureSize = 512;
        [Header("Debug")] public bool drawDebugLines = true;
        public Color debugColor = Color.red;
        public float debugScale = 10f;
        public bool continuousDebugDraw = true;

        [Button("Force Evaluation", ButtonSizes.Large)]
        // public void ForceEvaluation() => ProcessSequentialGraphs();
        public void ForceEvaluation() => QueueRegeneration(Because.ManualRegeneration);


        [Button("Regenerate Weights", ButtonSizes.Medium)]
        public void RegenerateWeights()
        {
            maskRegenQueued = true;
            QueueRegeneration(Because.MaskRegeneration);
        }

        #endregion

        #region Private members

        private ParameterWatcher paramWatcher;
        private VectorData currentVectorResult;

        // private RasterData currentRasterResult;
        private bool hasVectorResult = false;

        private bool hasRasterResult = false;

        // private bool regenQueued = false;
        private bool maskRegenQueued = false;

        // Previous values to detect changes in OnValidate for mask regeneration
        private int _prevTextureSize;
        private float _prevDominanceNoiseFrequency;
        private float _prevDominanceSharpness;
        private uint _prevDominanceMasterSeed;
        private List<GraphLayer> _prevGraphLayersSnapshot;

        private RasterData currentRaster;
        private bool hasRasterData = false;
        private JobHandle rasterizeJobHandle;

        #endregion

        #region Public properties

        public bool HasVectorVectorData => hasVectorResult;
        public bool HasRasterData => hasRasterData;
        public int CurrentRasterSize => fieldRes;

        private FieldData FieldData;

        #endregion

        #region Update Queue

        public enum Because
        {
            NodeParameterChanged,
            ValidationTriggered,
            ManualRegeneration,
            MaskRegeneration,
            ExternalProcess
        }

        private struct UpdateRequest
        {
            public Because reason;
            public string caller;
            public string sourceFile;
            public int sourceLine;
        }

        private UpdateRequest? queuedUpdate;

        void QueueRegeneration(Because reason,
            [CallerMemberName] string caller = "",
            [CallerFilePath] string sourceFile = "",
            [CallerLineNumber] int sourceLine = 0)
        {
            queuedUpdate = new UpdateRequest
            {
                reason = reason,
                caller = caller,
                sourceFile = Path.GetFileName(sourceFile),
                sourceLine = sourceLine
            };
        }

        #endregion

        #region Unity Lifecycle

        void Start()
        {
            BaseNode.OnAnyNodeChanged += OnNodeParameterChanged;
            paramWatcher = new ParameterWatcher(this);
            maskRegenQueued = true;
        }

        private void OnValidate()
        {
            if (dominanceNoiseFrequency <= 0) dominanceNoiseFrequency = 0.1f;
            if (dominanceSharpness <= 0) dominanceSharpness = 1f;
            maskRegenQueued = true;
            QueueRegeneration(Because
                .ValidationTriggered); //TODO: implement change detection that supports the nested class
        }

        void Update()
        {
            if (maskRegenQueued)
            {
                var maskResult = RegenerateWeightMasks();
                if (!maskResult.IsSuccess)
                {
                    ErrorHandler.LogError("FieldGenMain.Update",
                        $"Weight mask regeneration failed: {maskResult.ErrorMessage}");
                    return;
                }

                QueueRegeneration(Because.MaskRegeneration);
            }

            if (queuedUpdate.HasValue)
            {
                var update = queuedUpdate.Value;
                Debug.Log(
                    $"Processing graphs due to: {update.reason} (called by {update.caller} in {update.sourceFile}:{update.sourceLine})");
                queuedUpdate = null;

                var processResult = ProcessSequentialGraphs();
                if (!processResult.IsSuccess)
                {
                    ErrorHandler.LogError("FieldGenMain.Update",
                        $"Graph processing failed: {processResult.ErrorMessage}");
                }
            }

            if (continuousDebugDraw && drawDebugLines && hasVectorResult && currentVectorResult.IsValid &&
                currentVectorResult.Count > 1)
            {
                DebugDrawVectorData(currentVectorResult, debugColor, Time.deltaTime * 1.1f, Vector3.zero, debugScale);
            }
        }

        void OnDestroy()
        {
            BaseNode.OnAnyNodeChanged -= OnNodeParameterChanged;

            // Complete any pending jobs
            if (rasterizeJobHandle.IsCompleted == false)
            {
                rasterizeJobHandle.Complete();
            }

            // SAFE DISPOSAL - Check validity before disposing
            if (hasVectorResult && currentVectorResult.IsValid)
            {
                try
                {
                    currentVectorResult.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, that's fine
                }
            }

            if (hasRasterData && currentRaster.IsValid)
            {
                try
                {
                    currentRaster.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, that's fine
                }
            }

            // Dispose weight masks
            foreach (var layer in graphLayers)
            {
                if (layer.weightMask.IsCreated)
                {
                    try
                    {
                        layer.weightMask.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed, that's fine
                    }
                }
            }

            // Dispose FieldData last
            if (FieldData != null)
            {
                try
                {
                    FieldData.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, that's fine
                }
                FieldData = null;
            }
        }

        #endregion

        #region API

        private void OnNodeParameterChanged()
        {
            QueueRegeneration(Because.NodeParameterChanged);
        }

        public void ExternalProcess(float seed)
        {
            // TODO: implement seed
            QueueRegeneration(Because.ExternalProcess);
        }

        #endregion

        #region Validation

        /// <summary>
        /// Comprehensive validation of the entire FieldGen configuration
        /// </summary>
        public ValidationResult ValidateConfiguration()
        {
            var result = new ValidationResult();

            // Validate basic parameters
            var paramValidation = ParameterValidator.Create()
                .ValidatePositive(circleVertexCount, nameof(circleVertexCount))
                .ValidatePositive(circleRadius, nameof(circleRadius))
                .ValidatePositive(fieldRes, nameof(fieldRes))
                .ValidatePowerOfTwo(fieldRes, nameof(fieldRes))
                .ValidatePositive(dominanceNoiseFrequency, nameof(dominanceNoiseFrequency))
                .ValidatePositive(dominanceSharpness, nameof(dominanceSharpness))
                .ValidateRange(circleVertexCount, 3, 65536, nameof(circleVertexCount))
                .ValidateRange(circleRadius, 0.01f, 10f, nameof(circleRadius))
                .WarnIf(fieldRes > 2048, "Large texture sizes may impact performance")
                .WarnIf(circleVertexCount > 256, "High vertex count may impact performance")
                .Build();

            result.Merge(paramValidation);

            // Validate graph layers
            if (graphLayers == null || graphLayers.Count == 0)
            {
                result.AddError("At least one graph layer is required");
                return result; // Can't validate further without layers
            }

            for (int i = 0; i < graphLayers.Count; i++)
            {
                var layer = graphLayers[i];
                if (layer == null)
                {
                    result.AddError($"Graph layer {i} is null");
                    continue;
                }

                var layerValidation = layer.Validate();
                foreach (var error in layerValidation.Errors)
                {
                    result.AddError($"Layer {i}: {error}");
                }

                foreach (var warning in layerValidation.Warnings)
                {
                    result.AddWarning($"Layer {i}: {warning}");
                }

                var structureValidation = layer.ValidateGraphStructure();
                foreach (var error in structureValidation.Errors)
                {
                    result.AddError($"Layer {i} graph: {error}");
                }

                foreach (var warning in structureValidation.Warnings)
                {
                    result.AddWarning($"Layer {i} graph: {warning}");
                }
            }

            // Validate weight distribution
            float totalWeight = graphLayers.Where(l => l != null).Sum(l => l.baseWeight);
            if (Math.Abs(totalWeight - 1.0f) > 0.1f)
            {
                result.AddWarning($"Total base weights ({totalWeight:F2}) should sum to approximately 1.0");
            }

            return result;
        }

        #endregion

        #region Internal Methods

        private Result RegenerateWeightMasks()
        {
            return ErrorHandler.TryExecute("FieldGenMain.RegenerateWeightMasks", () =>
                {
                    if (graphLayers == null || graphLayers.Count == 0)
                        throw new InvalidOperationException("No graph layers defined for weight mask regeneration");

                    WeightMask.GenerateWeightMasks(graphLayers, fieldRes, dominanceMasterSeed,
                        dominanceNoiseFrequency, dominanceSharpness);
                    maskRegenQueued = false;

                    Debug.Log($"Weight massks regenerated for {graphLayers.Count} layers");
                }
            );
        }

        private Result ProcessSequentialGraphs()
        {
            // Validate configuration before processing
            var configValidation = ValidateConfiguration();
            if (!configValidation.IsValid)
            {
                return Result.Failure($"Configuration validation failed: {configValidation.GetSummary()}");
            }

            // Log warnings but continue
            if (configValidation.Warnings.Any())
            {
                foreach (var warning in configValidation.Warnings)
                {
                    ErrorHandler.LogWarning("FieldGenMain.ProcessSequentialGraphs", warning);
                }
            }

            return ErrorHandler.TryExecute("FieldGenMain.ProcessSequentialGraphs", () =>
            {
                // Complete any pending rasterization job before disposing old data
                if (rasterizeJobHandle.IsCompleted == false)
                {
                    rasterizeJobHandle.Complete();
                }

                // Dispose old data safely - BUT DON'T DISPOSE FieldData HERE
                // FieldData will be disposed when we create a new one
                if (hasVectorResult && currentVectorResult.IsValid)
                {
                    currentVectorResult.Dispose();
                    hasVectorResult = false;
                }

                if (hasRasterData && currentRaster.IsValid)
                {
                    currentRaster.Dispose();
                    hasRasterData = false;
                }

                // Generate initial circle to seed the first graph
                VectorData currentVector = VectorUtils.CreateCircle(circleRadius, circleVertexCount);

                // Process vector stages
                try
                {
                    for (int i = 0; i < graphLayers.Count; i++)
                    {
                        var layer = graphLayers[i];
                        if (!layer.IsValid)
                        {
                            ErrorHandler.LogWarning("FieldGenMain.ProcessSequentialGraphs",
                                $"Layer {i} has invalid graph, skipping");
                            continue;
                        }

                        // Validate weight mask
                        if (!layer.weightMask.IsCreated || layer.weightMask.Length != fieldRes * fieldRes)
                        {
                            ErrorHandler.LogWarning("FieldGenMain.ProcessSequentialGraphs",
                                $"Weight mask for layer {i} invalid, using equal weights");

                            if (layer.weightMask.IsCreated) layer.weightMask.Dispose();
                            layer.weightMask = new NativeArray<float>(fieldRes * fieldRes, Allocator.Persistent);
                            float equalWeight = 1f / graphLayers.Count;
                            for (int j = 0; j < layer.weightMask.Length; j++)
                                layer.weightMask[j] = equalWeight;

                            // Update the layer in the list since it's a struct
                            graphLayers[i] = layer;
                        }

                        // Process this layer
                        VectorData layerResult =
                            ProcessVectorGraph(layer.graph, currentVector, layer.weightMask, layer.seed);

                        // Only dispose the input vector if it's not the first iteration
                        // (first iteration uses the initial circle which we own)
                        if (i > 0)
                        {
                            currentVector.Dispose();
                        }

                        currentVector = layerResult;
                    }

                    currentVectorResult = currentVector;
                    hasVectorResult = true;
                    OnVectorDataGenerated?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error during vector processing: {e.Message}");
                    if (currentVector.IsValid)
                    {
                        currentVector.Dispose();
                    }

                    throw;
                }

                // Process raster stages
                currentRaster = new RasterData(fieldRes, Allocator.Persistent);
                rasterizeJobHandle =
                    GPUVectorRasterizer.RasterizeVector(currentVectorResult, fieldRes, 1f, ref currentRaster);
                rasterizeJobHandle.Complete();
                hasRasterData = true;

                try
                {
                    for (int i = 0; i < graphLayers.Count; i++)
                    {
                        var layer = graphLayers[i];
                        if (!layer.IsValid)
                        {
                            ErrorHandler.LogWarning("FieldGenMain.ProcessSequentialGraphs",
                                $"Layer {i} has invalid graph, skipping raster processing");
                            continue;
                        }

                        // Validate weight mask (same check as vector processing)
                        if (!layer.weightMask.IsCreated || layer.weightMask.Length != fieldRes * fieldRes)
                        {
                            ErrorHandler.LogWarning("FieldGenMain.ProcessSequentialGraphs",
                                $"Weight mask for layer {i} invalid for raster processing, using equal weights");

                            if (layer.weightMask.IsCreated) layer.weightMask.Dispose();
                            layer.weightMask = new NativeArray<float>(fieldRes * fieldRes, Allocator.Persistent);
                            float equalWeight = 1f / graphLayers.Count;
                            for (int j = 0; j < layer.weightMask.Length; j++)
                                layer.weightMask[j] = equalWeight;

                            // Update the layer in the list since it's a struct
                            graphLayers[i] = layer;
                        }

                        // Process this layer
                        RasterData layerResult =
                            ProcessRasterGraph(layer.graph, currentRaster, layer.weightMask, layer.seed);

                        // Only dispose the input raster if it's not the first iteration
                        if (i > 0)
                        {
                            currentRaster.Dispose();
                        }

                        currentRaster = layerResult;
                    }

                    hasRasterResult = true;

                    // SAFE FIELD DATA REPLACEMENT
                    // Dispose old FieldData before creating new one
                    var oldFieldData = FieldData;

                    // Create new FieldData - this takes ownership of currentRaster and currentVector
                    FieldData = new FieldData(fieldRes, currentRaster, currentVector);

                    // Now it's safe to dispose the old FieldData
                    oldFieldData?.Dispose();

                    OnPlanetDataGenerated?.Invoke();
                    OnDataReady?.Invoke(FieldData);

                    Debug.Log($"Successfully processed {graphLayers.Count} graph layers");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error during raster processing: {e.Message}");
                    if (currentRaster.IsValid)
                    {
                        currentRaster.Dispose();
                    }

                    throw;
                }
            });
        }

        private RasterData ProcessRasterGraph(GeneratorGraph graph, RasterData inputRaster,
            NativeArray<float> weightMask, float seed)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph), "Graph cannot be null");

            var outputNode = graph.nodes.OfType<RasterOutputNode>().FirstOrDefault();
            if (outputNode == null)
                throw new InvalidOperationException("No RasterOutputNode found in graph");

            graph.SetRasterInput(inputRaster);
            graph.SetMaskInput(weightMask, fieldRes);
            graph.SetSeed(seed);

            var outputRaster = new RasterData(fieldRes, Allocator.Persistent);
            var tempBuffers = new TempBufferManager(true);

            JobHandle rasterHandle = default;

            try
            {
                rasterHandle =
                    outputNode.SchedulePlanetData(new JobHandle(), fieldRes, tempBuffers, ref outputRaster);
                rasterHandle.Complete();
                return outputRaster;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during raster processing: {e.Message}");
                if (rasterHandle.IsCompleted == false)
                    rasterHandle.Complete();

                if (outputRaster.IsValid)
                    outputRaster.Dispose();

                throw;
            }
            finally
            {
                tempBuffers.DisposeAll();
                graph.ClearExternalInputs();
            }
        }

        private VectorData ProcessVectorGraph(
            GeneratorGraph graph,
            VectorData inputVector,
            NativeArray<float> weightMask,
            float seed)
        {
            if (graph == null)
            {
                Debug.LogError("Graph is null");

                return default;
            }

            var vectorOutputNode = graph.nodes.OfType<VectorOutputNode>().FirstOrDefault();
            if (vectorOutputNode == null)
            {
                Debug.LogError("No VectorOutputNode found in graph");

                return default;
            }


            graph.SetVectorInput(inputVector);
            graph.SetMaskInput(weightMask, fieldRes);
            graph.SetSeed(seed);


            var outputVector = new VectorData(inputVector.Vertices.Length);
            var tempBuffers = new TempBufferManager(true);

            JobHandle vectorHandle = default;

            try
            {
                vectorHandle =
                    vectorOutputNode.ScheduleVector(new JobHandle(), fieldRes, tempBuffers, ref outputVector);


                vectorHandle.Complete();


                return outputVector;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during vector processing: {e.Message}");
                if (vectorHandle.IsCompleted == false)
                    vectorHandle.Complete();
                return default;
            }
            finally
            {
                tempBuffers.DisposeAll();
                graph.ClearExternalInputs();
            }
        }

        private void DebugDrawVectorData(VectorData vectorData, Color color, float duration = 1f,
            Vector3 center = default, float scale = 1f)
        {
            if (!vectorData.IsValid || vectorData.Count < 2)
            {
                return;
            }

            for (int i = 0; i < vectorData.Count; i++)
            {
                int nextIndex = (i + 1) % vectorData.Count;

                float2 current = vectorData.Vertices[i];
                Vector3 currentPos = center + new Vector3(current.x * scale, current.y * scale, 0f);

                float2 next = vectorData.Vertices[nextIndex];
                Vector3 nextPos = center + new Vector3(next.x * scale, next.y * scale, 0f);

                Debug.DrawLine(currentPos, nextPos, color, duration);
            }
        }

        #endregion
    }
}