using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


namespace PlanetGen.MarchingSquares

{
    public struct LineSegment

    {
        public float2 start;

        public float2 end;

        public int cellX; // Store cell coordinates for polyline connectivity

        public int cellY;


        public LineSegment(float2 start, float2 end, int cellX = -1, int cellY = -1)

        {
            this.start = start;

            this.end = end;

            this.cellX = cellX;

            this.cellY = cellY;
        }
    }


    /// <summary>
    /// Spatial hash key for efficient segment connectivity lookup during polyline generation
    /// </summary>
    public struct SpatialKey : IEquatable<SpatialKey>

    {
        public int x, y;


        public SpatialKey(float2 point, float resolution)

        {
            x = (int)(point.x * resolution);

            y = (int)(point.y * resolution);
        }


        public bool Equals(SpatialKey other) => x == other.x && y == other.y;

        public override int GetHashCode() => x ^ (y << 16);
    }


    public class CPUMarchingSquares : IDisposable

    {
        private NativeList<LineSegment> segments;

        private NativeArray<int> segmentCounts;

        private NativeArray<LineSegment> segmentBatches;


        public NativeArray<LineSegment> Segments => segments.AsArray();

        public int SegmentCount => segments.Length;


        public CPUMarchingSquares()

        {
            segments = new NativeList<LineSegment>(Allocator.Persistent);
        }


        /// <summary>
        /// Generates marching squares contours from a scalar field using parallel processing.
        /// Output coordinates are normalized from -1 to 1 for both x and y axes.
        /// </summary>
        /// <param name="scalarField">The input scalar field data</param>
        /// <param name="width">Width of the scalar field</param>
        /// <param name="threshold">Isoline threshold value (typically 0.5f)</param>
        /// <param name="batchSize">Number of rows to process per job (tune for performance)</param>
        public void GenerateContoursByPrecount(NativeArray<float> scalarField, int width, float threshold = 0.5f,
            int batchSize = 4)

        {
            int height = scalarField.Length / width;


// Clear previous results

            segments.Clear();


// Calculate number of batches needed

            int numBatches = Mathf.CeilToInt((float)(height - 1) / batchSize);


// Allocate temporary storage for batch results

            segmentCounts = new NativeArray<int>(numBatches, Allocator.TempJob);


// First pass: Count segments per batch

            var countJob = new CountSegmentsJob

            {
                scalarField = scalarField,

                width = width,

                height = height,

                threshold = threshold,

                batchSize = batchSize,

                segmentCounts = segmentCounts
            };


            JobHandle countHandle = countJob.Schedule(numBatches, 1);

            countHandle.Complete();


// Calculate total segments and offsets

            int totalSegments = 0;

            NativeArray<int> batchOffsets = new NativeArray<int>(numBatches, Allocator.TempJob);


            for (int i = 0; i < numBatches; i++)

            {
                batchOffsets[i] = totalSegments;

                totalSegments += segmentCounts[i];
            }


            if (totalSegments == 0)

            {
                segmentCounts.Dispose();

                batchOffsets.Dispose();

                return;
            }


// Allocate output array

            segmentBatches = new NativeArray<LineSegment>(totalSegments, Allocator.TempJob);


// Second pass: Generate actual segments

            var generateJob = new GenerateSegmentsJob

            {
                scalarField = scalarField,

                width = width,

                height = height,

                threshold = threshold,

                batchSize = batchSize,

                segmentCounts = segmentCounts,

                batchOffsets = batchOffsets,

                outputSegments = segmentBatches
            };


            JobHandle generateHandle = generateJob.Schedule(numBatches, 1);

            generateHandle.Complete();


// Copy results to persistent storage

            segments.Capacity = totalSegments;

            for (int i = 0; i < totalSegments; i++)

            {
                segments.Add(segmentBatches[i]);
            }


// Cleanup temporary arrays

            segmentCounts.Dispose();

            batchOffsets.Dispose();

            segmentBatches.Dispose();
        }


        /// <summary>
        /// Optimized version that processes the entire field in parallel chunks.
        /// Output coordinates are normalized from -1 to 1 for both x and y axes.
        /// </summary>
        public void GenerateContoursByCompaction(NativeArray<float> scalarField, int width, float threshold = 0.5f)

        {
            int height = scalarField.Length / width;

            int cellCount = (width - 1) * (height - 1);


// Clear previous results

            segments.Clear();


// Use a different approach: first collect all valid segments with their cell info

// Each cell gets exactly 2 slots regardless of whether it uses them

            NativeArray<LineSegment> tempSegments = new NativeArray<LineSegment>(cellCount * 2, Allocator.TempJob);

            NativeArray<bool> segmentValid = new NativeArray<bool>(cellCount * 2, Allocator.TempJob);


            var job = new OptimizedMarchingSquaresJob

            {
                scalarField = scalarField,

                width = width,

                height = height,

                threshold = threshold,

                outputSegments = tempSegments,

                segmentValid = segmentValid
            };


            JobHandle handle = job.Schedule(cellCount, 64); // Process 64 cells per batch

            handle.Complete();


// Compact the results - only add valid segments

            for (int i = 0; i < tempSegments.Length; i++)

            {
                if (segmentValid[i])

                {
                    segments.Add(tempSegments[i]);
                }
            }


            tempSegments.Dispose();

            segmentValid.Dispose();
        }


        /// <summary>
        /// Gets segments organized by spatial proximity for efficient polyline generation.
        /// Returns a parallel hash map where keys are spatial locations and values are segment indices.
        /// Note: Spatial resolution should be adjusted for -1 to 1 coordinate range.
        /// </summary>
        public NativeParallelMultiHashMap<SpatialKey, int> GetSpatialSegmentMap(float spatialResolution = 500.0f)

        {
            var spatialMap = new NativeParallelMultiHashMap<SpatialKey, int>(segments.Length * 2, Allocator.Temp);


            for (int i = 0; i < segments.Length; i++)

            {
                var segment = segments[i];


// Add both endpoints to spatial hash

                var startKey = new SpatialKey(segment.start, spatialResolution);

                var endKey = new SpatialKey(segment.end, spatialResolution);


                spatialMap.Add(startKey, i);

                spatialMap.Add(endKey, i);
            }


            return spatialMap;
        }


        public void Dispose()

        {
            if (segments.IsCreated)

                segments.Dispose();
        }
    }


    [BurstCompile(CompileSynchronously = true)]
    struct CountSegmentsJob : IJobParallelFor

    {
        [ReadOnly] public NativeArray<float> scalarField;

        [ReadOnly] public int width;

        [ReadOnly] public int height;

        [ReadOnly] public float threshold;

        [ReadOnly] public int batchSize;

        [WriteOnly] public NativeArray<int> segmentCounts;


        public void Execute(int batchIndex)

        {
            int startY = batchIndex * batchSize;

            int endY = math.min(startY + batchSize, height - 1);

            int count = 0;


            for (int y = startY; y < endY; y++)

            {
                for (int x = 0; x < width - 1; x++)

                {
                    count += CountCellSegments(x, y);
                }
            }


            segmentCounts[batchIndex] = count;
        }


        private int CountCellSegments(int x, int y)

        {
// Sample the four corners of the cell

            float tl = scalarField[y * width + x]; // top-left

            float tr = scalarField[y * width + (x + 1)]; // top-right

            float bl = scalarField[(y + 1) * width + x]; // bottom-left

            float br = scalarField[(y + 1) * width + (x + 1)]; // bottom-right


// Convert to binary configuration

            int config = 0;

            if (tl > threshold) config |= 1;

            if (tr > threshold) config |= 2;

            if (br > threshold) config |= 4;

            if (bl > threshold) config |= 8;


            return GetSegmentCountForConfig(config);
        }


        private static int GetSegmentCountForConfig(int config)

        {
// Returns number of line segments for each marching squares configuration

            switch (config)

            {
                case 0:
                case 15: return 0; // All same

                case 1:
                case 2:
                case 4:
                case 7:
                case 8:
                case 11:
                case 13:
                case 14: return 1; // Single line

                case 3:
                case 6:
                case 9:
                case 12: return 1; // Single line

                case 5:
                case 10: return 2; // Two lines (saddle configurations)

                default: return 0;
            }
        }
    }


    [BurstCompile(CompileSynchronously = true)]
    struct GenerateSegmentsJob : IJobParallelFor

    {
        [ReadOnly] public NativeArray<float> scalarField;

        [ReadOnly] public int width;

        [ReadOnly] public int height;

        [ReadOnly] public float threshold;

        [ReadOnly] public int batchSize;

        [ReadOnly] public NativeArray<int> segmentCounts;

        [ReadOnly] public NativeArray<int> batchOffsets;

        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<LineSegment> outputSegments;


        public void Execute(int batchIndex)

        {
            int startY = batchIndex * batchSize;

            int endY = math.min(startY + batchSize, height - 1);

            int segmentIndex = batchOffsets[batchIndex];


            for (int y = startY; y < endY; y++)

            {
                for (int x = 0; x < width - 1; x++)

                {
                    segmentIndex += ProcessCell(x, y, segmentIndex);
                }
            }
        }


        private int ProcessCell(int x, int y, int outputIndex)

        {
// Sample the four corners of the cell

            float tl = scalarField[y * width + x]; // top-left

            float tr = scalarField[y * width + (x + 1)]; // top-right

            float bl = scalarField[(y + 1) * width + x]; // bottom-left

            float br = scalarField[(y + 1) * width + (x + 1)]; // bottom-right


// Convert to binary configuration

            int config = 0;

            if (tl > threshold) config |= 1;

            if (tr > threshold) config |= 2;

            if (br > threshold) config |= 4;

            if (bl > threshold) config |= 8;


            return GenerateSegmentsForConfig(config, x, y, tl, tr, bl, br, outputIndex);
        }


        private int GenerateSegmentsForConfig(int config, int x, int y, float tl, float tr, float bl, float br,
            int outputIndex)

        {
// Cell coordinates normalized to [-1, 1] range

            float cellX = -1.0f + 2.0f * (float)x / (width - 1);

            float cellY = -1.0f + 2.0f * (float)y / (height - 1);

            float cellSize = 2.0f / (width - 1);


// Edge interpolation points

            float2 top = new float2(cellX + cellSize * Lerp(tl, tr), cellY);

            float2 right = new float2(cellX + cellSize, cellY + cellSize * Lerp(tr, br));

            float2 bottom = new float2(cellX + cellSize * Lerp(bl, br), cellY + cellSize);

            float2 left = new float2(cellX, cellY + cellSize * Lerp(tl, bl));


            int segmentCount = 0;


            switch (config)

            {
                case 1:
                case 14: // Bottom-left to left

                    outputSegments[outputIndex] = new LineSegment(bottom, left, x, y);

                    segmentCount = 1;

                    break;

                case 2:
                case 13: // Top to right

                    outputSegments[outputIndex] = new LineSegment(top, right, x, y);

                    segmentCount = 1;

                    break;

                case 3:
                case 12: // Left to right

                    outputSegments[outputIndex] = new LineSegment(left, right, x, y);

                    segmentCount = 1;

                    break;

                case 4:
                case 11: // Right to bottom

                    outputSegments[outputIndex] = new LineSegment(right, bottom, x, y);

                    segmentCount = 1;

                    break;

                case 5: // Two segments: top-right, bottom-left

                    outputSegments[outputIndex] = new LineSegment(top, right, x, y);

                    outputSegments[outputIndex + 1] = new LineSegment(bottom, left, x, y);

                    segmentCount = 2;

                    break;

                case 6:
                case 9: // Top to bottom

                    outputSegments[outputIndex] = new LineSegment(top, bottom, x, y);

                    segmentCount = 1;

                    break;

                case 7:
                case 8: // Left to top

                    outputSegments[outputIndex] = new LineSegment(left, top, x, y);

                    segmentCount = 1;

                    break;

                case 10: // Two segments: top-left, bottom-right

                    outputSegments[outputIndex] = new LineSegment(top, left, x, y);

                    outputSegments[outputIndex + 1] = new LineSegment(right, bottom, x, y);

                    segmentCount = 2;

                    break;

                default:

                    segmentCount = 0;

                    break;
            }


            return segmentCount;
        }


        private float Lerp(float a, float b)

        {
            if (math.abs(a - b) < 0.001f) return 0.5f;

            return (threshold - a) / (b - a);
        }
    }


    [BurstCompile(CompileSynchronously = true)]
    struct OptimizedMarchingSquaresJob : IJobParallelFor

    {
        [ReadOnly] public NativeArray<float> scalarField;

        [ReadOnly] public int width;

        [ReadOnly] public int height;

        [ReadOnly] public float threshold;

        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<LineSegment> outputSegments;

        [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<bool> segmentValid;


        public void Execute(int cellIndex)

        {
            int x = cellIndex % (width - 1);

            int y = cellIndex / (width - 1);


// Sample the four corners of the cell

            float tl = scalarField[y * width + x]; // top-left

            float tr = scalarField[y * width + (x + 1)]; // top-right

            float bl = scalarField[(y + 1) * width + x]; // bottom-left

            float br = scalarField[(y + 1) * width + (x + 1)]; // bottom-right


// Convert to binary configuration

            int config = 0;

            if (tl > threshold) config |= 1;

            if (tr > threshold) config |= 2;

            if (br > threshold) config |= 4;

            if (bl > threshold) config |= 8;


// Each cell has exactly 2 slots: cellIndex * 2 and cellIndex * 2 + 1

            int slot1 = cellIndex * 2;

            int slot2 = cellIndex * 2 + 1;


// Bounds check to ensure we don't go out of range

            if (slot1 >= outputSegments.Length || slot2 >= outputSegments.Length)

                return;


// Initialize as invalid

            segmentValid[slot1] = false;

            segmentValid[slot2] = false;


// Skip empty configurations

            if (config == 0 || config == 15) return;


// Cell coordinates normalized to [-1, 1] range

            float cellX = -1.0f + 2.0f * (float)x / (width - 1);

            float cellY = -1.0f + 2.0f * (float)y / (height - 1);

            float cellSize = 2.0f / (width - 1);


// Edge interpolation points

            float2 top = new float2(cellX + cellSize * Lerp(tl, tr), cellY);

            float2 right = new float2(cellX + cellSize, cellY + cellSize * Lerp(tr, br));

            float2 bottom = new float2(cellX + cellSize * Lerp(bl, br), cellY + cellSize);

            float2 left = new float2(cellX, cellY + cellSize * Lerp(tl, bl));


// Generate segments based on configuration

            LineSegment segment1, segment2;

            int segmentCount = GetSegmentsForConfig(config, top, right, bottom, left, x, y, out segment1, out segment2);


            if (segmentCount > 0)

            {
                outputSegments[slot1] = segment1;

                segmentValid[slot1] = true;


                if (segmentCount == 2)

                {
                    outputSegments[slot2] = segment2;

                    segmentValid[slot2] = true;
                }
            }
        }


        private static int GetSegmentsForConfig(int config, float2 top, float2 right, float2 bottom, float2 left,
            int cellX, int cellY, out LineSegment segment1, out LineSegment segment2)

        {
            segment2 = default;


            switch (config)

            {
                case 1:
                case 14:

                    segment1 = new LineSegment(bottom, left, cellX, cellY);

                    return 1;

                case 2:
                case 13:

                    segment1 = new LineSegment(top, right, cellX, cellY);

                    return 1;

                case 3:
                case 12:

                    segment1 = new LineSegment(left, right, cellX, cellY);

                    return 1;

                case 4:
                case 11:

                    segment1 = new LineSegment(right, bottom, cellX, cellY);

                    return 1;

                case 5:

                    segment1 = new LineSegment(top, right, cellX, cellY);

                    segment2 = new LineSegment(bottom, left, cellX, cellY);

                    return 2;

                case 6:
                case 9:

                    segment1 = new LineSegment(top, bottom, cellX, cellY);

                    return 1;

                case 7:
                case 8:

                    segment1 = new LineSegment(left, top, cellX, cellY);

                    return 1;

                case 10:

                    segment1 = new LineSegment(top, left, cellX, cellY);

                    segment2 = new LineSegment(right, bottom, cellX, cellY);

                    return 2;

                default:

                    segment1 = default;

                    return 0;
            }
        }


        private static float Lerp(float a, float b)

        {
            if (math.abs(a - b) < 0.001f) return 0.5f;

            return (0.5f - a) / (b - a); // Assuming threshold = 0.5f
        }
    }
}

