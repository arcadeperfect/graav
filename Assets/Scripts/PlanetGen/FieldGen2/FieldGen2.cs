using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using PlanetGen.FieldGen2.Graph.Nodes.IO;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.noise; // Use snoise directly
using Random = Unity.Mathematics.Random;

namespace PlanetGen.FieldGen2
{
    [Serializable]
    public class GraphLayer
    {
        [Required] public GeneratorGraph graph;

        [Range(0f, 1f)] [Tooltip("Base weight for this layer's influence")]
        public float baseWeight = 1f;

        [Range(0f, 1000f)] [Tooltip("Random seed for this layer's weight distribution")]
        public float weightSeed = 0f; // Used for unique noise offset for this layer

        [NonSerialized] public NativeArray<float> weightMask;

        public bool IsValid => graph != null;
    }

    public class FieldGen2 : MonoBehaviour
    {
        [Header("Sequential Vector Processing")] [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        public List<GraphLayer> graphLayers = new List<GraphLayer>();

        [Header("Initial Circle")] public int circleVertexCount = 64;
        public float circleRadius = 0.5f;

        [Header("Dominance Mask Generation")] // Renamed header
        public int textureSize = 512;

        [Range(0.1f, 10f)] [Tooltip("Frequency of the noise patterns for dominance.")]
        public float dominanceNoiseFrequency = 2f;

        [Range(1f, 100f)]
        [Tooltip("How sharply influence drops off for non-dominant graphs. Higher = more distinct regions.")]
        public float dominanceSharpness = 20f;

        public uint dominanceMasterSeed = 123;

        [Header("Debug")] public bool drawDebugLines = true;
        public Color debugColor = Color.red;
        public float debugScale = 10f;
        public bool continuousDebugDraw = true;

        [Button("Force Evaluation", ButtonSizes.Large)]
        public void ForceEvaluation() => ProcessSequentialGraphs();

        [Button("Regenerate Weights", ButtonSizes.Medium)]
        public void RegenerateWeights()
        {
            maskRegenQueued = true;
            regenQueued = true;
        }

        private VectorData currentResult;
        private bool hasResult = false;
        private bool regenQueued = false;
        private bool maskRegenQueued = false;

        // Previous values to detect changes in OnValidate for mask regeneration
        private int _prevTextureSize;
        private float _prevDominanceNoiseFrequency;
        private float _prevDominanceSharpness;
        private uint _prevDominanceMasterSeed;
        private List<GraphLayer> _prevGraphLayersSnapshot;

        void Start()
        {
            BaseNode.OnAnyNodeChanged += OnNodeParameterChanged;
            maskRegenQueued = true;
            regenQueued = true;
            TakeSnapshotOfParameters();
        }

        private void OnValidate()
        {
            if (dominanceNoiseFrequency <= 0) dominanceNoiseFrequency = 0.1f;
            if (dominanceSharpness <= 0) dominanceSharpness = 1f;

            if (textureSize != _prevTextureSize ||
                !Mathf.Approximately(dominanceNoiseFrequency, _prevDominanceNoiseFrequency) ||
                !Mathf.Approximately(dominanceSharpness, _prevDominanceSharpness) ||
                dominanceMasterSeed != _prevDominanceMasterSeed ||
                HasGraphLayerParametersChanged())
            {
                maskRegenQueued = true;
            }

            regenQueued = true;
            TakeSnapshotOfParameters();
        }

        void OnNodeParameterChanged()
        {
            regenQueued = true;
        }

        void Update()
        {
            if (regenQueued)
            {
                regenQueued = false;
                ProcessSequentialGraphs();
            }

            if (continuousDebugDraw && drawDebugLines && hasResult && currentResult.IsValid && currentResult.Count > 1)
            {
                DebugDrawVectorData(currentResult, debugColor, Time.deltaTime * 1.1f, Vector3.zero, debugScale);
            }
        }

        public void ProcessSequentialGraphs()
        {
            if (graphLayers == null || graphLayers.Count == 0)
            {
                // Debug.LogError("No graph layers configured");
                return; 
            }

            if (maskRegenQueued)
            {
                // Debug.Log("Regenerating Dominance-based weight masks...");
                // WeightMask.GenerateWeightMasks(graphLayers, textureSize, dominanceMasterSeed, dominanceNoiseFrequency,
                //     dominanceSharpness);
                // maskRegenQueued = false;
                WeightMask.GenerateWeightMasks(graphLayers, textureSize, dominanceMasterSeed, dominanceNoiseFrequency,
                    dominanceSharpness);
                maskRegenQueued = false;
            }

            if (hasResult && currentResult.IsValid)
            {
                currentResult.Dispose();
            }

            VectorData currentVector = VectorUtils.CreateCircle(circleRadius, circleVertexCount);
            // Debug.Log(
            //     $"Created initial circle: Valid={currentVector.IsValid}, Count={currentVector.Count}, Radius={circleRadius}");

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

                    // Debug.Log($"Processing layer {i}: {layer.graph.name}");

                    VectorData layerResult = ProcessVectorGraph(layer.graph, currentVector, layer.weightMask);

                    if (i > 0)
                    {
                        currentVector.Dispose();
                    }

                    currentVector = layerResult;
                }

                currentResult = currentVector;
                hasResult = true;

                // Debug.Log($"Sequential processing complete. Final output vertices: {currentResult.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error during sequential processing: {e.Message}");
                if (currentVector.IsValid)
                {
                    currentVector.Dispose();
                }
            }
        }
        
        public VectorData ProcessVectorGraph(GeneratorGraph graph, VectorData inputVector,
            NativeArray<float> weightMask)
        {
            // Debug.Log($"ProcessVectorGraph - Input vector: Valid={inputVector.IsValid}, Count={inputVector.Count}");

            if (graph == null)
            {
                Debug.LogError("Graph is null");
                return default;
            }

            var outputNode = graph.nodes.OfType<VectorOutputNode>().FirstOrDefault();
            if (outputNode == null)
            {
                Debug.LogError("No VectorOutputNode found in graph");
                return default;
            }

            // Debug.Log("Found VectorOutputNode, setting inputs...");

            graph.SetVectorInput(inputVector);
            graph.SetMaskInput(weightMask, textureSize);

            var outputVector = new VectorData(inputVector.Vertices.Length);
            var tempBuffers = new TempBufferManager(true);

            JobHandle vectorHandle = default;

            try
            {
                // Debug.Log("Scheduling vector processing...");
                vectorHandle = outputNode.ScheduleVector(new JobHandle(), textureSize, tempBuffers, ref outputVector);
                vectorHandle.Complete();

                // Debug.Log($"Processing complete: Output Count={outputVector.Count}");
                return outputVector;
            }
            catch (System.Exception e)
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

        public void DebugDrawVectorData(VectorData vectorData, Color color, float duration = 1f,
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
                Vector3 currentPos = center + new Vector3(
                    math.cos(current.x) * current.y * scale,
                    math.sin(current.x) * current.y * scale,
                    0f
                );

                float2 next = vectorData.Vertices[nextIndex];
                Vector3 nextPos = center + new Vector3(
                    math.cos(next.x) * next.y * scale,
                    math.sin(next.x) * next.y * scale,
                    0f
                );

                Debug.DrawLine(currentPos, nextPos, color, duration);
            }
        }

        void OnDestroy()
        {
            BaseNode.OnAnyNodeChanged -= OnNodeParameterChanged;

            if (hasResult && currentResult.IsValid)
            {
                currentResult.Dispose();
            }

            foreach (var layer in graphLayers)
            {
                if (layer.weightMask.IsCreated)
                {
                    layer.weightMask.Dispose();
                }
            }
        }

        private void TakeSnapshotOfParameters()
        {
            _prevTextureSize = textureSize;
            _prevDominanceNoiseFrequency = dominanceNoiseFrequency;
            _prevDominanceSharpness = dominanceSharpness;
            _prevDominanceMasterSeed = dominanceMasterSeed;

            _prevGraphLayersSnapshot = new List<GraphLayer>(graphLayers.Count);
            foreach (var layer in graphLayers)
            {
                _prevGraphLayersSnapshot.Add(new GraphLayer
                {
                    graph = layer.graph,
                    baseWeight = layer.baseWeight,
                    weightSeed = layer.weightSeed
                });
            }
        }

        private bool HasGraphLayerParametersChanged()
        {
            if (_prevGraphLayersSnapshot == null || graphLayers.Count != _prevGraphLayersSnapshot.Count)
            {
                return true;
            }

            for (int i = 0; i < graphLayers.Count; i++)
            {
                if (graphLayers[i].graph != _prevGraphLayersSnapshot[i].graph ||
                    !Mathf.Approximately(graphLayers[i].baseWeight, _prevGraphLayersSnapshot[i].baseWeight) ||
                    !Mathf.Approximately(graphLayers[i].weightSeed, _prevGraphLayersSnapshot[i].weightSeed))
                {
                    return true;
                }
            }

            return false;
        }
    }
}