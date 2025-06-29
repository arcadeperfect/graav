using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen.FieldGen2.Graph.Jobs
{
    [BurstCompile(CompileSynchronously = true)]
    public struct CircleJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> DeformationNoise;
        [WriteOnly] public NativeArray<float> Output;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float radius;
        [ReadOnly] public float deformationAmplitude;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;

            float center = textureSize / 2f;
            float dx = x - center;
            float dy = y - center;
        
            float distanceSquared = dx * dx + dy * dy;
            float angle = math.atan2(dy, dx);
        
            float circleRadius = textureSize * 0.3f; 
            float circleCenterX = textureSize * 0.5f;
            float circleCenterY = textureSize * 0.5f;
        
            float sampleX = circleCenterX + math.cos(angle) * circleRadius;
            float sampleY = circleCenterY + math.sin(angle) * circleRadius;
        
            int noiseSampleX = math.clamp((int)sampleX, 0, textureSize - 1);
            int noiseSampleY = math.clamp((int)sampleY, 0, textureSize - 1);
            int noiseIndex = noiseSampleY * textureSize + noiseSampleX;
        
            float noiseValue = DeformationNoise[noiseIndex];
        
            float normalizer = 1f / (textureSize * textureSize);
            float normalizedDistanceSquared = distanceSquared * normalizer;
            float radiusSquared = radius * radius;
            float scaledAmplitude = deformationAmplitude * 0.01f;
        
            float val = normalizedDistanceSquared < radiusSquared + (noiseValue * scaledAmplitude) ? 1.0f : 0.0f;
        
            Output[index] = val;
        }
    }
}