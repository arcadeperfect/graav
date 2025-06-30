using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PlanetGen.FieldGen2.Graph.Jobs
{
    public struct CellularNoiseJob : IJobParallelFor
    {
        [WriteOnly]
        public NativeArray<float> outputBuffer;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float frequency;
        [ReadOnly] public float amplitude;
        [ReadOnly] public float seed;
        [ReadOnly] public bool returnF2;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;

            float2 pos = new float2(x / (float)textureSize, y / (float)textureSize) * frequency;
            
            // Calculate cellular noise
            float2 cellNoise = noise.cellular(pos + new float2(seed, seed));
            
            // cellNoise.x contains F1 (distance to closest point)
            // cellNoise.y contains F2 (distance to second closest point)
            float noiseValue = returnF2 ? cellNoise.y : cellNoise.x;
            
            outputBuffer[index] = noiseValue * amplitude;
        }
    }
}