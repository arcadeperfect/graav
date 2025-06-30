using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen.FieldGen2.Graph.Jobs.Generator
{
    [BurstCompile(CompileSynchronously = true)]
    public struct InitializePlanetDataJob : IJobParallelFor
    {
        [WriteOnly] public PlanetData Output;
        [ReadOnly] public int textureSize;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;
            float center = textureSize / 2f;
            float dx = x - center;
            float dy = y - center;
            float angle = math.atan2(dy, dx);
            float altitude = math.distance(new float2(x, y), new float2(center, center)) / (textureSize / 2f);
            Output.Altitude[index] = altitude;
            Output.Angle[index] = angle;
            Output.Scalar[index] = altitude;
        }
    }
}