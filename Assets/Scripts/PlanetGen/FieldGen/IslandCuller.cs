using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PlanetGen.FieldGen
{
    public static class IslandCuller
    {
        /// <summary>
        /// Serial flood fill - simple but slower for large textures
        /// </summary>
        public static void FloodFillSerial(NativeArray<float> fieldData, int texWidth)
        {
            var visited = new NativeArray<bool>(texWidth * texWidth, Allocator.Temp);
            var queue = new Queue<int2>();

            // Find the center pixel or nearest land pixel to it
            int centerX = texWidth / 2;
            int centerY = texWidth / 2;
            int startIndex = -1;

            // Start from center if it's land
            int centerIndex = centerY * texWidth + centerX;
            if (fieldData[centerIndex] > 0.5f)
            {
                startIndex = centerIndex;
            }
            else
            {
                // Find nearest land pixel to center
                float nearestDist = float.MaxValue;
                for (int y = 0; y < texWidth; y++)
                {
                    for (int x = 0; x < texWidth; x++)
                    {
                        int index = y * texWidth + x;
                        if (fieldData[index] > 0.5f)
                        {
                            float dist = math.distance(new float2(x, y), new float2(centerX, centerY));
                            if (dist < nearestDist)
                            {
                                nearestDist = dist;
                                startIndex = index;
                            }
                        }
                    }
                }
            }

            if (startIndex == -1)
            {
                visited.Dispose();
                return; // No land found
            }

            // Start flood fill
            queue.Enqueue(new int2(startIndex % texWidth, startIndex / texWidth));
            visited[startIndex] = true;

            // 4-directional flood fill without managed arrays
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int x = current.x;
                int y = current.y;
                
                // Check 4 neighbors individually
                // Right
                if (x + 1 < texWidth)
                {
                    int rightIndex = y * texWidth + (x + 1);
                    if (!visited[rightIndex] && fieldData[rightIndex] > 0.5f)
                    {
                        visited[rightIndex] = true;
                        queue.Enqueue(new int2(x + 1, y));
                    }
                }
                
                // Down
                if (y + 1 < texWidth)
                {
                    int downIndex = (y + 1) * texWidth + x;
                    if (!visited[downIndex] && fieldData[downIndex] > 0.5f)
                    {
                        visited[downIndex] = true;
                        queue.Enqueue(new int2(x, y + 1));
                    }
                }
                
                // Left
                if (x - 1 >= 0)
                {
                    int leftIndex = y * texWidth + (x - 1);
                    if (!visited[leftIndex] && fieldData[leftIndex] > 0.5f)
                    {
                        visited[leftIndex] = true;
                        queue.Enqueue(new int2(x - 1, y));
                    }
                }
                
                // Up
                if (y - 1 >= 0)
                {
                    int upIndex = (y - 1) * texWidth + x;
                    if (!visited[upIndex] && fieldData[upIndex] > 0.5f)
                    {
                        visited[upIndex] = true;
                        queue.Enqueue(new int2(x, y - 1));
                    }
                }
            }

            // Clear all unvisited land pixels (floating islands)
            for (int i = 0; i < fieldData.Length; i++)
            {
                if (fieldData[i] > 0.5f && !visited[i])
                {
                    fieldData[i] = 0f;
                }
            }

            visited.Dispose();
        }

        /// <summary>
        /// Parallelized erosion-based island removal
        /// Safer than Union-Find and still much faster than serial for large textures
        /// </summary>
        public static void FloodFillParallel(NativeArray<float> fieldData, int texWidth)
        {
            // First pass: mark all pixels by distance from center
            var distanceField = new NativeArray<float>(texWidth * texWidth, Allocator.TempJob);
            var markJob = new DistanceMarkJob
            {
                FieldData = fieldData,
                DistanceField = distanceField,
                TexWidth = texWidth,
                CenterX = texWidth / 2f,
                CenterY = texWidth / 2f
            };
            var markHandle = markJob.Schedule(texWidth * texWidth, 64);
            markHandle.Complete();

            // Find the main landmass center (closest land pixel to center)
            float2 mainCenter = FindMainLandmassCenter(fieldData, distanceField, texWidth);

            // Multiple erosion passes to remove disconnected islands
            var tempBuffer = new NativeArray<float>(texWidth * texWidth, Allocator.TempJob);
            
            for (int pass = 0; pass < 3; pass++) // 3 erosion passes should be enough
            {
                var erosionJob = new IslandErosionJob
                {
                    InputData = fieldData,
                    OutputData = tempBuffer,
                    TexWidth = texWidth,
                    MainCenterX = mainCenter.x,
                    MainCenterY = mainCenter.y,
                    MaxDistance = texWidth * 0.6f // Allow landmass to extend up to 60% of texture size
                };
                var erosionHandle = erosionJob.Schedule(texWidth * texWidth, 64);
                erosionHandle.Complete();

                // Copy back
                fieldData.CopyFrom(tempBuffer);
            }

            distanceField.Dispose();
            tempBuffer.Dispose();
        }

        private static float2 FindMainLandmassCenter(NativeArray<float> fieldData, NativeArray<float> distanceField, int texWidth)
        {
            float centerX = texWidth / 2f;
            float centerY = texWidth / 2f;
            float bestDistance = float.MaxValue;
            float2 bestCenter = new float2(centerX, centerY);

            // Find the land pixel closest to the texture center
            for (int i = 0; i < fieldData.Length; i++)
            {
                if (fieldData[i] > 0.5f && distanceField[i] < bestDistance)
                {
                    bestDistance = distanceField[i];
                    int x = i % texWidth;
                    int y = i / texWidth;
                    bestCenter = new float2(x, y);
                }
            }

            return bestCenter;
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct DistanceMarkJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> FieldData;
            [WriteOnly] public NativeArray<float> DistanceField;
            [ReadOnly] public int TexWidth;
            [ReadOnly] public float CenterX;
            [ReadOnly] public float CenterY;

            public void Execute(int index)
            {
                if (FieldData[index] > 0.5f)
                {
                    int x = index % TexWidth;
                    int y = index / TexWidth;
                    float distance = math.distance(new float2(x, y), new float2(CenterX, CenterY));
                    DistanceField[index] = distance;
                }
                else
                {
                    DistanceField[index] = float.MaxValue;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct IslandErosionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> InputData;
            [WriteOnly] public NativeArray<float> OutputData;
            [ReadOnly] public int TexWidth;
            [ReadOnly] public float MainCenterX;
            [ReadOnly] public float MainCenterY;
            [ReadOnly] public float MaxDistance;

            public void Execute(int index)
            {
                if (InputData[index] < 0.5f)
                {
                    OutputData[index] = 0f;
                    return;
                }

                int x = index % TexWidth;
                int y = index / TexWidth;

                // Distance from main landmass center
                float distFromMain = math.distance(new float2(x, y), new float2(MainCenterX, MainCenterY));
                
                // If too far from main center, remove it
                if (distFromMain > MaxDistance)
                {
                    OutputData[index] = 0f;
                    return;
                }

                // Count connected land neighbors
                int landNeighbors = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        
                        int nx = x + dx;
                        int ny = y + dy;
                        
                        if (nx >= 0 && nx < TexWidth && ny >= 0 && ny < TexWidth)
                        {
                            int neighborIndex = ny * TexWidth + nx;
                            if (InputData[neighborIndex] > 0.5f)
                                landNeighbors++;
                        }
                    }
                }

                // Keep pixel if it has enough neighbors (prevents isolated pixels)
                // Also bias towards keeping pixels closer to the main center
                float bias = 1f - (distFromMain / MaxDistance);
                int requiredNeighbors = (int)(3 - bias * 2); // 1-3 neighbors required depending on distance
                
                OutputData[index] = landNeighbors >= requiredNeighbors ? InputData[index] : 0f;
            }
        }
    }
}