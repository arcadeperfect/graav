using System;
using System.Linq;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using PlanetGen.FieldGen2.Graph.Nodes.IO;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.noise;


namespace PlanetGen.FieldGen2
{
    public class FieldGen2 : MonoBehaviour
    {
        [Header("Vector Processing")] public GeneratorGraph vectorGraph;
        public int circleVertexCount = 64;
        public float circleRadius = 0.5f;
        public int textureSize = 512;

        [Header("Debug")] public bool drawDebugLines = true;
        public Color debugColor = Color.red;
        public float debugScale = 10f;
        public bool continuousDebugDraw = true;

        // [UnityEngine.Header("Controls")]
        [Button("Force Evaluation", ButtonSizes.Large)]
        public void ForceEvaluation() => ProcessVector();

        private VectorData currentResult;
        private bool hasResult = false;
        private bool regenQueued = false;

        void Start()
        {
            BaseNode.OnAnyNodeChanged += OnNodeParameterChanged;
            // Initial generation
            regenQueued = true;
        }

        private void OnValidate()
        {
            regenQueued = true;
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
                ProcessVector();
            }

            // Continuously draw debug lines if enabled
            if (continuousDebugDraw && drawDebugLines && hasResult && currentResult.IsValid && currentResult.Count > 1)
            {
                DebugDrawVectorData(currentResult, debugColor, Time.deltaTime * 1.1f, Vector3.zero, debugScale);
            }
        }

        public void ProcessVector()
        {
            if (vectorGraph == null)
            {
                Debug.LogError("Vector graph is null");
                return;
            }

            // Clean up previous result
            if (hasResult && currentResult.IsValid)
            {
                currentResult.Dispose();
            }

            // Create initial circle
            VectorData inputCircle = VectorUtils.CreateCircle(circleRadius, circleVertexCount);
            Debug.Log(
                $"Created input circle: Valid={inputCircle.IsValid}, Count={inputCircle.Count}, Radius={circleRadius}");

            try
            {
                // Process through the graph
                currentResult = ProcessVectorGraph(vectorGraph, inputCircle);
                hasResult = true;

                Debug.Log($"Vector processing complete. Output vertices: {currentResult.Count}");
            }
            finally
            {
                // Clean up input
                inputCircle.Dispose();
            }
        }

        public VectorData ProcessVectorGraph(GeneratorGraph graph, VectorData inputVector)
        {
            Debug.Log($"ProcessVectorGraph - Input vector: Valid={inputVector.IsValid}, Count={inputVector.Count}");

            if (graph == null)
            {
                Debug.LogError("Graph is null");
                return default;
            }

            // Find the vector output node
            var outputNode = graph.nodes.OfType<VectorOutputNode>().FirstOrDefault();
            if (outputNode == null)
            {
                Debug.LogError("No VectorOutputNode found in graph");
                return default;
            }

            Debug.Log("Found VectorOutputNode, setting input...");

            // Create a basic noise mask for testing
            var testMask = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            for (int i = 0; i < testMask.Length; i++)
            {
                int x = i % textureSize;
                int y = i / textureSize;
                float2 pos = new float2(x, y) / textureSize;

                // Simple noise pattern - you can replace this with any pattern
                float noiseValue = Unity.Mathematics.noise.snoise(pos * 5f) * 0.5f + 0.5f; // [0, 1] range
                testMask[i] = noiseValue;
            }

            // Set both vector input and mask input on the graph
            graph.SetVectorInput(inputVector);
            graph.SetMaskInput(testMask, textureSize);

            // Verify inputs were set
            if (graph.TryGetVectorInput(out VectorData testInput))
            {
                Debug.Log($"Graph input set successfully: Count={testInput.Count}");
            }
            else
            {
                Debug.LogError("Failed to set graph input");
            }

            // Create output buffer with same capacity as input
            var outputVector = new VectorData(inputVector.Vertices.Length);
            var tempBuffers = new TempBufferManager(true);

            JobHandle vectorHandle = default;

            try
            {
                Debug.Log("Scheduling vector processing...");
                // Schedule the vector processing
                vectorHandle = outputNode.ScheduleVector(new JobHandle(), textureSize, tempBuffers, ref outputVector);

                // Complete the job first
                vectorHandle.Complete();

                // Debug.Log($"Processing complete: Output Count={outputVector.Count}");
                return outputVector;
            }
            catch (System.Exception e)
            {
                // Debug.LogError($"Error during vector processing: {e.Message}");
                if (vectorHandle.IsCompleted == false)
                    vectorHandle.Complete();
                return default;
            }
            finally
            {
                // Clean up temp buffers and test mask
                tempBuffers.DisposeAll();
                testMask.Dispose();
                graph.ClearExternalInputs();
            }
        }

        public void DebugDrawVectorData(VectorData vectorData, Color color, float duration = 1f,
            Vector3 center = default, float scale = 1f)
        {
            if (!vectorData.IsValid || vectorData.Count < 2)
            {
                Debug.LogWarning("VectorData is invalid or has insufficient vertices for drawing");
                return;
            }

            // Convert polar coordinates to world positions and draw lines
            for (int i = 0; i < vectorData.Count; i++)
            {
                int nextIndex = (i + 1) % vectorData.Count; // Wrap around to close the loop

                // Current vertex: polar (angle, radius) to cartesian (x, y)
                float2 current = vectorData.Vertices[i];
                Vector3 currentPos = center + new Vector3(
                    math.cos(current.x) * current.y * scale,
                    math.sin(current.x) * current.y * scale,
                    0f
                );

                // Next vertex
                float2 next = vectorData.Vertices[nextIndex];
                Vector3 nextPos = center + new Vector3(
                    math.cos(next.x) * next.y * scale,
                    math.sin(next.x) * next.y * scale,
                    0f
                );

                // Draw line segment
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
        }
    }
}