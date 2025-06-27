using System;
using UnityEngine;

namespace PlanetGen.Compute
{
    public class LocalUDFGenerator : IDisposable
    {
        private ComputeShader spatialHashShader;
        private int buildHashKernel;
        private int calculateUDFKernel;
        
        // Spatial hash buffers
        private ComputeBuffer gridCounter;
        private ComputeBuffer segmentIndices;
        
        // Grid parameters
        private int gridResolution = 64;          // 64x64 grid for spatial hashing
        private int maxSegmentsPerCell = 32;      // Max segments per grid cell
        private int textureResolution;
        
        // Debug info
        public int LastSegmentCount { get; private set; }
        public float LastMaxDistance { get; private set; }
        
        public LocalUDFGenerator()
        {
            // Load the compute shader
            spatialHashShader = Resources.Load<ComputeShader>("Compute/SpatialHashUDF");
            if (spatialHashShader == null)
            {
                Debug.LogError("Failed to load SpatialHashUDF compute shader from Resources folder");
                return;
            }
            
            // Get kernel indices
            buildHashKernel = spatialHashShader.FindKernel("BuildSpatialHash");
            calculateUDFKernel = spatialHashShader.FindKernel("CalculateLocalUDF");
            
            if (buildHashKernel < 0 || calculateUDFKernel < 0)
            {
                Debug.LogError("Failed to find required kernels in SpatialHashUDF compute shader");
            }
        }
        
        public void Init(int newTextureResolution)
        {
            textureResolution = newTextureResolution;
            
            // Calculate buffer sizes
            int totalCells = gridResolution * gridResolution;
            int totalIndices = totalCells * maxSegmentsPerCell;
            
            // Dispose old buffers
            gridCounter?.Dispose();
            segmentIndices?.Dispose();
            
            // Create new buffers
            gridCounter = new ComputeBuffer(totalCells, sizeof(uint));
            segmentIndices = new ComputeBuffer(totalIndices, sizeof(uint));
            
            // Set constant parameters on compute shader
            spatialHashShader.SetInt("_GridResolution", gridResolution);
            spatialHashShader.SetInt("_MaxSegmentsPerCell", maxSegmentsPerCell);
            spatialHashShader.SetInt("_TextureResolution", textureResolution);
            
            Debug.Log($"LocalUDFGenerator initialized: {gridResolution}x{gridResolution} grid, " +
                     $"{maxSegmentsPerCell} max segments/cell, {textureResolution}x{textureResolution} texture");
        }
        
        public void GenerateLocalUDF(ComputeBuffer segments, ComputeBuffer segmentCount, 
                                   RenderTexture outputUDF, float maxDistance)
        {
            if (spatialHashShader == null || gridCounter == null || segmentIndices == null)
            {
                Debug.LogError("LocalUDFGenerator not properly initialized");
                return;
            }
            
            LastMaxDistance = maxDistance;
            
            // Get segment count for debugging
            int[] segCountArray = new int[1];
            segmentCount.GetData(segCountArray);
            LastSegmentCount = segCountArray[0];
            
            if (LastSegmentCount == 0)
            {
                // No segments - clear the UDF texture
                ClearUDFTexture(outputUDF);
                return;
            }
            
            // PHASE 1: Build Spatial Hash
            BuildSpatialHash(segments, segmentCount, maxDistance);
            
            // PHASE 2: Calculate Local UDF
            CalculateUDF(segments, segmentCount, outputUDF, maxDistance);
        }
        
        private void BuildSpatialHash(ComputeBuffer segments, ComputeBuffer segmentCount, float maxDistance)
        {
            // Clear the grid counter buffer
            uint[] zeros = new uint[gridResolution * gridResolution];
            gridCounter.SetData(zeros);
            
            // Early exit if no segments
            if (LastSegmentCount == 0)
            {
                return;
            }
            
            // Set up compute shader for hash building
            spatialHashShader.SetBuffer(buildHashKernel, "_Segments", segments);
            spatialHashShader.SetBuffer(buildHashKernel, "_SegmentCount", segmentCount);
            spatialHashShader.SetBuffer(buildHashKernel, "_GridCounter", gridCounter);
            spatialHashShader.SetBuffer(buildHashKernel, "_SegmentIndices", segmentIndices);
            spatialHashShader.SetFloat("_MaxUDFDistance", maxDistance);
            
            // Dispatch - one thread per segment, ensure at least 1 thread group
            int threadGroups = Mathf.Max(1, Mathf.CeilToInt(LastSegmentCount / 64.0f));
            spatialHashShader.Dispatch(buildHashKernel, threadGroups, 1, 1);
        }
        
        private void CalculateUDF(ComputeBuffer segments, ComputeBuffer segmentCount, 
                                 RenderTexture outputUDF, float maxDistance)
        {
            // Set up compute shader for UDF calculation
            spatialHashShader.SetBuffer(calculateUDFKernel, "_Segments", segments);
            spatialHashShader.SetBuffer(calculateUDFKernel, "_SegmentCount", segmentCount);
            spatialHashShader.SetBuffer(calculateUDFKernel, "_GridCounter", gridCounter);
            spatialHashShader.SetBuffer(calculateUDFKernel, "_SegmentIndices", segmentIndices);
            spatialHashShader.SetTexture(calculateUDFKernel, "_LocalUDF", outputUDF);
            spatialHashShader.SetFloat("_MaxUDFDistance", maxDistance);
            
            // Always dispatch to ensure texture is properly initialized
            // 8x8 thread groups for texture
            int threadGroups = Mathf.Max(1, Mathf.CeilToInt(textureResolution / 8.0f));
            spatialHashShader.Dispatch(calculateUDFKernel, threadGroups, threadGroups, 1);
        }
        
        private void ClearUDFTexture(RenderTexture texture)
        {
            // Clear to invalid value (-1)
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, new Color(-1, 0, 0, 0));
            RenderTexture.active = previous;
        }
        
        // Debug method to check spatial hash efficiency
        public void GetHashStatistics(out float avgSegmentsPerCell, out int maxSegmentsInCell, out int occupiedCells)
        {
            if (gridCounter == null)
            {
                avgSegmentsPerCell = 0;
                maxSegmentsInCell = 0;
                occupiedCells = 0;
                return;
            }
            
            uint[] counters = new uint[gridResolution * gridResolution];
            gridCounter.GetData(counters);
            
            int totalSegments = 0;
            maxSegmentsInCell = 0;
            occupiedCells = 0;
            
            for (int i = 0; i < counters.Length; i++)
            {
                int count = (int)counters[i];
                if (count > 0)
                {
                    occupiedCells++;
                    totalSegments += count;
                    maxSegmentsInCell = Mathf.Max(maxSegmentsInCell, count);
                }
            }
            
            avgSegmentsPerCell = occupiedCells > 0 ? (float)totalSegments / occupiedCells : 0;
        }
        
        public void Dispose()
        {
            gridCounter?.Dispose();
            segmentIndices?.Dispose();
            gridCounter = null;
            segmentIndices = null;
        }
    }
}