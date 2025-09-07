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
    public struct SineWaveNoiseJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> outputBuffer;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float frequency;
        [ReadOnly] public float amplitude;
        [ReadOnly] public float seed;
        [ReadOnly] public float phase;
        [ReadOnly] public float direction;
        
        [ReadOnly] public float globalContribution;
        [ReadOnly] public float globalSeed;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;

            float2 pos = new float2(x / (float)textureSize, y / (float)textureSize);

            // Create directional vector for the wave
            float2 waveDirection = new float2(math.cos(direction), math.sin(direction));
            
            // Project position onto the wave direction
            float projection = math.dot(pos, waveDirection);

            // Calculate sine wave with frequency, phase, and global seed
            float effectiveSeed = seed + globalSeed;
            float waveValue = math.sin(projection * frequency * 2f * math.PI + phase + effectiveSeed);

            // Apply amplitude and global contribution
            outputBuffer[index] = waveValue * amplitude * globalContribution;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct RadialSinWaveJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> outputBuffer;
        
        [ReadOnly] public int textureSize;
        [ReadOnly] public float frequency;
        [ReadOnly] public float amplitude;
        [ReadOnly] public float seed;
        [ReadOnly] public float phase;
        [ReadOnly] public float direction;
        
        [ReadOnly] public float globalContribution;
        [ReadOnly] public float globalSeed;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;
            
            float2 pos = new float2(x / (float)textureSize, y / (float)textureSize);
            float2 center = new float2(0.5f, 0.5f);
            float2 toCenter = pos - center;
            
            float angle = math.atan2(toCenter.y, toCenter.x);
            float effectiveSeed = seed + globalSeed;

            float waveValue = math.sin(angle * frequency);
            // float waveValue = 1f; 
            outputBuffer[index] = waveValue * amplitude * globalContribution;
            // outputBuffer[index] = amplitude;
        }
    }

    [Node.CreateNodeMenu("Noise/Pattern/Sine Wave")]
    public class SineWaveNoiseNode : NoiseGeneratorNode
    {
        [Header("Sine Wave Parameters")]
        [Range(0f, 360f)]
        [Tooltip("Phase offset for the sine wave in degrees")]
        public float phaseDegrees = 0f;

        [Range(0f, 360f)]
        [Tooltip("Direction of the wave in degrees (0 = horizontal, 90 = vertical)")]
        public float directionDegrees = 0f;

        // Convert degrees to radians for internal use
        private float phase => math.radians(phaseDegrees);
        private float direction => math.radians(directionDegrees);

        protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref NativeArray<float> outputBuffer, EvaluationContext context)
        {
            // var noiseJob = new SineWaveNoiseJob
            var noiseJob = new RadialSinWaveJob
            {
                outputBuffer = outputBuffer,
                textureSize = textureSize,
                frequency = this.frequency,
                amplitude = this.amplitude,
                seed = this.seed,
                phase = this.phase,
                direction = this.direction,
                
                globalContribution = context.contribution,
                globalSeed = context.seed,
            };

            return noiseJob.Schedule(textureSize * textureSize, 64, dependency);
        }
    }
}
