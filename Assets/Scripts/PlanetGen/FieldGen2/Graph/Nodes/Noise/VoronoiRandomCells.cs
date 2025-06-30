using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Noise
{
    [BurstCompile(CompileSynchronously = true)]
    public struct VoronoiRandomCellsNoiseJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> outputBuffer;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float frequency;
        [ReadOnly] public float amplitude;
        [ReadOnly] public float seed;
        [ReadOnly] public float jitter;
        
        [ReadOnly] public float globalContribution;
        [ReadOnly] public float globalSeed;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;
            
            float2 uv = new float2(x, y) / (float)textureSize;
            float2 scaledUV = uv * frequency;
            
            int2 baseCell = (int2)math.floor(scaledUV);
            
            float minDistance = float.MaxValue;
            float cellValue = 0f;
            float effectiveSeed = seed + globalSeed;
            
            // Check 3x3 neighborhood
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int2 neighborCell = baseCell + new int2(offsetX, offsetY);
                    float2 cellCenter = neighborCell + (int2)0.5f;
                    
                    // Use cell coordinates directly for noise (scaled appropriately)
                    float2 noiseCoord = (float2)neighborCell * 0.1f + new float2(effectiveSeed, effectiveSeed);
                    
                    float2 jitterOffset = new float2(
                        noise.snoise(noiseCoord),
                        noise.snoise(noiseCoord + new float2(100f, 200f))
                    ) * jitter * 0.5f;
                    
                    float2 cellPoint = cellCenter + jitterOffset;
                    float distance = math.distance(scaledUV, cellPoint);
                    
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        cellValue = noise.snoise(noiseCoord + new float2(500f, 600f));
                    }
                }
            }
            
            outputBuffer[index] = cellValue * amplitude * globalContribution;
        }
    }

    [Node.CreateNodeMenu("Noise/Voronoi Random Cells")]
    public class VoronoiRandomCellsNoiseNode : NoiseGeneratorNode
    {
        [Header("Voronoi Parameters")]
        [Range(0f, 1f)]
        [Tooltip("Cell center randomization")]
        public float jitter = 0.5f;

        protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref NativeArray<float> outputBuffer, EvaluationContext context)
        {
            var noiseJob = new VoronoiRandomCellsNoiseJob
            {
                outputBuffer = outputBuffer,
                textureSize = textureSize,
                frequency = this.frequency,
                amplitude = this.amplitude,
                seed = this.seed,
                jitter = this.jitter,
                
                globalContribution = context.contribution,
                globalSeed = context.seed,
            };

            return noiseJob.Schedule(textureSize * textureSize, 64, dependency);
        }
    }
}