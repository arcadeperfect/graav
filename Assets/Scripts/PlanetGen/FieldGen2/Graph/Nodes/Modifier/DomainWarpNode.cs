// using System.Collections.Generic;
// using PlanetGen.FieldGen2.Graph.Jobs;
// using Unity.Collections;
// using Unity.Jobs;
// using UnityEngine;
// using XNode;
//
// namespace PlanetGen.FieldGen2.Graph.Nodes
// {
//     [CreateNodeMenu("Modifiers/Domain Warp")]
//     public class DomainWarpNode : BaseNode
//     {
//         [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
//         public BaseNode sourceInput;
//
//         [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
//         public BaseNode warpInput;
//
//         [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
//         public BaseNode output;
//
//         [Range(0f, 100f)] public float warpStrength = 10f;
//         public float warpFrequency = 5f;
//         public bool useSecondaryWarp = false;
//         [Range(0f, 100f)] public float secondaryWarpStrength = 5f;
//
//         public override object GetValue(NodePort port)
//         {
//             if (port?.fieldName == "output" || port == null)
//             {
//                 return this;
//             }
//             return null;
//         }
//
//         public override JobHandle Schedule(JobHandle dependency, int textureSize, List<NativeArray<float>> tempBuffers,
//             ref NativeArray<float> outputBuffer)
//         {
//             var sourceNode = GetInputValue<BaseNode>("sourceInput");
//             var warpNode = GetInputValue<BaseNode>("warpInput");
//
//             if (sourceNode == null)
//             {
//                 Debug.LogError("No source input for domain warp - this will cause issues!");
//                 return dependency;
//             }
//
//             if (warpNode == null)
//             {
//                 Debug.LogError("No warp input for domain warp - this will cause issues!");
//                 return dependency;
//             }
//
//             // Create buffers for source and warp data
//             var sourceBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
//             var warpBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
//             tempBuffers.Add(sourceBuffer);
//             tempBuffers.Add(warpBuffer);
//
//             // Schedule source job
//             JobHandle sourceHandle = sourceNode.Schedule(dependency, textureSize, tempBuffers, ref sourceBuffer);
//             
//             // Schedule warp job
//             JobHandle warpHandle = warpNode.Schedule(dependency, textureSize, tempBuffers, ref warpBuffer);
//             
//             // Combine dependencies
//             JobHandle combinedHandle = JobHandle.CombineDependencies(sourceHandle, warpHandle);
//
//             var domainWarpJob = new DomainWarpJob
//             {
//                 sourceBuffer = sourceBuffer,
//                 warpBuffer = warpBuffer,
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 warpStrength = this.warpStrength,
//                 warpFrequency = this.warpFrequency,
//                 useSecondaryWarp = this.useSecondaryWarp,
//                 secondaryWarpStrength = this.secondaryWarpStrength,
//                 seed = 12345
//             };
//
//             return domainWarpJob.Schedule(textureSize * textureSize, 64, combinedHandle);
//         }
//     }
// }