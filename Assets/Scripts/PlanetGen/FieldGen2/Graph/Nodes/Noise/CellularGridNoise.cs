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
    public struct CellularGridNoiseJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> outputBuffer;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float frequency;
        [ReadOnly] public float amplitude;
        [ReadOnly] public float seed;
        [ReadOnly] public int octaves;
        
        [ReadOnly] public float globalContribution;
        [ReadOnly] public float globalSeed;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;
            
            float2 pos = new float2(x / (float)textureSize, y / (float)textureSize);
            float effectiveSeed = seed + globalSeed;
            
            // Use floor() to create cellular regions
            float2 cellPos = math.floor(pos * frequency + new float2(effectiveSeed, effectiveSeed));
            float cellNoise = noise.snoise(cellPos * 0.1f);
            
            // Add octaves for more complex patterns
            float finalValue = cellNoise;
            if (octaves > 1)
            {
                float octaveFrequency = frequency * 2f;
                float octaveAmplitude = 0.5f;
                
                for (int i = 1; i < octaves; i++)
                {
                    float2 octavePos = pos * octaveFrequency + new float2(effectiveSeed, effectiveSeed);
                    float2 octaveCellPos = math.floor(octavePos);
                    finalValue += noise.snoise(octaveCellPos * 0.1f) * octaveAmplitude;
                    octaveFrequency *= 2f;
                    octaveAmplitude *= 0.5f;
                }
            }
            
            outputBuffer[index] = finalValue * amplitude * globalContribution;
        }
    }

    [Node.CreateNodeMenu("Noise/Cellular Grid")]
    public class CellularGridNoiseNode : NoiseGeneratorNode
    {
        [Header("Cellular Parameters")]
        [Range(1, 4)]
        [Tooltip("Number of octaves for complexity")]
        public int octaves = 1;

        protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref NativeArray<float> outputBuffer, EvaluationContext context)
        {
            var noiseJob = new CellularGridNoiseJob
            {
                outputBuffer = outputBuffer,
                textureSize = textureSize,
                frequency = this.frequency,
                amplitude = this.amplitude,
                seed = this.seed,
                octaves = this.octaves,
                
                globalContribution = context.contribution,
                globalSeed = context.seed,
            };

            return noiseJob.Schedule(textureSize * textureSize, 64, dependency);
        }
    }
}