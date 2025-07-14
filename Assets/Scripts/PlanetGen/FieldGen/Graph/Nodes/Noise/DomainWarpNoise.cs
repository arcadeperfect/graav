// using System.Collections.Generic;
// using PlanetGen.FieldGen2.Graph.Nodes.Base;
// using Unity.Burst;
// using Unity.Collections;
// using Unity.Jobs;
// using Unity.Mathematics;
// using UnityEngine;
// using XNode;
//
// namespace PlanetGen.FieldGen2.Graph.Nodes.Noise
// {
//     [BurstCompile(CompileSynchronously = true)]
//     public struct DomainWarpNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency;
//         [ReadOnly] public float amplitude;
//         [ReadOnly] public float seed;
//         [ReadOnly] public float warpStrength;
//         [ReadOnly] public float warpFrequency;
//         [ReadOnly] public int octaves;
//         [ReadOnly] public float lacunarity;
//         [ReadOnly] public float persistence;
//         
//         [ReadOnly] public float globalContribution;
//         [ReadOnly] public float globalSeed;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 pos = new float2(x, y) / (float)textureSize;
//             float effectiveSeed = seed + globalSeed;
//             
//             // Generate warp offsets
//             float2 warpPos = pos * warpFrequency + new float2(effectiveSeed, effectiveSeed);
//             float2 warp = new float2(
//                 noise.snoise(warpPos),
//                 noise.snoise(warpPos + new float2(100f, 200f))
//             ) * warpStrength;
//             
//             // Sample noise at warped position with FBM
//             float2 warpedPos = pos + warp;
//             
//             float value = 0f;
//             float currentAmplitude = amplitude;
//             float currentFrequency = frequency;
//             float maxValue = 0f;
//             
//             for (int i = 0; i < octaves; i++)
//             {
//                 float2 noisePos = warpedPos * currentFrequency + new float2(effectiveSeed, effectiveSeed);
//                 float noiseValue = noise.snoise(noisePos);
//                 
//                 value += noiseValue * currentAmplitude;
//                 maxValue += currentAmplitude;
//                 
//                 currentFrequency *= lacunarity;
//                 currentAmplitude *= persistence;
//             }
//             
//             // Normalize to maintain amplitude range
//             if (maxValue > 0f)
//             {
//                 value /= maxValue;
//                 value *= amplitude;
//             }
//             
//             outputBuffer[index] = value * globalContribution;
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Domain Warp")]
//     public class DomainWarpNoiseNode : NoiseGeneratorNode
//     {
//         [Header("Domain Warp Parameters")]
//         [Range(0.1f, 2f)]
//         [Tooltip("Strength of the domain warping effect")]
//         public float warpStrength = 0.5f;
//         
//         [Range(0.1f, 5f)]
//         [Tooltip("Frequency of the warp noise")]
//         public float warpFrequency = 1f;
//         
//         [Header("FBM Parameters")]
//         [Range(1, 8)]
//         [Tooltip("Number of noise octaves")]
//         public int octaves = 4;
//         
//         [Range(1f, 4f)]
//         [Tooltip("Frequency multiplier for each octave")]
//         public float lacunarity = 2f;
//         
//         [Range(0.1f, 1f)]
//         [Tooltip("Amplitude multiplier for each octave")]
//         public float persistence = 0.5f;
//
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer, EvaluationContext context)
//         {
//             var noiseJob = new DomainWarpNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 warpStrength = this.warpStrength,
//                 warpFrequency = this.warpFrequency,
//                 octaves = this.octaves,
//                 lacunarity = this.lacunarity,
//                 persistence = this.persistence,
//                 
//                 globalContribution = context.contribution,
//                 globalSeed = context.seed,
//             };
//
//             return noiseJob.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
// }