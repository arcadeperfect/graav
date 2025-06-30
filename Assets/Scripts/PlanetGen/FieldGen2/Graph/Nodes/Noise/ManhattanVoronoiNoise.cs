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
    public struct ManhattanVoronoiNoiseJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> outputBuffer;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float frequency;
        [ReadOnly] public float amplitude;
        [ReadOnly] public float seed;
        [ReadOnly] public float jitter;
        [ReadOnly] public int octaves;
        [ReadOnly] public float lacunarity;
        [ReadOnly] public float persistence;
        
        [ReadOnly] public float globalContribution;
        [ReadOnly] public float globalSeed;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;
            
            float2 uv = new float2(x, y) / (float)textureSize;
            float effectiveSeed = seed + globalSeed;
            
            float value = 0f;
            float currentAmplitude = amplitude;
            float currentFrequency = frequency;
            float maxValue = 0f;
            
            for (int i = 0; i < octaves; i++)
            {
                float2 scaledUV = uv * currentFrequency;
                int2 baseCell = (int2)math.floor(scaledUV);
                
                float minDistance = float.MaxValue;
                float cellValue = 0f;
                
                for (int offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int2 neighborCell = baseCell + new int2(offsetX, offsetY);
                        float2 cellCenter = neighborCell + (int2)0.5f;
                        
                        float2 noiseCoord = (float2)neighborCell * 0.1f + new float2(effectiveSeed, effectiveSeed);
                        float2 jitterOffset = new float2(
                            noise.snoise(noiseCoord),
                            noise.snoise(noiseCoord + new float2(100f, 200f))
                        ) * jitter * 0.5f;
                        
                        float2 cellPoint = cellCenter + jitterOffset;
                        
                        // Manhattan distance instead of Euclidean
                        float distance = math.abs(scaledUV.x - cellPoint.x) + math.abs(scaledUV.y - cellPoint.y);
                        
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            cellValue = noise.snoise(noiseCoord + new float2(500f, 600f));
                        }
                    }
                }
                
                value += cellValue * currentAmplitude;
                maxValue += currentAmplitude;
                
                currentFrequency *= lacunarity;
                currentAmplitude *= persistence;
            }
            
            // Normalize to maintain amplitude range
            if (maxValue > 0f)
            {
                value /= maxValue;
                value *= amplitude;
            }
            
            outputBuffer[index] = value * globalContribution;
        }
    }

    [Node.CreateNodeMenu("Noise/Manhattan Voronoi")]
    public class ManhattanVoronoiNoiseNode : NoiseGeneratorNode
    {
        [Header("Voronoi Parameters")]
        [Range(0f, 1f)]
        [Tooltip("Cell center randomization")]
        public float jitter = 0.5f;
        
        [Header("FBM Parameters")]
        [Range(1, 8)]
        [Tooltip("Number of noise octaves")]
        public int octaves = 4;
        
        [Range(1f, 4f)]
        [Tooltip("Frequency multiplier for each octave")]
        public float lacunarity = 2f;
        
        [Range(0.1f, 1f)]
        [Tooltip("Amplitude multiplier for each octave")]
        public float persistence = 0.5f;

        protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref NativeArray<float> outputBuffer, EvaluationContext context)
        {
            var noiseJob = new ManhattanVoronoiNoiseJob
            {
                outputBuffer = outputBuffer,
                textureSize = textureSize,
                frequency = this.frequency,
                amplitude = this.amplitude,
                seed = this.seed,
                jitter = this.jitter,
                octaves = this.octaves,
                lacunarity = this.lacunarity,
                persistence = this.persistence,
                
                globalContribution = context.contribution,
                globalSeed = context.seed,
            };

            return noiseJob.Schedule(textureSize * textureSize, 64, dependency);
        }
    }
}