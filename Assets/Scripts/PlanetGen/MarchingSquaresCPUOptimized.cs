using System;
using PlanetGen.FieldGen2.Types;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace PlanetGen.Compute
{
    public static class MarchingSquaresCPUOptimized
    {
        public struct PolylineData : IDisposable
        {
            public NativeList<float2> AllPoints;
            public NativeList<int2> PolylineRanges;

            public PolylineData(Allocator allocator)
            {
                AllPoints = new NativeList<float2>(allocator);
                PolylineRanges = new NativeList<int2>(allocator);
            }

            public void Dispose()
            {
                if (AllPoints.IsCreated) AllPoints.Dispose();
                if (PolylineRanges.IsCreated) PolylineRanges.Dispose();
            }
        }

        public struct ColliderData
        {
            public int StartIndex;
            public int PointCount;
            public bool IsClosed;
        }

        /// <summary>
        /// Generates line segments from scalar field data using a Burst-compiled parallel job.
        /// Updated to use optimized version directly.
        /// </summary>
        public static NativeList<float4> GenerateSegmentsBurst(DeformableFieldData fieldData, float isoValue)
        {
            int width = fieldData.Size;
            int cellCount = (width - 1) * (width - 1);
            var segments = new NativeList<float4>(cellCount / 2, Allocator.TempJob);

            var job = new MarchingSquaresJob
            {
                ScalarField = fieldData.ModifiedScalarField,
                Width = width,
                IsoValue = isoValue,
                Segments = segments.AsParallelWriter()
            };

            JobHandle handle = job.Schedule(cellCount, 64);
            handle.Complete();

            return segments;
        }

        /// <summary>
        /// FAST: Extract polylines only - O(n log n) complexity
        /// Use this for immediate polyline needs
        /// </summary>
        public static PolylineData ExtractPolylinesFast(NativeList<float4> segments, Allocator allocator)
        {
            var polylineData = new PolylineData(allocator);
            if (segments.Length == 0) return polylineData;

            var job = new FastPolylineExtractionJob
            {
                Segments = segments,
                Epsilon = 0.0001f,
                AllPoints = polylineData.AllPoints,
                PolylineRanges = polylineData.PolylineRanges
            };

            JobHandle handle = job.Schedule();
            handle.Complete();

            return polylineData;
        }

        /// <summary>
        /// ASYNC: Generate collider data from existing polylines over multiple frames
        /// Call this after ExtractPolylinesFast() when you need colliders
        /// </summary>
        public static JobHandle GenerateCollidersAsync(PolylineData polylineData,
            NativeList<ColliderData> colliderData, int minPointsForCollider)
        {
            if (!polylineData.AllPoints.IsCreated || polylineData.PolylineRanges.Length == 0)
                return default;

            var job = new AsyncColliderGenerationJob
            {
                AllPoints = polylineData.AllPoints,
                PolylineRanges = polylineData.PolylineRanges,
                ColliderData = colliderData,
                MinPointsForCollider = minPointsForCollider,
                Epsilon = 0.0001f
            };

            return job.Schedule();
        }

        /// <summary>
        /// STREAMING: Process colliders in batches over multiple frames
        /// </summary>
        public struct StreamingColliderProcessor : IDisposable
        {
            private NativeArray<bool> processedPolylines;
            private int currentIndex;
            private int batchSize;

            public bool IsComplete => currentIndex >= processedPolylines.Length;

            public float Progress =>
                processedPolylines.Length > 0 ? (float)currentIndex / processedPolylines.Length : 1f;

            public StreamingColliderProcessor(int polylineCount, int batchSize = 10,
                Allocator allocator = Allocator.Persistent)
            {
                this.batchSize = batchSize;
                currentIndex = 0;
                processedPolylines = new NativeArray<bool>(polylineCount, allocator, NativeArrayOptions.ClearMemory);
            }

            // FIXED: Properly handle job dependencies
            public JobHandle ProcessBatch(PolylineData polylineData, NativeList<ColliderData> colliderData,
                int minPointsForCollider, JobHandle dependency = default)
            {
                if (IsComplete) return dependency;

                int endIndex = math.min(currentIndex + batchSize, processedPolylines.Length);

                var job = new BatchColliderGenerationJob
                {
                    AllPoints = polylineData.AllPoints,
                    PolylineRanges = polylineData.PolylineRanges,
                    ColliderData = colliderData,
                    MinPointsForCollider = minPointsForCollider,
                    Epsilon = 0.0001f,
                    StartIndex = currentIndex,
                    EndIndex = endIndex
                };

                currentIndex = endIndex;

                // CRITICAL FIX: Pass the dependency parameter to Schedule
                return job.Schedule(dependency);
            }

            public void Reset()
            {
                currentIndex = 0;
            }

            public void Dispose()
            {
                if (processedPolylines.IsCreated)
                    processedPolylines.Dispose();
            }
        }

        // OPTIMIZED: O(n log n) polyline extraction using spatial hashing
        [BurstCompile(CompileSynchronously = true)]
        private struct FastPolylineExtractionJob : IJob
        {
            [ReadOnly] public NativeList<float4> Segments;
            [ReadOnly] public float Epsilon;

            public NativeList<float2> AllPoints;
            public NativeList<int2> PolylineRanges;

            private bool PointsEqual(float2 a, float2 b)
            {
                return math.distancesq(a, b) < Epsilon * Epsilon;
            }

            public void Execute()
            {
                if (Segments.Length == 0) return;

                // Build adjacency lists for faster lookups - O(n) instead of O(n²)
                var pointToSegments = new NativeParallelMultiHashMap<int, int>(Segments.Length * 2, Allocator.Temp);

                // Hash points to grid cells for faster spatial lookups
                const float gridSize = 0.01f; // Adjust based on your coordinate system

                for (int i = 0; i < Segments.Length; i++)
                {
                    var segment = Segments[i];
                    int hash1 = HashPoint(segment.xy, gridSize);
                    int hash2 = HashPoint(segment.zw, gridSize);

                    pointToSegments.Add(hash1, i);
                    pointToSegments.Add(hash2, i);
                }

                var usedSegments =
                    new NativeArray<bool>(Segments.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
                var currentPolyline = new NativeList<float2>(128, Allocator.Temp); // Pre-allocate reasonable size

                for (int startIdx = 0; startIdx < Segments.Length; startIdx++)
                {
                    if (usedSegments[startIdx]) continue;

                    currentPolyline.Clear();
                    usedSegments[startIdx] = true;

                    var startSegment = Segments[startIdx];
                    currentPolyline.Add(startSegment.xy);
                    currentPolyline.Add(startSegment.zw);

                    // Extend forward and backward using spatial hash lookups
                    ExtendPolyline(currentPolyline, usedSegments, pointToSegments, gridSize, true); // Forward
                    ExtendPolyline(currentPolyline, usedSegments, pointToSegments, gridSize, false); // Backward

                    if (currentPolyline.Length > 1)
                    {
                        PolylineRanges.Add(new int2(AllPoints.Length, currentPolyline.Length));
                        AllPoints.AddRange(currentPolyline.AsArray());
                    }
                }

                pointToSegments.Dispose();
                usedSegments.Dispose();
                currentPolyline.Dispose();
            }

            private void ExtendPolyline(NativeList<float2> polyline, NativeArray<bool> usedSegments,
                NativeParallelMultiHashMap<int, int> pointToSegments, float gridSize, bool forward)
            {
                bool foundConnection = true;
                while (foundConnection)
                {
                    foundConnection = false;
                    float2 searchPoint = forward ? polyline[polyline.Length - 1] : polyline[0];
                    int hash = HashPoint(searchPoint, gridSize);

                    // Check nearby grid cells for potential connections
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nearbyHash = hash + dx + dy * 1000; // Simple hash offset

                            if (pointToSegments.TryGetFirstValue(nearbyHash, out int segmentIdx, out var iterator))
                            {
                                do
                                {
                                    if (usedSegments[segmentIdx]) continue;

                                    var segment = Segments[segmentIdx];
                                    float2 connectPoint = default;
                                    bool canConnect = false;

                                    if (PointsEqual(searchPoint, segment.xy))
                                    {
                                        connectPoint = segment.zw;
                                        canConnect = true;
                                    }
                                    else if (PointsEqual(searchPoint, segment.zw))
                                    {
                                        connectPoint = segment.xy;
                                        canConnect = true;
                                    }

                                    if (canConnect)
                                    {
                                        if (forward)
                                        {
                                            polyline.Add(connectPoint);
                                        }
                                        else
                                        {
                                            polyline.InsertRange(0, 1);
                                            polyline[0] = connectPoint;
                                        }

                                        usedSegments[segmentIdx] = true;
                                        foundConnection = true;
                                        goto NextExtension; // Break out of nested loops
                                    }
                                } while (pointToSegments.TryGetNextValue(out segmentIdx, ref iterator));
                            }
                        }
                    }

                    NextExtension: ;
                }
            }

            private int HashPoint(float2 point, float gridSize)
            {
                int x = (int)math.floor(point.x / gridSize);
                int y = (int)math.floor(point.y / gridSize);
                return x + y * 1000; // Simple hash function
            }
        }

        // ASYNC: Generate colliders without blocking
        [BurstCompile(CompileSynchronously = true)]
        private struct AsyncColliderGenerationJob : IJob
        {
            [ReadOnly] public NativeList<float2> AllPoints;
            [ReadOnly] public NativeList<int2> PolylineRanges;
            [ReadOnly] public int MinPointsForCollider;
            [ReadOnly] public float Epsilon;

            public NativeList<ColliderData> ColliderData;

            private bool PointsEqual(float2 a, float2 b)
            {
                return math.distancesq(a, b) < Epsilon * Epsilon;
            }

            public void Execute()
            {
                for (int i = 0; i < PolylineRanges.Length; i++)
                {
                    var range = PolylineRanges[i];
                    if (range.y >= MinPointsForCollider)
                    {
                        // Check if it's a closed loop
                        float2 firstPoint = AllPoints[range.x];
                        float2 lastPoint = AllPoints[range.x + range.y - 1];
                        bool isClosed = PointsEqual(firstPoint, lastPoint);

                        ColliderData.Add(new ColliderData
                        {
                            StartIndex = range.x,
                            PointCount = range.y,
                            IsClosed = isClosed
                        });
                    }
                }
            }
        }

        // STREAMING: Process colliders in batches
        [BurstCompile(CompileSynchronously = true)]
        private struct BatchColliderGenerationJob : IJob
        {
            [ReadOnly] public NativeList<float2> AllPoints;
            [ReadOnly] public NativeList<int2> PolylineRanges;
            [ReadOnly] public int MinPointsForCollider;
            [ReadOnly] public float Epsilon;
            [ReadOnly] public int StartIndex;
            [ReadOnly] public int EndIndex;

            public NativeList<ColliderData> ColliderData;

            private bool PointsEqual(float2 a, float2 b)
            {
                return math.distancesq(a, b) < Epsilon * Epsilon;
            }

            public void Execute()
            {
                for (int i = StartIndex; i < EndIndex && i < PolylineRanges.Length; i++)
                {
                    var range = PolylineRanges[i];
                    if (range.y >= MinPointsForCollider)
                    {
                        float2 firstPoint = AllPoints[range.x];
                        float2 lastPoint = AllPoints[range.x + range.y - 1];
                        bool isClosed = PointsEqual(firstPoint, lastPoint);

                        ColliderData.Add(new ColliderData
                        {
                            StartIndex = range.x,
                            PointCount = range.y,
                            IsClosed = isClosed
                        });
                    }
                }
            }
        }

        // Marching Squares Job - exposed for direct use
        [BurstCompile(CompileSynchronously = true)]
        public struct MarchingSquaresJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> ScalarField;
            [ReadOnly] public int Width;
            [ReadOnly] public float IsoValue;
            public NativeList<float4>.ParallelWriter Segments;

            private float2 InterpolateEdge(float2 p1, float2 p2, float v1, float v2)
            {
                float t = (IsoValue - v1) / (v2 - v1);
                return math.lerp(p1, p2, t);
            }

            private float2 TextureToNormalized(float2 texCoord)
            {
                return (texCoord / new float2(Width, Width)) * 2.0f - 1.0f;
            }

            public void Execute(int index)
            {
                int x = index % (Width - 1);
                int y = index / (Width - 1);

                float v00 = ScalarField[y * Width + x];
                float v10 = ScalarField[y * Width + (x + 1)];
                float v11 = ScalarField[(y + 1) * Width + (x + 1)];
                float v01 = ScalarField[(y + 1) * Width + x];

                int caseIndex = 0;
                if (v00 > IsoValue) caseIndex |= 1;
                if (v10 > IsoValue) caseIndex |= 2;
                if (v11 > IsoValue) caseIndex |= 4;
                if (v01 > IsoValue) caseIndex |= 8;

                if (caseIndex == 0 || caseIndex == 15) return;

                float2 edge0_p = InterpolateEdge(new float2(x, y), new float2(x + 1, y), v00, v10);
                float2 edge1_p = InterpolateEdge(new float2(x + 1, y), new float2(x + 1, y + 1), v10, v11);
                float2 edge2_p = InterpolateEdge(new float2(x, y + 1), new float2(x + 1, y + 1), v01, v11);
                float2 edge3_p = InterpolateEdge(new float2(x, y), new float2(x, y + 1), v00, v01);

                float2 edge0 = TextureToNormalized(edge0_p);
                float2 edge1 = TextureToNormalized(edge1_p);
                float2 edge2 = TextureToNormalized(edge2_p);
                float2 edge3 = TextureToNormalized(edge3_p);

                switch (caseIndex)
                {
                    case 1:
                    case 14: Segments.AddNoResize(new float4(edge3, edge0)); break;
                    case 2:
                    case 13: Segments.AddNoResize(new float4(edge0, edge1)); break;
                    case 3:
                    case 12: Segments.AddNoResize(new float4(edge3, edge1)); break;
                    case 4:
                    case 11: Segments.AddNoResize(new float4(edge1, edge2)); break;
                    case 6:
                    case 9: Segments.AddNoResize(new float4(edge0, edge2)); break;
                    case 7:
                    case 8: Segments.AddNoResize(new float4(edge2, edge3)); break;
                    case 5:
                        Segments.AddNoResize(new float4(edge3, edge0));
                        Segments.AddNoResize(new float4(edge1, edge2));
                        break;
                    case 10:
                        Segments.AddNoResize(new float4(edge0, edge1));
                        Segments.AddNoResize(new float4(edge2, edge3));
                        break;
                }
            }
        }
    }
}