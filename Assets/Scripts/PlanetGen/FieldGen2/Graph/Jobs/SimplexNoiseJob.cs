using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PlanetGen.FieldGen2.Graph.Jobs
{
    public struct SimplexNoiseJob : IJobParallelFor
    {
        [WriteOnly]
        public NativeArray<float> outputBuffer;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float frequency;
        [ReadOnly] public float amplitude;
        [ReadOnly] public float seed;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;
        
            float2 pos = new float2(x / (float)textureSize, y / (float)textureSize) * frequency + new float2(seed, seed);
            float noiseValue = (noise.snoise(pos));
        
            outputBuffer[index] = noiseValue * amplitude;
        }
    }
}