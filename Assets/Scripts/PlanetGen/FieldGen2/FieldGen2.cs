using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
        // [TriggerMaskRegen]
        public float baseWeight = 1f;

        [Range(0f, 1000f)] [Tooltip("Random seed for this layer's weight distribution")]
        // [TriggerMaskRegen]
        public float weightSeed = 0f; // Used for unique noise offset for this layer

        [NonSerialized] public NativeArray<float> weightMask;
        // [TriggerMaskRegen]
        public float seed;
        public bool IsValid => graph != null;
    }

    public class FieldGen2 : MonoBehaviour
    {
        #region Events

        public static event Action<FieldGen2> OnVectorDataGenerated;
        public static event Action<FieldGen2> OnPlanetDataGenerated;

        #endregion

        #region Inspector parameters

        // public bool RemoteMode;
        //
        [Header("Sequential Vector Processing")] [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        [TriggerMaskRegen]
        public List<GraphLayer> graphLayers = new();

        [Header("Initial Circle")] 
        [TriggerFieldRegen]
        public int circleVertexCount = 64;
        [TriggerFieldRegen]
        public float circleRadius = 0.5f;

        [Header("Dominance Mask Generation")] 
        [TriggerFieldRegen][TriggerMaskRegen]
        public int textureSize = 512;


        [Range(0.1f, 10f)] [Tooltip("Frequency of the noise patterns for dominance.")]
        [TriggerMaskRegen]
        public float dominanceNoiseFrequency = 2f;

        [Range(1f, 100f)]
        [Tooltip("How sharply influence drops off for non-dominant graphs. Higher = more distinct regions.")]
        [TriggerMaskRegen]
        public float dominanceSharpness = 20f;
        [TriggerMaskRegen]
        public uint dominanceMasterSeed = 123;

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
        public int CurrentRasterSize => textureSize;

        public FieldData2 FieldData;

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

            // if (GetComponent<PlanetGenMain>() != null)
            //     RemoteMode = true;
            //
            // if(RemoteMode)
            //     maskRegenQueued = false;
            // else
            //     maskRegenQueued = true;
            maskRegenQueued = true;
        }
        
        private void OnValidate()
        {
            // if (RemoteMode)
            //     return;
            
            if (dominanceNoiseFrequency <= 0) dominanceNoiseFrequency = 0.1f;
            if (dominanceSharpness <= 0) dominanceSharpness = 1f;
            maskRegenQueued = true;
            QueueRegeneration(Because.ValidationTriggered); //TODO: implement change detection that supports the nested class
        }
        
        void Update()
        {
            // if (paramWatcher != null)
            // {
            //     var changes = paramWatcher.CheckForChanges();
            //     
            //     if (changes.HasMaskRegen())
            //     {
            //         print("mask");
            //         maskRegenQueued = true;
            //         QueueRegeneration(UpdateReason.MaskRegeneration);
            //     }
            //     else if (changes.HasFieldRegen())
            //     {
            //         print("field");
            //         QueueRegeneration(UpdateReason.ValidationTriggered);
            //     }
            // }
            // else
            // {
            //     throw new Exception("ParameterWatcher is null");
            // }

            if (maskRegenQueued)
            {
                RegenerateWeights();
                QueueRegeneration(Because.MaskRegeneration);
            }
            
            if (queuedUpdate.HasValue)
            {
                var update = queuedUpdate.Value;
                Debug.Log($"Processing graphs due to: {update.reason} " +
                          $"(called by {update.caller} in {update.sourceFile}:{update.sourceLine})");
                queuedUpdate = null;
                ProcessSequentialGraphs();
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

            if (hasVectorResult && currentVectorResult.IsValid)
            {
                currentVectorResult.Dispose();
            }

            if (hasRasterData && currentRaster.Scalar.IsCreated)
            {
                currentRaster.Dispose();
            }

            foreach (var layer in graphLayers)
            {
                if (layer.weightMask.IsCreated)
                {
                    layer.weightMask.Dispose();
                }
            }
        }

        #endregion

        #region API

        private void OnNodeParameterChanged()
        {
            // regenQueued = true;
            QueueRegeneration(Because.NodeParameterChanged);

        }
        // void OnNodeParameterChanged() => QueueRegeneration(UpdateReason.NodeParameterChanged);
        public FieldData2 ExternalProcess(float seed)
        {
            // TODO: implement seed
            // QueueRegeneration(Because.ExternalProcess);
            GenerateFieldSynchronous();
            return FieldData;
        }

        #endregion

        #region Internal Methods
        public void GenerateFieldSynchronous(bool forceMaskRegen = false)
        {
            // Ensure any pending async job is completed before we start a new synchronous one
            if (rasterizeJobHandle.IsCompleted == false)
            {
                rasterizeJobHandle.Complete();
            }

            if (forceMaskRegen || maskRegenQueued)
            {
                // Force mask regeneration if needed
                WeightMask.GenerateWeightMasks(graphLayers, textureSize, dominanceMasterSeed, dominanceNoiseFrequency,
                    dominanceSharpness);
                maskRegenQueued = false; // Reset the flag after synchronous generation
            }

            // Directly call the processing logic
            ProcessSequentialGraphs(); // Renamed ProcessSequentialGraphs to avoid confusion and make it clear it's an internal helper
            
        }
        private void ProcessSequentialGraphs()
        {
            // print("Processing sequential graphs");
            if (graphLayers == null || graphLayers.Count == 0)
            {
                Debug.LogError("No graph layers");

                return;
            }

            if (maskRegenQueued)
            {
                WeightMask.GenerateWeightMasks(graphLayers, textureSize, dominanceMasterSeed, dominanceNoiseFrequency,
                    dominanceSharpness);
                maskRegenQueued = false;
            }

            // Complete any pending rasterization job before disposing old data

            if (rasterizeJobHandle.IsCompleted == false)
            {
                rasterizeJobHandle.Complete();
            }


            if (hasVectorResult && currentVectorResult.IsValid)
            {
                currentVectorResult.Dispose();
            }

            // Dispose old raster data
            if (hasRasterData && currentRaster.Scalar.IsCreated)
            {
                currentRaster.Dispose();
                hasRasterData = false;
            }


            // Generate an initial circle to seed the first graph with
            VectorData currentVector = VectorUtils.CreateCircle(circleRadius, circleVertexCount);


            ////////////////////////
            // process vector stages
            // /////////////////////

            try
            {
                for (int i = 0; i < graphLayers.Count; i++)
                {
                    var layer = graphLayers[i];
                    if (!layer.IsValid)
                    {
                        Debug.LogWarning($"Layer {i} has invalid graph, skipping");
                        continue;
                    }

                    if (!layer.weightMask.IsCreated || layer.weightMask.Length != textureSize * textureSize)
                    {
                        Debug.LogWarning(
                            $"Weight mask for layer {i} was not created or resized correctly. Forcing mask regeneration.");
                        maskRegenQueued = true;
                        if (layer.weightMask.IsCreated) layer.weightMask.Dispose();
                        layer.weightMask = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
                        float equalWeight = 1f / graphLayers.Count;
                        for (int j = 0; j < layer.weightMask.Length; j++) layer.weightMask[j] = equalWeight;
                        graphLayers[i] = layer;
                    }


                    // Pass the output of each graph as input to the next
                    VectorData layerResult =
                        ProcessVectorGraph(layer.graph, currentVector, layer.weightMask, layer.seed);


                    if (i > 0)
                    {
                        currentVector.Dispose();
                    }

                    currentVector = layerResult;
                }

                currentVectorResult = currentVector;
                hasVectorResult = true;
                OnVectorDataGenerated?.Invoke(this);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during sequential processing: {e.Message}");
                if (currentVector.IsValid)
                {
                    currentVector.Dispose();
                }
            }


            ////////////////////////
            // Process raster stages
            ////////////////////////


            // rasterize the final vector result

            currentRaster = new RasterData(textureSize, Allocator.Persistent);
            // rasterizeJobHandle =
            //     VectorRasterizer.RasterizeVector(currentVectorResult, textureSize, 1f, ref currentRaster);
            rasterizeJobHandle =
                GPUVectorRasterizer.RasterizeVector(currentVectorResult, textureSize, 1f, ref currentRaster);


            rasterizeJobHandle.Complete(); // Ensure rasterization is complete before proceeding
            hasRasterData = true;


            try
            {
                for (int i = 0; i < graphLayers.Count; i++)
                {
                    var layer = graphLayers[i];
                    if (!layer.IsValid)
                    {
                        Debug.LogWarning($"Layer {i} has invalid graph, skipping");
                        continue;
                    }

                    if (!layer.weightMask.IsCreated || layer.weightMask.Length != textureSize * textureSize)
                    {
                        Debug.LogWarning(
                            $"Weight mask for layer {i} was not created or resized correctly. Forcing mask regeneration.");
                        maskRegenQueued = true;
                        if (layer.weightMask.IsCreated) layer.weightMask.Dispose();
                        layer.weightMask = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
                        float equalWeight = 1f / graphLayers.Count;
                        for (int j = 0; j < layer.weightMask.Length; j++) layer.weightMask[j] = equalWeight;
                        graphLayers[i] = layer;
                    }


                    // Pass the output of each graph as input to the next
                    RasterData layerResult =
                        ProcessRasterGraph(layer.graph, currentRaster, layer.weightMask, layer.seed);


                    if (i > 0)
                    {
                        currentRaster.Dispose();
                    }

                    currentRaster = layerResult;
                }

                hasRasterResult = true;
                FieldData = new FieldData2(textureSize, currentRaster, currentVector);
                OnPlanetDataGenerated?.Invoke(this);
            }

            catch (Exception e)
            {
                Debug.LogError($"Error during raster processing: {e.Message}");
                if (currentRaster.Scalar.IsCreated)
                {
                    currentRaster.Dispose();
                }
            }
        }

        private RasterData ProcessRasterGraph(
            GeneratorGraph graph,
            RasterData inputRaster,
            NativeArray<float> weightMask,
            float seed)
        {
            if (graph == null)
            {
                Debug.LogError("Graph is null");

                return default;
            }

            var outputNode =
                graph.nodes.OfType<RasterOutputNode>().FirstOrDefault(); //todo there should be only one output node
            if (outputNode == null)
            {
                Debug.LogError("No RasterOutputNode found in graph");

                return default;
            }


            graph.SetRasterInput(inputRaster);
            graph.SetMaskInput(weightMask, textureSize);
            graph.SetSeed(seed);


            var outputRaster = new RasterData(textureSize, Allocator.Persistent);
            var tempBuffers = new TempBufferManager(true);

            JobHandle rasterHandle = default;

            try
            {
                rasterHandle = outputNode.SchedulePlanetData(new JobHandle(), textureSize, tempBuffers,
                    ref outputRaster);


                rasterHandle.Complete();


                return outputRaster;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during raster processing: {e.Message}");
                if (rasterHandle.IsCompleted == false)
                    rasterHandle.Complete();
                return default;
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
            graph.SetMaskInput(weightMask, textureSize);
            graph.SetSeed(seed);


            var outputVector = new VectorData(inputVector.Vertices.Length);
            var tempBuffers = new TempBufferManager(true);

            JobHandle vectorHandle = default;

            try
            {
                vectorHandle =
                    vectorOutputNode.ScheduleVector(new JobHandle(), textureSize, tempBuffers, ref outputVector);


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