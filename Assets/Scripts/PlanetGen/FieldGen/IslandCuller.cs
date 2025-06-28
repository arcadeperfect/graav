using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen.FieldGen
{
    public static class IslandCuller
    {
        /// <summary>
        /// Simple flood fill that keeps only the main landmass connected to the center
        /// </summary>
        public static void FloodFillSimple(NativeArray<float> fieldData, int texWidth)
        {
            // Create visited array
            var visited = new NativeArray<bool>(texWidth * texWidth, Allocator.Temp);
            var queue = new Queue<int2>();

            // Find starting point - either center if it's land, or closest land to center
            int centerX = texWidth / 2;
            int centerY = texWidth / 2;
            int startIndex = GetStartingPoint(fieldData, texWidth, centerX, centerY);

            if (startIndex == -1)
            {
                // Debug.Log("No land found - skipping island culling");
                visited.Dispose();
                return;
            }

            // Convert start index to coordinates
            int startX = startIndex % texWidth;
            int startY = startIndex / texWidth;
            
            // Debug.Log($"Starting flood fill from ({startX}, {startY})");

            // Initialize flood fill
            queue.Enqueue(new int2(startX, startY));
            visited[startIndex] = true;

            int connectedPixels = 0;

            // Flood fill using 4-connectivity
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                connectedPixels++;

                // Check 4 neighbors
                CheckAndAddNeighbor(current.x + 1, current.y, texWidth, fieldData, visited, queue);
                CheckAndAddNeighbor(current.x - 1, current.y, texWidth, fieldData, visited, queue);
                CheckAndAddNeighbor(current.x, current.y + 1, texWidth, fieldData, visited, queue);
                CheckAndAddNeighbor(current.x, current.y - 1, texWidth, fieldData, visited, queue);
            }

            // Debug.Log($"Main landmass contains {connectedPixels} pixels");

            // Remove all unvisited land pixels (these are the floating islands)
            int removedPixels = 0;
            for (int i = 0; i < fieldData.Length; i++)
            {
                if (fieldData[i] > 0.5f && !visited[i])
                {
                    fieldData[i] = 0f;
                    removedPixels++;
                }
            }

            // Debug.Log($"Removed {removedPixels} island pixels");
            visited.Dispose();
        }

        private static int GetStartingPoint(NativeArray<float> fieldData, int texWidth, int centerX, int centerY)
        {
            int centerIndex = centerY * texWidth + centerX;
            
            // If center is land, start there
            if (fieldData[centerIndex] > 0.5f)
            {
                return centerIndex;
            }

            // Otherwise find the closest land pixel to center
            float nearestDistSq = float.MaxValue;
            int nearestIndex = -1;

            for (int y = 0; y < texWidth; y++)
            {
                for (int x = 0; x < texWidth; x++)
                {
                    int index = y * texWidth + x;
                    if (fieldData[index] > 0.5f)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        float distSq = dx * dx + dy * dy;
                        
                        if (distSq < nearestDistSq)
                        {
                            nearestDistSq = distSq;
                            nearestIndex = index;
                        }
                    }
                }
            }

            return nearestIndex;
        }

        private static void CheckAndAddNeighbor(int x, int y, int texWidth, 
                                              NativeArray<float> fieldData, 
                                              NativeArray<bool> visited, 
                                              Queue<int2> queue)
        {
            // Check bounds
            if (x < 0 || x >= texWidth || y < 0 || y >= texWidth)
                return;

            int index = y * texWidth + x;
            
            // Check if it's land and not visited
            if (fieldData[index] > 0.5f && !visited[index])
            {
                visited[index] = true;
                queue.Enqueue(new int2(x, y));
            }
        }

        /// <summary>
        /// Alternative parallel version for very large textures (experimental)
        /// Uses a growing region approach instead of traditional flood fill
        /// </summary>
        public static void FloodFillParallel(NativeArray<float> fieldData, int texWidth)
        {
            // For small textures, just use the simple version
            if (texWidth <= 512)
            {
                FloodFillSimple(fieldData, texWidth);
                return;
            }

            Debug.Log("Using parallel flood fill for large texture");
            
            // Create a connectivity map
            var connected = new NativeArray<bool>(texWidth * texWidth, Allocator.TempJob);
            
            // Find starting point
            int centerX = texWidth / 2;
            int centerY = texWidth / 2;
            int startIndex = GetStartingPoint(fieldData, texWidth, centerX, centerY);
            
            if (startIndex == -1)
            {
                connected.Dispose();
                return;
            }

            // Mark starting point as connected
            connected[startIndex] = true;
            
            // Iteratively grow the connected region
            bool changed = true;
            int iterations = 0;
            
            while (changed && iterations < texWidth) // Safety limit
            {
                changed = false;
                
                for (int i = 0; i < fieldData.Length; i++)
                {
                    if (fieldData[i] > 0.5f && !connected[i])
                    {
                        // Check if this pixel is adjacent to any connected pixel
                        int x = i % texWidth;
                        int y = i / texWidth;
                        
                        if (HasConnectedNeighbor(x, y, texWidth, connected))
                        {
                            connected[i] = true;
                            changed = true;
                        }
                    }
                }
                iterations++;
            }
            
            Debug.Log($"Parallel flood fill completed in {iterations} iterations");

            // Remove unconnected land
            int removedPixels = 0;
            for (int i = 0; i < fieldData.Length; i++)
            {
                if (fieldData[i] > 0.5f && !connected[i])
                {
                    fieldData[i] = 0f;
                    removedPixels++;
                }
            }

            Debug.Log($"Removed {removedPixels} island pixels");
            connected.Dispose();
        }

        private static bool HasConnectedNeighbor(int x, int y, int texWidth, NativeArray<bool> connected)
        {
            // Check 4 neighbors
            if (x > 0 && connected[(y * texWidth) + (x - 1)]) return true;
            if (x < texWidth - 1 && connected[(y * texWidth) + (x + 1)]) return true;
            if (y > 0 && connected[((y - 1) * texWidth) + x]) return true;
            if (y < texWidth - 1 && connected[((y + 1) * texWidth) + x]) return true;
            
            return false;
        }
    }
}