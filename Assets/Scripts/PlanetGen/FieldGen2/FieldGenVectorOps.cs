// using System.Linq;
// using PlanetGen.FieldGen2.Graph;
// using PlanetGen.FieldGen2.Graph.Nodes.IO;
// using Unity.Jobs;
// using Unity.Mathematics;
// using UnityEngine;
//
// namespace PlanetGen.FieldGen2
// {
//     public partial class FieldGen2 : MonoBehaviour
//     {
//         public VectorData ProcessVectorGraph(GeneratorGraph graph, VectorData inputVector)
//         {
//             if (graph == null)
//             {
//                 Debug.LogError("Graph is null");
//                 return default;
//             }
//
//             // Find the vector output node
//             var outputNode = graph.nodes.OfType<VectorInputNode.VectorOutputNode>().FirstOrDefault();
//             if (outputNode == null)
//             {
//                 Debug.LogError("No VectorOutputNode found in graph");
//                 return default;
//             }
//
//             // Set the input vector on the graph
//             graph.SetVectorInput(inputVector);
//
//             // Create output buffer with same capacity as input
//             var outputVector = new VectorData(inputVector.Vertices.Length);
//             var tempBuffers = new TempBufferManager(true);
//
//             try
//             {
//                 // Schedule and complete the vector processing
//                 JobHandle vectorHandle = outputNode.ScheduleVector(new JobHandle(), textureSize, tempBuffers, ref outputVector);
//                 vectorHandle.Complete();
//
//                 return outputVector;
//             }
//             finally
//             {
//                 // Clean up
//                 tempBuffers.DisposeAll();
//                 graph.ClearExternalInputs();
//             }
//         }
//     }
//     
//     public void DebugDrawVectorData(VectorData vectorData, Color color, float duration = 1f, Vector3 center = default, float scale = 1f)
//     {
//         if (!vectorData.IsValid || vectorData.Count < 2)
//         {
//             Debug.LogWarning("VectorData is invalid or has insufficient vertices for drawing");
//             return;
//         }
//
//         // Convert polar coordinates to world positions and draw lines
//         for (int i = 0; i < vectorData.Count; i++)
//         {
//             int nextIndex = (i + 1) % vectorData.Count; // Wrap around to close the loop
//         
//             // Current vertex: polar (angle, radius) to cartesian (x, y)
//             float2 current = vectorData.Vertices[i];
//             Vector3 currentPos = center + new Vector3(
//                 math.cos(current.x) * current.y * scale,
//                 math.sin(current.x) * current.y * scale,
//                 0f
//             );
//         
//             // Next vertex
//             float2 next = vectorData.Vertices[nextIndex];
//             Vector3 nextPos = center + new Vector3(
//                 math.cos(next.x) * next.y * scale,
//                 math.sin(next.x) * next.y * scale,
//                 0f
//             );
//         
//             // Draw line segment
//             Debug.DrawLine(currentPos, nextPos, color, duration);
//         }
//     }
// }