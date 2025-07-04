// using System;
// using System.Collections.Generic;
// using System.Linq;
// using PlanetGen.FieldGen2.Graph;
// using PlanetGen.FieldGen2.Graph.Nodes.Base;
// using PlanetGen.FieldGen2.Graph.Nodes.IO;
// using Sirenix.OdinInspector;
// using Unity.Collections;
// using Unity.Jobs;
// using Unity.Mathematics;
// using UnityEngine;
// using static Unity.Mathematics.noise; // Use snoise directly
// using Random = Unity.Mathematics.Random;
//
// namespace PlanetGen.FieldGen2
// {
//     [Serializable]
//     public class GraphLayer
//     {
//         [Required] public GeneratorGraph graph;
//
//         [Range(0f, 1f)] [Tooltip("Base weight for this layer's influence")]
//         public float baseWeight = 1f; // This can still be used to bias regions
//
//         [Range(0f, 1000f)] [Tooltip("Random seed for this layer's weight distribution")]
//         public float weightSeed = 0f; // Used for unique noise offset for this layer
//
//         [NonSerialized] public NativeArray<float> weightMask;
//
//         public bool IsValid => graph != null;
//     }
//
//     public class FieldGen2 : MonoBehaviour
//     {
//         [Header("Sequential Vector Processing")]
//         [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
//         public List<GraphLayer> graphLayers = new List<GraphLayer>();
//
//         [Header("Initial Circle")]
//         public int circleVertexCount = 64;
//         public float circleRadius = 0.5f;
//
//         [Header("Dominance Mask Generation")] // Renamed header
//         public int textureSize = 512;
//         [Range(0.1f, 10f)] [Tooltip("Frequency of the noise patterns for dominance.")]
//         public float dominanceNoiseFrequency = 2f; // New parameter
//         [Range(1f, 100f)] [Tooltip("How sharply influence drops off for non-dominant graphs. Higher = more distinct regions.")]
//         public float dominanceSharpness = 20f; // New parameter
//         public uint dominanceMasterSeed = 123; // New parameter
//
//         [Header("Debug")]
//         public bool drawDebugLines = true;
//         public Color debugColor = Color.red;
//         public float debugScale = 10f;
//         public bool continuousDebugDraw = true;
//
//         [Button("Force Evaluation", ButtonSizes.Large)]
//         public void ForceEvaluation() => ProcessSequentialGraphs();
//
//         [Button("Regenerate Weights", ButtonSizes.Medium)]
//         public void RegenerateWeights()
//         {
//             maskRegenQueued = true;
//             regenQueued = true;
//         }
//
//         private VectorData currentResult;
//         private bool hasResult = false;
//         private bool regenQueued = false;
//         private bool maskRegenQueued = false;
//
//         // Previous values to detect changes in OnValidate for mask regeneration
//         private int _prevTextureSize;
//         private float _prevDominanceNoiseFrequency; // New prev param
//         private float _prevDominanceSharpness; // New prev param
//         private uint _prevDominanceMasterSeed; // New prev param
//         private List<GraphLayer> _prevGraphLayersSnapshot;
//
//         void Start()
//         {
//             BaseNode.OnAnyNodeChanged += OnNodeParameterChanged;
//             maskRegenQueued = true;
//             regenQueued = true;
//             TakeSnapshotOfParameters();
//         }
//
//         private void OnValidate()
//         {
//             // Ensure parameters are within reasonable bounds
//             if (dominanceNoiseFrequency <= 0) dominanceNoiseFrequency = 0.1f;
//             if (dominanceSharpness <= 0) dominanceSharpness = 1f;
//
//             // Check if mask-related parameters have changed
//             if (textureSize != _prevTextureSize ||
//                 !Mathf.Approximately(dominanceNoiseFrequency, _prevDominanceNoiseFrequency) ||
//                 !Mathf.Approximately(dominanceSharpness, _prevDominanceSharpness) ||
//                 dominanceMasterSeed != _prevDominanceMasterSeed ||
//                 HasGraphLayerParametersChanged())
//             {
//                 maskRegenQueued = true;
//             }
//
//             regenQueued = true;
//             TakeSnapshotOfParameters();
//         }
//
//         void OnNodeParameterChanged()
//         {
//             regenQueued = true;
//         }
//
//         void Update()
//         {
//             if (regenQueued)
//             {
//                 regenQueued = false;
//                 ProcessSequentialGraphs();
//             }
//
//             if (continuousDebugDraw && drawDebugLines && hasResult && currentResult.IsValid && currentResult.Count > 1)
//             {
//                 DebugDrawVectorData(currentResult, debugColor, Time.deltaTime * 1.1f, Vector3.zero, debugScale);
//             }
//         }
//
//         public void ProcessSequentialGraphs()
//         {
//             if (graphLayers == null || graphLayers.Count == 0)
//             {
//                 Debug.LogError("No graph layers configured");
//                 return;
//             }
//
//             if (maskRegenQueued)
//             {
//                 Debug.Log("Regenerating Dominance-based weight masks...");
//                 GenerateWeightMasks();
//                 maskRegenQueued = false;
//             }
//
//             if (hasResult && currentResult.IsValid)
//             {
//                 currentResult.Dispose();
//             }
//
//             VectorData currentVector = VectorUtils.CreateCircle(circleRadius, circleVertexCount);
//             Debug.Log(
//                 $"Created initial circle: Valid={currentVector.IsValid}, Count={currentVector.Count}, Radius={circleRadius}");
//
//             try
//             {
//                 for (int i = 0; i < graphLayers.Count; i++)
//                 {
//                     var layer = graphLayers[i];
//                     if (!layer.IsValid)
//                     {
//                         Debug.LogWarning($"Layer {i} has invalid graph, skipping");
//                         continue;
//                     }
//
//                     // Fallback for uncreated or resized masks (should be rare with proper queueing)
//                     if (!layer.weightMask.IsCreated || layer.weightMask.Length != textureSize * textureSize)
//                     {
//                         Debug.LogWarning($"Weight mask for layer {i} was not created or resized correctly. Forcing mask regeneration.");
//                         maskRegenQueued = true; // Force regeneration on next pass
//                         if (layer.weightMask.IsCreated) layer.weightMask.Dispose();
//                         layer.weightMask = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
//                         // Initialize with an equal distribution as a robust fallback
//                         float equalWeight = 1f / graphLayers.Count;
//                         for (int j = 0; j < layer.weightMask.Length; j++) layer.weightMask[j] = equalWeight;
//                         graphLayers[i] = layer; // Assign back the modified struct
//                     }
//
//                     Debug.Log($"Processing layer {i}: {layer.graph.name}");
//
//                     VectorData layerResult = ProcessVectorGraph(layer.graph, currentVector, layer.weightMask);
//
//                     if (i > 0)
//                     {
//                         currentVector.Dispose();
//                     }
//
//                     currentVector = layerResult;
//                 }
//
//                 currentResult = currentVector;
//                 hasResult = true;
//
//                 Debug.Log($"Sequential processing complete. Final output vertices: {currentResult.Count}");
//             }
//             catch (System.Exception e)
//             {
//                 Debug.LogError($"Error during sequential processing: {e.Message}");
//                 if (currentVector.IsValid)
//                 {
//                     currentVector.Dispose();
//                 }
//             }
//         }
//
//         private void GenerateWeightMasks()
//         {
//             // Dispose existing masks first
//             for (int i = 0; i < graphLayers.Count; i++)
//             {
//                 var layer = graphLayers[i];
//                 if (layer.weightMask.IsCreated)
//                 {
//                     layer.weightMask.Dispose();
//                 }
//             }
//
//             if (graphLayers.Count == 0) return;
//
//             // Prepare all layer weight masks (Persistent allocation for long-term use)
//             var newLayerWeightMasks = new NativeArray<NativeArray<float>>(graphLayers.Count, Allocator.Temp);
//             for (int i = 0; i < graphLayers.Count; i++)
//             {
//                 newLayerWeightMasks[i] = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
//             }
//
//             // Generate a random offset for each layer's noise, derived from master seed + layer seed
//             var layerNoiseOffsets = new NativeArray<float2>(graphLayers.Count, Allocator.Temp);
//             var masterRandom = new Random(dominanceMasterSeed);
//             for(int i = 0; i < graphLayers.Count; i++)
//             {
//                 // Use a combination of master seed and layer's specific seed for unique but predictable offsets
//                 masterRandom.InitState(dominanceMasterSeed + (uint)graphLayers[i].weightSeed * 731 + (uint)i * 127);
//                 layerNoiseOffsets[i] = masterRandom.NextFloat2() * 1000f; // Large random offset
//             }
//
//
//             // Per-pixel processing for dominance-based blending
//             for (int i = 0; i < textureSize * textureSize; i++)
//             {
//                 int x = i % textureSize;
//                 int y = i / textureSize;
//                 float2 pixelPos = new float2(x, y) / textureSize;
//
//                 var unnormalizedInfluences = new NativeArray<float>(graphLayers.Count, Allocator.Temp);
//                 float maxNoiseValue = -float.MaxValue; // Track the highest noise value at this pixel
//
//                 // 1. Calculate noise value for each layer and find the dominant one
//                 for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
//                 {
//                     float2 noiseCoord = pixelPos * dominanceNoiseFrequency + layerNoiseOffsets[layerIdx];
//                     // snoise returns [-1, 1], convert to [0, 1] for easier comparison
//                     float noiseValue = snoise(noiseCoord) * 0.5f + 0.5f;
//
//                     unnormalizedInfluences[layerIdx] = noiseValue; // Store noise value temporarily here
//                     if (noiseValue > maxNoiseValue)
//                     {
//                         maxNoiseValue = noiseValue;
//                     }
//                 }
//
//                 float totalUnnormalizedInfluence = 0f;
//
//                 // 2. Adjust influences based on dominance and apply sharpness
//                 for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
//                 {
//                     float currentNoiseValue = unnormalizedInfluences[layerIdx]; // This holds the [0,1] noise value
//
//                     // Calculate how much less dominant this layer is compared to the winner
//                     float dominanceDelta = maxNoiseValue - currentNoiseValue;
//
//                     // Apply an aggressive falloff using dominanceSharpness.
//                     // If currentNoiseValue == maxNoiseValue, dominanceDelta = 0, so weight is max (1.0).
//                     // As dominanceDelta increases, weight drops off exponentially.
//                     float layerInfluence = math.exp(-dominanceDelta * dominanceSharpness); // Exponential falloff
//                     
//                     unnormalizedInfluences[layerIdx] = layerInfluence * graphLayers[layerIdx].baseWeight;
//                     totalUnnormalizedInfluence += unnormalizedInfluences[layerIdx];
//                 }
//
//                 // 3. Final normalization for the current pixel's weights
//                 if (totalUnnormalizedInfluence > 0f)
//                 {
//                     for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
//                     {
//                         NativeArray<float> currentLayerMask = newLayerWeightMasks[layerIdx];
//                         currentLayerMask[i] = unnormalizedInfluences[layerIdx] / totalUnnormalizedInfluence;
//                         newLayerWeightMasks[layerIdx] = currentLayerMask;
//                     }
//                 }
//                 else
//                 {
//                     // Fallback: This should ideally not happen if maxNoiseValue is always > 0,
//                     // but if it does (e.g., all noise values are 0 or negative), distribute equally.
//                     float equalWeight = 1f / graphLayers.Count;
//                     for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
//                     {
//                         NativeArray<float> currentLayerMask = newLayerWeightMasks[layerIdx];
//                         currentLayerMask[i] = equalWeight;
//                         newLayerWeightMasks[layerIdx] = currentLayerMask;
//                     }
//                 }
//
//                 unnormalizedInfluences.Dispose();
//             }
//
//             layerNoiseOffsets.Dispose(); // Dispose the temporary offsets array
//
//             // Assign normalized weights back to layers
//             for (int layerIndex = 0; layerIndex < graphLayers.Count; layerIndex++)
//             {
//                 var layer = graphLayers[layerIndex];
//                 if (layer.weightMask.IsCreated)
//                 {
//                     layer.weightMask.Dispose();
//                 }
//                 layer.weightMask = newLayerWeightMasks[layerIndex];
//                 graphLayers[layerIndex] = layer;
//             }
//
//             newLayerWeightMasks.Dispose();
//         }
//
//         public VectorData ProcessVectorGraph(GeneratorGraph graph, VectorData inputVector,
//             NativeArray<float> weightMask)
//         {
//             Debug.Log($"ProcessVectorGraph - Input vector: Valid={inputVector.IsValid}, Count={inputVector.Count}");
//
//             if (graph == null)
//             {
//                 Debug.LogError("Graph is null");
//                 return default;
//             }
//
//             var outputNode = graph.nodes.OfType<VectorOutputNode>().FirstOrDefault();
//             if (outputNode == null)
//             {
//                 Debug.LogError("No VectorOutputNode found in graph");
//                 return default;
//             }
//
//             Debug.Log("Found VectorOutputNode, setting inputs...");
//
//             graph.SetVectorInput(inputVector);
//             graph.SetMaskInput(weightMask, textureSize);
//
//             var outputVector = new VectorData(inputVector.Vertices.Length);
//             var tempBuffers = new TempBufferManager(true);
//
//             JobHandle vectorHandle = default;
//
//             try
//             {
//                 Debug.Log("Scheduling vector processing...");
//                 vectorHandle = outputNode.ScheduleVector(new JobHandle(), textureSize, tempBuffers, ref outputVector);
//                 vectorHandle.Complete();
//
//                 Debug.Log($"Processing complete: Output Count={outputVector.Count}");
//                 return outputVector;
//             }
//             catch (System.Exception e)
//             {
//                 Debug.LogError($"Error during vector processing: {e.Message}");
//                 if (vectorHandle.IsCompleted == false)
//                     vectorHandle.Complete();
//                 return default;
//             }
//             finally
//             {
//                 tempBuffers.DisposeAll();
//                 graph.ClearExternalInputs();
//             }
//         }
//
//         public void DebugDrawVectorData(VectorData vectorData, Color color, float duration = 1f,
//             Vector3 center = default, float scale = 1f)
//         {
//             if (!vectorData.IsValid || vectorData.Count < 2)
//             {
//                 return;
//             }
//
//             for (int i = 0; i < vectorData.Count; i++)
//             {
//                 int nextIndex = (i + 1) % vectorData.Count;
//
//                 float2 current = vectorData.Vertices[i];
//                 Vector3 currentPos = center + new Vector3(
//                     math.cos(current.x) * current.y * scale,
//                     math.sin(current.x) * current.y * scale,
//                     0f
//                 );
//
//                 float2 next = vectorData.Vertices[nextIndex];
//                 Vector3 nextPos = center + new Vector3(
//                     math.cos(next.x) * next.y * scale,
//                     math.sin(next.x) * next.y * scale,
//                     0f
//                 );
//
//                 Debug.DrawLine(currentPos, nextPos, color, duration);
//             }
//         }
//
//         void OnDestroy()
//         {
//             BaseNode.OnAnyNodeChanged -= OnNodeParameterChanged;
//
//             if (hasResult && currentResult.IsValid)
//             {
//                 currentResult.Dispose();
//             }
//
//             // Clean up weight masks
//             foreach (var layer in graphLayers)
//             {
//                 if (layer.weightMask.IsCreated)
//                 {
//                     layer.weightMask.Dispose();
//                 }
//             }
//         }
//
//         // Helper to take a snapshot of parameters for change detection
//         private void TakeSnapshotOfParameters()
//         {
//             _prevTextureSize = textureSize;
//             _prevDominanceNoiseFrequency = dominanceNoiseFrequency;
//             _prevDominanceSharpness = dominanceSharpness;
//             _prevDominanceMasterSeed = dominanceMasterSeed;
//
//             _prevGraphLayersSnapshot = new List<GraphLayer>(graphLayers.Count);
//             foreach (var layer in graphLayers)
//             {
//                 _prevGraphLayersSnapshot.Add(new GraphLayer
//                 {
//                     graph = layer.graph,
//                     baseWeight = layer.baseWeight,
//                     weightSeed = layer.weightSeed
//                 });
//             }
//         }
//
//         // Helper to check for changes in graphLayers list or its elements
//         private bool HasGraphLayerParametersChanged()
//         {
//             if (_prevGraphLayersSnapshot == null || graphLayers.Count != _prevGraphLayersSnapshot.Count)
//             {
//                 return true;
//             }
//
//             for (int i = 0; i < graphLayers.Count; i++)
//             {
//                 if (graphLayers[i].graph != _prevGraphLayersSnapshot[i].graph ||
//                     !Mathf.Approximately(graphLayers[i].baseWeight, _prevGraphLayersSnapshot[i].baseWeight) ||
//                     !Mathf.Approximately(graphLayers[i].weightSeed, _prevGraphLayersSnapshot[i].weightSeed))
//                 {
//                     return true;
//                 }
//             }
//             return false;
//         }
//     }
// }

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
        [Header("Sequential Vector Processing")]
        [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        public List<GraphLayer> graphLayers = new List<GraphLayer>();

        [Header("Initial Circle")]
        public int circleVertexCount = 64;
        public float circleRadius = 0.5f;

        [Header("Dominance Mask Generation")] // Renamed header
        public int textureSize = 512;
        [Range(0.1f, 10f)] [Tooltip("Frequency of the noise patterns for dominance.")]
        public float dominanceNoiseFrequency = 2f;
        [Range(1f, 100f)] [Tooltip("How sharply influence drops off for non-dominant graphs. Higher = more distinct regions.")]
        public float dominanceSharpness = 20f;
        public uint dominanceMasterSeed = 123;

        [Header("Debug")]
        public bool drawDebugLines = true;
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
                Debug.LogError("No graph layers configured");
                return;
            }

            if (maskRegenQueued)
            {
                Debug.Log("Regenerating Dominance-based weight masks...");
                GenerateWeightMasks();
                maskRegenQueued = false;
            }

            if (hasResult && currentResult.IsValid)
            {
                currentResult.Dispose();
            }

            VectorData currentVector = VectorUtils.CreateCircle(circleRadius, circleVertexCount);
            Debug.Log(
                $"Created initial circle: Valid={currentVector.IsValid}, Count={currentVector.Count}, Radius={circleRadius}");

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
                        Debug.LogWarning($"Weight mask for layer {i} was not created or resized correctly. Forcing mask regeneration.");
                        maskRegenQueued = true;
                        if (layer.weightMask.IsCreated) layer.weightMask.Dispose();
                        layer.weightMask = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
                        float equalWeight = 1f / graphLayers.Count;
                        for (int j = 0; j < layer.weightMask.Length; j++) layer.weightMask[j] = equalWeight;
                        graphLayers[i] = layer;
                    }

                    Debug.Log($"Processing layer {i}: {layer.graph.name}");

                    VectorData layerResult = ProcessVectorGraph(layer.graph, currentVector, layer.weightMask);

                    if (i > 0)
                    {
                        currentVector.Dispose();
                    }

                    currentVector = layerResult;
                }

                currentResult = currentVector;
                hasResult = true;

                Debug.Log($"Sequential processing complete. Final output vertices: {currentResult.Count}");
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

        private void GenerateWeightMasks()
        {
            for (int i = 0; i < graphLayers.Count; i++)
            {
                var layer = graphLayers[i];
                if (layer.weightMask.IsCreated)
                {
                    layer.weightMask.Dispose();
                }
            }

            if (graphLayers.Count == 0) return;

            var newLayerWeightMasks = new NativeArray<NativeArray<float>>(graphLayers.Count, Allocator.Temp);
            for (int i = 0; i < graphLayers.Count; i++)
            {
                newLayerWeightMasks[i] = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            }

            var layerNoiseOffsets = new NativeArray<float2>(graphLayers.Count, Allocator.Temp);
            var masterRandom = new Random(dominanceMasterSeed);
            for(int i = 0; i < graphLayers.Count; i++)
            {
                masterRandom.InitState(dominanceMasterSeed + (uint)graphLayers[i].weightSeed * 731 + (uint)i * 127);
                layerNoiseOffsets[i] = masterRandom.NextFloat2() * 1000f;
            }

            for (int i = 0; i < textureSize * textureSize; i++)
            {
                int x = i % textureSize;
                int y = i / textureSize;
                
                // --- MODIFIED: Convert pixel position to polar-like coordinates for radial noise generation ---
                // Center the coordinates so atan2 and length behave correctly for a circle centered at 0,0
                float normalizedX = (float)x / (textureSize - 1) - 0.5f; // Range [-0.5, 0.5]
                float normalizedY = (float)y / (textureSize - 1) - 0.5f; // Range [-0.5, 0.5]

                // Angle from -PI to PI
                float angle_coord = math.atan2(normalizedY, normalizedX); 
                
                // Radius from center, scaled to [0,1] to cover the full circle if texture is square
                float radius_coord = math.sqrt(normalizedX * normalizedX + normalizedY * normalizedY) * 2f; 
                radius_coord = math.clamp(radius_coord, 0f, 1f); // Ensure it's within [0,1] for noise

                var unnormalizedInfluences = new NativeArray<float>(graphLayers.Count, Allocator.Temp);
                float maxNoiseValue = -float.MaxValue;

                for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
                {
                    // Use transformed polar coordinates for snoise
                    // The frequencies now apply to angular and radial dimensions, creating radial patterns
                    float2 noiseCoord = new float2(angle_coord * dominanceNoiseFrequency, radius_coord * dominanceNoiseFrequency) + layerNoiseOffsets[layerIdx];
                    float noiseValue = snoise(noiseCoord) * 0.5f + 0.5f; // Range [0, 1]

                    unnormalizedInfluences[layerIdx] = noiseValue;
                    if (noiseValue > maxNoiseValue)
                    {
                        maxNoiseValue = noiseValue;
                    }
                }

                float totalUnnormalizedInfluence = 0f;

                for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
                {
                    float currentNoiseValue = unnormalizedInfluences[layerIdx];
                    float dominanceDelta = maxNoiseValue - currentNoiseValue;
                    
                    float layerInfluence = math.exp(-dominanceDelta * dominanceSharpness);
                    
                    unnormalizedInfluences[layerIdx] = layerInfluence * graphLayers[layerIdx].baseWeight;
                    totalUnnormalizedInfluence += unnormalizedInfluences[layerIdx];
                }

                if (totalUnnormalizedInfluence > 0f)
                {
                    for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
                    {
                        NativeArray<float> currentLayerMask = newLayerWeightMasks[layerIdx];
                        currentLayerMask[i] = unnormalizedInfluences[layerIdx] / totalUnnormalizedInfluence;
                        newLayerWeightMasks[layerIdx] = currentLayerMask;
                    }
                }
                else
                {
                    float equalWeight = 1f / graphLayers.Count;
                    for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
                    {
                        NativeArray<float> currentLayerMask = newLayerWeightMasks[layerIdx];
                        currentLayerMask[i] = equalWeight;
                        newLayerWeightMasks[layerIdx] = currentLayerMask;
                    }
                }
                
                unnormalizedInfluences.Dispose();
            }

            layerNoiseOffsets.Dispose();

            for (int layerIndex = 0; layerIndex < graphLayers.Count; layerIndex++)
            {
                var layer = graphLayers[layerIndex];
                if (layer.weightMask.IsCreated)
                {
                    layer.weightMask.Dispose();
                }
                layer.weightMask = newLayerWeightMasks[layerIndex];
                graphLayers[layerIndex] = layer;
            }

            newLayerWeightMasks.Dispose();
        }

        public VectorData ProcessVectorGraph(GeneratorGraph graph, VectorData inputVector,
            NativeArray<float> weightMask)
        {
            Debug.Log($"ProcessVectorGraph - Input vector: Valid={inputVector.IsValid}, Count={inputVector.Count}");

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

            Debug.Log("Found VectorOutputNode, setting inputs...");

            graph.SetVectorInput(inputVector);
            graph.SetMaskInput(weightMask, textureSize);

            var outputVector = new VectorData(inputVector.Vertices.Length);
            var tempBuffers = new TempBufferManager(true);

            JobHandle vectorHandle = default;

            try
            {
                Debug.Log("Scheduling vector processing...");
                vectorHandle = outputNode.ScheduleVector(new JobHandle(), textureSize, tempBuffers, ref outputVector);
                vectorHandle.Complete();

                Debug.Log($"Processing complete: Output Count={outputVector.Count}");
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