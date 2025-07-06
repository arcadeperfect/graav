// using System;
// using System.Collections.Generic;
// using System.Linq;
// using PlanetGen.FieldGen2.Graph;
// using PlanetGen.FieldGen2.Graph.Nodes.Base;
// using PlanetGen.FieldGen2.Graph.Nodes.IO;
// using PlanetGen.FieldGen2.Graph.Nodes.Outputs;
// using PlanetGen.FieldGen2.Graph.Types;
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
//         public float baseWeight = 1f;
//
//         [Range(0f, 1000f)] [Tooltip("Random seed for this layer's weight distribution")]
//         public float weightSeed = 0f; // Used for unique noise offset for this layer
//
//         [NonSerialized] public NativeArray<float> weightMask;
//         public float seed;
//         public bool IsValid => graph != null;
//     }
//
//     public class FieldGen2 : MonoBehaviour
//     {
//         public static event Action<FieldGen2> OnVectorDataGenerated;
//         public static event Action<FieldGen2> OnPlanetDataGenerated;
//
//         [Header("Sequential Vector Processing")] [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
//         public List<GraphLayer> graphLayers = new List<GraphLayer>();
//
//         [Header("Initial Circle")] public int circleVertexCount = 64;
//         public float circleRadius = 0.5f;
//
//         [Header("Dominance Mask Generation")] public int textureSize = 512;
//
//
//         [Range(0.1f, 10f)] [Tooltip("Frequency of the noise patterns for dominance.")]
//         public float dominanceNoiseFrequency = 2f;
//
//         [Range(1f, 100f)]
//         [Tooltip("How sharply influence drops off for non-dominant graphs. Higher = more distinct regions.")]
//         public float dominanceSharpness = 20f;
//
//         public uint dominanceMasterSeed = 123;
//
//         [Header("Raster")] public int textureSize = 512;
//         [Header("Debug")] public bool drawDebugLines = true;
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
//         private VectorData currentVectorResult;
//         private RasterData currentRasterResult;
//         private bool hasVectorResult = false;
//         private bool hasRasterResult = false;
//         private bool regenQueued = false;
//         private bool maskRegenQueued = false;
//
//         // Previous values to detect changes in OnValidate for mask regeneration
//         private int _prevTextureSize;
//         private float _prevDominanceNoiseFrequency;
//         private float _prevDominanceSharpness;
//         private uint _prevDominanceMasterSeed;
//         private List<GraphLayer> _prevGraphLayersSnapshot;
//
//         public RasterData currentRaster;
//         private bool hasRasterData = false;
//         private JobHandle rasterizeJobHandle;
//
//         public VectorData CurrentVectorVectorData => currentVectorResult;
//         public RasterData CurrentRaster => currentRaster;
//         public bool HasVectorVectorData => hasVectorResult;
//         public bool HasRasterData => hasRasterData;
//         public int CurrentRasterSize => textureSize;
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
//             if (dominanceNoiseFrequency <= 0) dominanceNoiseFrequency = 0.1f;
//             if (dominanceSharpness <= 0) dominanceSharpness = 1f;
//
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
//             if (continuousDebugDraw && drawDebugLines && hasVectorResult && currentVectorResult.IsValid &&
//                 currentVectorResult.Count > 1)
//             {
//                 DebugDrawVectorData(currentVectorResult, debugColor, Time.deltaTime * 1.1f, Vector3.zero, debugScale);
//             }
//         }
//
//         public void ProcessSequentialGraphs()
//         {
//             if (graphLayers == null || graphLayers.Count == 0)
//             {
//                 Debug.LogError("No graph layers");
//                 return;
//             }
//
//             if (maskRegenQueued)
//             {
//                 WeightMask.GenerateWeightMasks(graphLayers, textureSize, dominanceMasterSeed, dominanceNoiseFrequency,
//                     dominanceSharpness);
//                 maskRegenQueued = false;
//             }
//
//             // Complete any pending rasterization job before disposing old data
//             if (rasterizeJobHandle.IsCompleted == false)
//             {
//                 rasterizeJobHandle.Complete();
//             }
//
//             if (hasVectorResult && currentVectorResult.IsValid)
//             {
//                 currentVectorResult.Dispose();
//             }
//
//             // Dispose old raster data
//             if (hasRasterData && currentRaster.Scalar.IsCreated)
//             {
//                 currentRaster.Dispose();
//                 hasRasterData = false;
//             }
//
//             // Generate an initial circle to seed the first graph with
//             VectorData currentVector = VectorUtils.CreateCircle(circleRadius, circleVertexCount);
//
//
//             ////////////////////////
//             // process vector stages
//             // /////////////////////
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
//                     if (!layer.weightMask.IsCreated || layer.weightMask.Length != textureSize * textureSize)
//                     {
//                         Debug.LogWarning(
//                             $"Weight mask for layer {i} was not created or resized correctly. Forcing mask regeneration.");
//                         maskRegenQueued = true;
//                         if (layer.weightMask.IsCreated) layer.weightMask.Dispose();
//                         layer.weightMask = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
//                         float equalWeight = 1f / graphLayers.Count;
//                         for (int j = 0; j < layer.weightMask.Length; j++) layer.weightMask[j] = equalWeight;
//                         graphLayers[i] = layer;
//                     }
//
//                     // Pass the output of each graph as input to the next
//                     VectorData layerResult =
//                         ProcessVectorGraph(layer.graph, currentVector, layer.weightMask, layer.seed);
//
//                     if (i > 0)
//                     {
//                         currentVector.Dispose();
//                     }
//
//                     currentVector = layerResult;
//                 }
//
//                 currentVectorResult = currentVector;
//                 hasVectorResult = true;
//                 OnVectorDataGenerated?.Invoke(this);
//
//
//                 // Complete the job immediately so we can safely dispose the vector data
//                 // rasterizeJobHandle.Complete();
//                 // hasRasterData = true;
//                 // OnPlanetDataGenerated?.Invoke(this);
//             }
//             catch (System.Exception e)
//             {
//                 Debug.LogError($"Error during sequential processing: {e.Message}");
//                 if (currentVector.IsValid)
//                 {
//                     currentVector.Dispose();
//                 }
//             }
//
//
//             ////////////////////////
//             // Process raster stages
//             ////////////////////////
//
//             // rasterize the final vector result
//             currentRaster = new RasterData(textureSize, Allocator.Persistent);
//             rasterizeJobHandle =
//                 VectorRasterizer.RasterizeVector(currentVectorResult, textureSize, 1f, ref currentRaster);
//
//             rasterizeJobHandle.Complete();
//             hasRasterData = true;
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
//                     if (!layer.weightMask.IsCreated || layer.weightMask.Length != textureSize * textureSize)
//                     {
//                         Debug.LogWarning(
//                             $"Weight mask for layer {i} was not created or resized correctly. Forcing mask regeneration.");
//                         maskRegenQueued = true;
//                         if (layer.weightMask.IsCreated) layer.weightMask.Dispose();
//                         layer.weightMask = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
//                         float equalWeight = 1f / graphLayers.Count;
//                         for (int j = 0; j < layer.weightMask.Length; j++) layer.weightMask[j] = equalWeight;
//                         graphLayers[i] = layer;
//                     }
//
//                     // Pass the output of each graph as input to the next
//                     RasterData layerResult =
//                         ProcessRasterGraph(layer.graph, currentRaster, layer.weightMask, layer.seed);
//                     if (i > 0)
//                     {
//                         currentRaster.Dispose();
//                     }
//
//                     currentRaster = layerResult;
//                 }
//                 currentRasterResult = currentRaster;
//                 hasRasterResult = true;
//                 OnPlanetDataGenerated?.Invoke(this);
//             }
//             catch (System.Exception e)
//             {
//                 Debug.LogError($"Error during raster processing: {e.Message}");
//                 if (currentRaster.Scalar.IsCreated)
//                 {
//                     currentRaster.Dispose();
//                 }
//             }
//             // finally
//             // {
//             //     // Complete the rasterization job to ensure all data is ready
//             //     if (rasterizeJobHandle.IsCompleted == false)
//             //     {
//             //         rasterizeJobHandle.Complete();
//             //     }
//             //
//             //     hasRasterData = true;
//             // }
//         }
//
//         public RasterData ProcessRasterGraph(
//             GeneratorGraph graph,
//             RasterData inputRaster,
//             NativeArray<float> weightMask,
//             float seed)
//         {
//             if (graph == null)
//             {
//                 Debug.LogError("Graph is null");
//                 return default;
//             }
//
//             var outputNode =
//                 graph.nodes.OfType<RasterOutputNode>().FirstOrDefault(); //todo there should be only one output node
//             if (outputNode == null)
//             {
//                 Debug.LogError("No RasterOutputNode found in graph");
//                 return default;
//             }
//
//             graph.SetRasterInput(inputRaster);
//             graph.SetMaskInput(weightMask, textureSize);
//             graph.SetSeed(seed);
//
//             var outputRaster = new RasterData(textureSize, Allocator.Persistent);
//             var tempBuffers = new TempBufferManager(true);
//
//             JobHandle rasterHandle = default;
//
//             try
//             {
//                 rasterHandle = outputNode.SchedulePlanetData(new JobHandle(), textureSize, tempBuffers,
//                     ref outputRaster);
//                 rasterHandle.Complete();
//
//                 return outputRaster;
//             }
//             catch (System.Exception e)
//             {
//                 Debug.LogError($"Error during raster processing: {e.Message}");
//                 if (rasterHandle.IsCompleted == false)
//                     rasterHandle.Complete();
//                 return default;
//             }
//             finally
//             {
//                 tempBuffers.DisposeAll();
//                 graph.ClearExternalInputs();
//             }
//         }
//
//         public VectorData ProcessVectorGraph(
//             GeneratorGraph graph,
//             VectorData inputVector,
//             NativeArray<float> weightMask,
//             float seed)
//         {
//             if (graph == null)
//             {
//                 Debug.LogError("Graph is null");
//                 return default;
//             }
//
//             var vectorOutputNode = graph.nodes.OfType<VectorOutputNode>().FirstOrDefault();
//             if (vectorOutputNode == null)
//             {
//                 Debug.LogError("No VectorOutputNode found in graph");
//                 return default;
//             }
//
//             graph.SetVectorInput(inputVector);
//             graph.SetMaskInput(weightMask, textureSize);
//             graph.SetSeed(seed);
//
//
//             var outputVector = new VectorData(inputVector.Vertices.Length);
//             var tempBuffers = new TempBufferManager(true);
//
//             JobHandle vectorHandle = default;
//
//             try
//             {
//                 // Debug.Log("Scheduling vector processing...");
//                 vectorHandle =
//                     vectorOutputNode.ScheduleVector(new JobHandle(), textureSize, tempBuffers, ref outputVector);
//                 vectorHandle.Complete();
//
//                 // Debug.Log($"Processing complete: Output Count={outputVector.Count}");
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
//                 // float2 current = vectorData.Vertices[i];
//                 // Vector3 currentPos = center + new Vector3(
//                 //     math.cos(current.x) * current.y * scale,
//                 //     math.sin(current.x) * current.y * scale,
//                 //     0f
//                 // );
//                 float2 current = vectorData.Vertices[i];
//                 Vector3 currentPos = center + new Vector3(current.x * scale, current.y * scale, 0f);
//
//                 // float2 next = vectorData.Vertices[nextIndex];
//                 // Vector3 nextPos = center + new Vector3(
//                 //     math.cos(next.x) * next.y * scale,
//                 //     math.sin(next.x) * next.y * scale,
//                 //     0f
//                 // );
//                 float2 next = vectorData.Vertices[nextIndex];
//                 Vector3 nextPos = center + new Vector3(next.x * scale, next.y * scale, 0f);
//
//                 Debug.DrawLine(currentPos, nextPos, color, duration);
//             }
//         }
//
//         void OnDestroy()
//         {
//             BaseNode.OnAnyNodeChanged -= OnNodeParameterChanged;
//
//             // Complete any pending jobs
//             if (rasterizeJobHandle.IsCompleted == false)
//             {
//                 rasterizeJobHandle.Complete();
//             }
//
//             if (hasVectorResult && currentVectorResult.IsValid)
//             {
//                 currentVectorResult.Dispose();
//             }
//
//             if (hasRasterData && currentRaster.Scalar.IsCreated)
//             {
//                 currentRaster.Dispose();
//             }
//
//             foreach (var layer in graphLayers)
//             {
//                 if (layer.weightMask.IsCreated)
//                 {
//                     layer.weightMask.Dispose();
//                 }
//             }
//         }
//
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
//
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
using PlanetGen.FieldGen2.Graph.Nodes.Outputs;
using PlanetGen.FieldGen2.Graph.Types;
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

        [Range(0f, 1000f)] [Tooltip("Random seed for this layer's weight distribution")]
        public float weightSeed = 0f; // Used for unique noise offset for this layer

        [NonSerialized] public NativeArray<float> weightMask;
        public float seed;
        public bool IsValid => graph != null;
    }

    public class FieldGen2 : MonoBehaviour
    {
        public static event Action<FieldGen2> OnVectorDataGenerated;
        public static event Action<FieldGen2> OnPlanetDataGenerated;

        [Header("Sequential Vector Processing")] [ListDrawerSettings(ShowIndexLabels = true, DraggableItems = true)]
        public List<GraphLayer> graphLayers = new List<GraphLayer>();

        [Header("Initial Circle")] public int circleVertexCount = 64;
        public float circleRadius = 0.5f;

        [Header("Dominance Mask Generation")] public int textureSize = 512;


        [Range(0.1f, 10f)] [Tooltip("Frequency of the noise patterns for dominance.")]
        public float dominanceNoiseFrequency = 2f;

        [Range(1f, 100f)]
        [Tooltip("How sharply influence drops off for non-dominant graphs. Higher = more distinct regions.")]
        public float dominanceSharpness = 20f;

        public uint dominanceMasterSeed = 123;

        // [Header("Raster")] public int textureSize = 512;
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

        private VectorData currentVectorResult;
        private RasterData currentRasterResult;
        private bool hasVectorResult = false;
        private bool hasRasterResult = false;
        private bool regenQueued = false;
        private bool maskRegenQueued = false;

        // Previous values to detect changes in OnValidate for mask regeneration
        private int _prevTextureSize;
        private float _prevDominanceNoiseFrequency;
        private float _prevDominanceSharpness;
        private uint _prevDominanceMasterSeed;
        private List<GraphLayer> _prevGraphLayersSnapshot;

        public RasterData currentRaster;
        private bool hasRasterData = false;
        private JobHandle rasterizeJobHandle;

        public VectorData CurrentVectorVectorData => currentVectorResult;
        public RasterData CurrentRaster => currentRaster;
        public bool HasVectorVectorData => hasVectorResult;
        public bool HasRasterData => hasRasterData;
        public int CurrentRasterSize => textureSize;

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

            if (continuousDebugDraw && drawDebugLines && hasVectorResult && currentVectorResult.IsValid &&
                currentVectorResult.Count > 1)
            {
                DebugDrawVectorData(currentVectorResult, debugColor, Time.deltaTime * 1.1f, Vector3.zero, debugScale);
            }
        }

        public void ProcessSequentialGraphs()
        {
            Profiler.BeginSample("FieldGen2.ProcessSequentialGraphs");

            if (graphLayers == null || graphLayers.Count == 0)
            {
                Debug.LogError("No graph layers");
                Profiler.EndSample();
                return;
            }

            if (maskRegenQueued)
            {
                Profiler.BeginSample("FieldGen2.GenerateWeightMasks");
                WeightMask.GenerateWeightMasks(graphLayers, textureSize, dominanceMasterSeed, dominanceNoiseFrequency,
                    dominanceSharpness);
                maskRegenQueued = false;
                Profiler.EndSample();
            }

            // Complete any pending rasterization job before disposing old data
            Profiler.BeginSample("FieldGen2.CompletePendingRasterJob");
            if (rasterizeJobHandle.IsCompleted == false)
            {
                rasterizeJobHandle.Complete();
            }
            Profiler.EndSample();


            Profiler.BeginSample("FieldGen2.DisposePreviousData");
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
            Profiler.EndSample();


            Profiler.BeginSample("FieldGen2.CreateInitialCircle");
            // Generate an initial circle to seed the first graph with
            VectorData currentVector = VectorUtils.CreateCircle(circleRadius, circleVertexCount);
            Profiler.EndSample();


            ////////////////////////
            // process vector stages
            // /////////////////////
            Profiler.BeginSample("FieldGen2.ProcessVectorStages");
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

                    Profiler.BeginSample($"FieldGen2.ProcessVectorGraph_Layer_{i}");
                    // Pass the output of each graph as input to the next
                    VectorData layerResult =
                        ProcessVectorGraph(layer.graph, currentVector, layer.weightMask, layer.seed);
                    Profiler.EndSample();


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
            catch (System.Exception e)
            {
                Debug.LogError($"Error during sequential processing: {e.Message}");
                if (currentVector.IsValid)
                {
                    currentVector.Dispose();
                }
            }
            Profiler.EndSample();


            ////////////////////////
            // Process raster stages
            ////////////////////////
            Profiler.BeginSample("FieldGen2.ProcessRasterStages");

            // rasterize the final vector result
            Profiler.BeginSample("FieldGen2.RasterizeVector");
            currentRaster = new RasterData(textureSize, Allocator.Persistent);
            // rasterizeJobHandle =
            //     VectorRasterizer.RasterizeVector(currentVectorResult, textureSize, 1f, ref currentRaster);
            rasterizeJobHandle = GPUVectorRasterizer.RasterizeVector(currentVectorResult, textureSize, 1f, ref currentRaster);


            rasterizeJobHandle.Complete(); // Ensure rasterization is complete before proceeding
            hasRasterData = true;
            Profiler.EndSample();
            
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

                    Profiler.BeginSample($"FieldGen2.ProcessRasterGraph_Layer_{i}");
                    // Pass the output of each graph as input to the next
                    RasterData layerResult =
                        ProcessRasterGraph(layer.graph, currentRaster, layer.weightMask, layer.seed);
                    Profiler.EndSample();

                    if (i > 0)
                    {
                        currentRaster.Dispose();
                    }

                    currentRaster = layerResult;
                }
                currentRasterResult = currentRaster;
                hasRasterResult = true;
                OnPlanetDataGenerated?.Invoke(this);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error during raster processing: {e.Message}");
                if (currentRaster.Scalar.IsCreated)
                {
                    currentRaster.Dispose();
                }
            }
            Profiler.EndSample(); // End ProcessRasterStages
            Profiler.EndSample(); // End FieldGen2.ProcessSequentialGraphs
        }

        public RasterData ProcessRasterGraph(
            GeneratorGraph graph,
            RasterData inputRaster,
            NativeArray<float> weightMask,
            float seed)
        {
            Profiler.BeginSample("FieldGen2.ProcessRasterGraph");

            if (graph == null)
            {
                Debug.LogError("Graph is null");
                Profiler.EndSample();
                return default;
            }

            var outputNode =
                graph.nodes.OfType<RasterOutputNode>().FirstOrDefault(); //todo there should be only one output node
            if (outputNode == null)
            {
                Debug.LogError("No RasterOutputNode found in graph");
                Profiler.EndSample();
                return default;
            }

            Profiler.BeginSample("FieldGen2.ProcessRasterGraph.SetInputs");
            graph.SetRasterInput(inputRaster);
            graph.SetMaskInput(weightMask, textureSize);
            graph.SetSeed(seed);
            Profiler.EndSample();

            var outputRaster = new RasterData(textureSize, Allocator.Persistent);
            var tempBuffers = new TempBufferManager(true);

            JobHandle rasterHandle = default;

            try
            {
                Profiler.BeginSample("FieldGen2.ProcessRasterGraph.ScheduleJob");
                rasterHandle = outputNode.SchedulePlanetData(new JobHandle(), textureSize, tempBuffers,
                    ref outputRaster);
                Profiler.EndSample();

                Profiler.BeginSample("FieldGen2.ProcessRasterGraph.CompleteJob");
                rasterHandle.Complete();
                Profiler.EndSample();

                return outputRaster;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error during raster processing: {e.Message}");
                if (rasterHandle.IsCompleted == false)
                    rasterHandle.Complete();
                return default;
            }
            finally
            {
                Profiler.BeginSample("FieldGen2.ProcessRasterGraph.Cleanup");
                tempBuffers.DisposeAll();
                graph.ClearExternalInputs();
                Profiler.EndSample();
                Profiler.EndSample(); // End FieldGen2.ProcessRasterGraph
            }
        }

        public VectorData ProcessVectorGraph(
            GeneratorGraph graph,
            VectorData inputVector,
            NativeArray<float> weightMask,
            float seed)
        {
            Profiler.BeginSample("FieldGen2.ProcessVectorGraph");

            if (graph == null)
            {
                Debug.LogError("Graph is null");
                Profiler.EndSample();
                return default;
            }

            var vectorOutputNode = graph.nodes.OfType<VectorOutputNode>().FirstOrDefault();
            if (vectorOutputNode == null)
            {
                Debug.LogError("No VectorOutputNode found in graph");
                Profiler.EndSample();
                return default;
            }

            Profiler.BeginSample("FieldGen2.ProcessVectorGraph.SetInputs");
            graph.SetVectorInput(inputVector);
            graph.SetMaskInput(weightMask, textureSize);
            graph.SetSeed(seed);
            Profiler.EndSample();


            var outputVector = new VectorData(inputVector.Vertices.Length);
            var tempBuffers = new TempBufferManager(true);

            JobHandle vectorHandle = default;

            try
            {
                Profiler.BeginSample("FieldGen2.ProcessVectorGraph.ScheduleJob");
                vectorHandle =
                    vectorOutputNode.ScheduleVector(new JobHandle(), textureSize, tempBuffers, ref outputVector);
                Profiler.EndSample();

                Profiler.BeginSample("FieldGen2.ProcessVectorGraph.CompleteJob");
                vectorHandle.Complete();
                Profiler.EndSample();

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
                Profiler.BeginSample("FieldGen2.ProcessVectorGraph.Cleanup");
                tempBuffers.DisposeAll();
                graph.ClearExternalInputs();
                Profiler.EndSample();
                Profiler.EndSample(); // End FieldGen2.ProcessVectorGraph
            }
        }

        public void DebugDrawVectorData(VectorData vectorData, Color color, float duration = 1f,
            Vector3 center = default, float scale = 1f)
        {
            Profiler.BeginSample("FieldGen2.DebugDrawVectorData");

            if (!vectorData.IsValid || vectorData.Count < 2)
            {
                Profiler.EndSample();
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
            Profiler.EndSample();
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