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
    public struct HexagonalCellsNoiseJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> outputBuffer;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float frequency;
        [ReadOnly] public float amplitude;
        [ReadOnly] public float seed;
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
                
                // Convert to hexagonal grid coordinates
                float2 hexCoord = ToHexGrid(scaledUV);
                
                // Generate random value for this hex cell
                float2 noiseCoord = hexCoord * 0.1f + new float2(effectiveSeed, effectiveSeed);
                float cellValue = noise.snoise(noiseCoord);
                
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
        
        // Proper hexagonal grid conversion
        private float2 ToHexGrid(float2 p)
        {
            // Hexagonal grid constants
            const float sqrt3 = 1.732050808f;
            const float sqrt3_3 = sqrt3 / 3f;
            
            // Convert to skewed coordinate system
            float2 s = new float2(
                sqrt3_3 * p.x - 1f/3f * p.y,
                2f/3f * p.y
            );
            
            // Find the cube coordinates
            float q = s.x;
            float r = s.y;
            float s_coord = -q - r;
            
            // Round to nearest hex center
            float rq = math.round(q);
            float rr = math.round(r);
            float rs = math.round(s_coord);
            
            float q_diff = math.abs(rq - q);
            float r_diff = math.abs(rr - r);
            float s_diff = math.abs(rs - s_coord);
            
            // Reset the coordinate with the largest difference
            if (q_diff > r_diff && q_diff > s_diff)
            {
                rq = -rr - rs;
            }
            else if (r_diff > s_diff)
            {
                rr = -rq - rs;
            }
            
            return new float2(rq, rr);
        }
    }

    [Node.CreateNodeMenu("Noise/Hexagonal Cells")]
    public class HexagonalCellsNoiseNode : NoiseGeneratorNode
    {
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
            var noiseJob = new HexagonalCellsNoiseJob
            {
                outputBuffer = outputBuffer,
                textureSize = textureSize,
                frequency = this.frequency,
                amplitude = this.amplitude,
                seed = this.seed,
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