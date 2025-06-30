using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PlanetGen.FieldGen2.Graph.Jobs
{
    [BurstCompile(CompileSynchronously = true)]
    public struct DomainWarpJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> sourceBuffer;
        [ReadOnly] public NativeArray<float> warpBuffer;
        [WriteOnly] public NativeArray<float> outputBuffer;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float warpStrength;
        [ReadOnly] public float warpFrequency;
        [ReadOnly] public bool useSecondaryWarp;
        [ReadOnly] public float secondaryWarpStrength;
        [ReadOnly] public float seed;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;

            // Normalize coordinates to 0-1 range
            float2 normalizedPos = new float2(x / (float)textureSize, y / (float)textureSize);
            
            // Generate primary warp offset using the input warp buffer value
            float warpValue = warpBuffer[index];
            
            // Generate additional warp noise for more complex distortion
            float2 warpPos = normalizedPos * warpFrequency + new float2(seed, seed);
            float2 warpOffset = new float2(
                noise.snoise(warpPos) * warpStrength,
                noise.snoise(warpPos + new float2(100f, 100f)) * warpStrength
            );
            
            // Scale the warp offset by the input warp value
            warpOffset *= warpValue;
            
            // Optional secondary warp for more complex distortion
            if (useSecondaryWarp)
            {
                float2 secondaryWarpPos = (normalizedPos + warpOffset * 0.1f) * warpFrequency * 2f + new float2(seed + 200f, seed + 200f);
                float2 secondaryOffset = new float2(
                    noise.snoise(secondaryWarpPos) * secondaryWarpStrength,
                    noise.snoise(secondaryWarpPos + new float2(300f, 300f)) * secondaryWarpStrength
                );
                
                warpOffset += secondaryOffset * warpValue;
            }
            
            // Apply warp offset to get the warped position
            float2 warpedPos = normalizedPos + warpOffset / textureSize;
            
            // Convert back to texture coordinates
            int warpedX = (int)math.round(warpedPos.x * textureSize);
            int warpedY = (int)math.round(warpedPos.y * textureSize);
            
            // Handle wrapping/clamping - you can choose the behavior you prefer
            warpedX = math.clamp(warpedX, 0, textureSize - 1);
            warpedY = math.clamp(warpedY, 0, textureSize - 1);
            
            // Sample from the source buffer at the warped position
            int warpedIndex = warpedY * textureSize + warpedX;
            
            // Bilinear interpolation for smoother results (optional)
            float sampledValue;
            if (warpedPos.x >= 0 && warpedPos.x < 1 && warpedPos.y >= 0 && warpedPos.y < 1)
            {
                sampledValue = BilinearSample(sourceBuffer, warpedPos * textureSize, textureSize);
            }
            else
            {
                sampledValue = sourceBuffer[warpedIndex];
            }
            
            outputBuffer[index] = sampledValue;
        }
        
        private float BilinearSample(NativeArray<float> buffer, float2 pos, int size)
        {
            int x0 = (int)math.floor(pos.x);
            int y0 = (int)math.floor(pos.y);
            int x1 = math.min(x0 + 1, size - 1);
            int y1 = math.min(y0 + 1, size - 1);
            
            x0 = math.clamp(x0, 0, size - 1);
            y0 = math.clamp(y0, 0, size - 1);
            
            float fx = pos.x - x0;
            float fy = pos.y - y0;
            
            float a = buffer[y0 * size + x0];
            float b = buffer[y0 * size + x1];
            float c = buffer[y1 * size + x0];
            float d = buffer[y1 * size + x1];
            
            float i1 = math.lerp(a, b, fx);
            float i2 = math.lerp(c, d, fx);
            
            return math.lerp(i1, i2, fy);
        }
    }
}