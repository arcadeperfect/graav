using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

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
        float noiseValue = (noise.snoise(pos) / 2f) + 0.5f;
        
        outputBuffer[index] = noiseValue * amplitude;
    }
}