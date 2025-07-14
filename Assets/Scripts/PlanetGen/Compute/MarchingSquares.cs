using System;
using PlanetGen.FieldGen2.Types;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PlanetGen.Compute
{
    /// <summary>
    /// Provides a highly optimized, parallelized, and Burst-compiled CPU implementation
    /// of the Marching Squares algorithm. This version is designed for high performance
    /// and is suitable for being called every frame.
    /// </summary>
    public static class MarchingSquaresCPU
    {
        // A struct to hold the results of polyline extraction.
        public struct PolylineData : IDisposable
        {
            // A single list containing all points from all polylines, concatenated.
            public NativeList<float2> AllPoints;
            // A list where each int2 represents a polyline.
            // .x is the start index in AllPoints, .y is the number of points.
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

        /// <summary>
        /// Generates line segments from scalar field data using a Burst-compiled parallel job.
        /// </summary>
        public static NativeList<float4> GenerateSegmentsBurst(FieldData fieldData, float isoValue, Allocator allocator)
        {
            int width = fieldData.Size;
            int cellCount = (width - 1) * (width - 1);
            // More conservative capacity estimation to avoid frequent resizing
            var segments = new NativeList<float4>(cellCount / 2, allocator);

            var job = new MarchingSquaresJob
            {
                // ScalarField = fieldData.ScalarFieldArray,
                ScalarField = fieldData.BaseRasterData.Scalar,
                Width = width,
                IsoValue = isoValue,
                Segments = segments.AsParallelWriter()
            };

            JobHandle handle = job.Schedule(cellCount, 64);
            handle.Complete();

            return segments;
        }

        /// <summary>
        /// Extracts connected polylines from an unordered list of segments.
        /// Improved version with better connectivity detection.
        /// </summary>
        public static PolylineData ExtractPolylinesBurst(NativeList<float4> segments, Allocator allocator)
        {
            var polylineData = new PolylineData(allocator);
            if (segments.Length == 0) return polylineData;
            
            var job = new ImprovedPolylineExtractionJob
            {
                Segments = segments,
                Epsilon = 0.0001f, // Small epsilon for floating point comparison
                AllPoints = polylineData.AllPoints,
                PolylineRanges = polylineData.PolylineRanges
            };
            
            JobHandle handle = job.Schedule();
            handle.Complete();
            
            return polylineData;
        }

        /// <summary>
        /// Extracts polylines and generates collider data in a single pass.
        /// Much more efficient for real-time collider generation.
        /// </summary>
        public static PolylineData ExtractPolylinesWithColliders(NativeList<float4> segments, 
            out NativeList<ColliderData> colliderData, int minPointsForCollider, Allocator allocator)
        {
            var polylineData = new PolylineData(allocator);
            colliderData = new NativeList<ColliderData>(allocator);
            
            if (segments.Length == 0) return polylineData;
            
            var job = new PolylineWithCollidersJob
            {
                Segments = segments,
                Epsilon = 0.0001f,
                MinPointsForCollider = minPointsForCollider,
                AllPoints = polylineData.AllPoints,
                PolylineRanges = polylineData.PolylineRanges,
                ColliderData = colliderData
            };
            
            JobHandle handle = job.Schedule();
            handle.Complete();
            
            return polylineData;
        }

        public struct ColliderData
        {
            public int StartIndex;     // Index into AllPoints where this collider's points start
            public int PointCount;     // Number of points for this collider
            public bool IsClosed;      // Whether this forms a closed loop
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct MarchingSquaresJob : IJobParallelFor
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
                    case 1: case 14: Segments.AddNoResize(new float4(edge3, edge0)); break;
                    case 2: case 13: Segments.AddNoResize(new float4(edge0, edge1)); break;
                    case 3: case 12: Segments.AddNoResize(new float4(edge3, edge1)); break;
                    case 4: case 11: Segments.AddNoResize(new float4(edge1, edge2)); break;
                    case 6: case 9:  Segments.AddNoResize(new float4(edge0, edge2)); break;
                    case 7: case 8:  Segments.AddNoResize(new float4(edge2, edge3)); break;
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

        [BurstCompile(CompileSynchronously = true)]
        private struct PolylineWithCollidersJob : IJob
        {
            [ReadOnly] public NativeList<float4> Segments;
            [ReadOnly] public float Epsilon;
            [ReadOnly] public int MinPointsForCollider;

            public NativeList<float2> AllPoints;
            public NativeList<int2> PolylineRanges;
            public NativeList<ColliderData> ColliderData;

            private bool PointsEqual(float2 a, float2 b)
            {
                return math.distancesq(a, b) < Epsilon * Epsilon;
            }

            public void Execute()
            {
                if (Segments.Length == 0) return;

                var usedSegments = new NativeArray<bool>(Segments.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
                var currentPolyline = new NativeList<float2>(Allocator.Temp);

                // Build polylines by connecting segments
                for (int startIdx = 0; startIdx < Segments.Length; startIdx++)
                {
                    if (usedSegments[startIdx]) continue;

                    currentPolyline.Clear();
                    usedSegments[startIdx] = true;

                    var startSegment = Segments[startIdx];
                    currentPolyline.Add(startSegment.xy);
                    currentPolyline.Add(startSegment.zw);

                    // Try to extend the polyline in both directions
                    bool foundConnection = true;
                    while (foundConnection)
                    {
                        foundConnection = false;

                        // Try to extend forward (from the end)
                        float2 endPoint = currentPolyline[currentPolyline.Length - 1];
                        for (int i = 0; i < Segments.Length; i++)
                        {
                            if (usedSegments[i]) continue;

                            var segment = Segments[i];
                            if (PointsEqual(endPoint, segment.xy))
                            {
                                currentPolyline.Add(segment.zw);
                                usedSegments[i] = true;
                                foundConnection = true;
                                break;
                            }
                            else if (PointsEqual(endPoint, segment.zw))
                            {
                                currentPolyline.Add(segment.xy);
                                usedSegments[i] = true;
                                foundConnection = true;
                                break;
                            }
                        }

                        if (foundConnection) continue;

                        // Try to extend backward (from the start)
                        float2 startPoint = currentPolyline[0];
                        for (int i = 0; i < Segments.Length; i++)
                        {
                            if (usedSegments[i]) continue;

                            var segment = Segments[i];
                            if (PointsEqual(startPoint, segment.zw))
                            {
                                currentPolyline.InsertRange(0, 1);
                                currentPolyline[0] = segment.xy;
                                usedSegments[i] = true;
                                foundConnection = true;
                                break;
                            }
                            else if (PointsEqual(startPoint, segment.xy))
                            {
                                currentPolyline.InsertRange(0, 1);
                                currentPolyline[0] = segment.zw;
                                usedSegments[i] = true;
                                foundConnection = true;
                                break;
                            }
                        }
                    }

                    // Store the completed polyline if it has more than 1 point
                    if (currentPolyline.Length > 1)
                    {
                        int startIndex = AllPoints.Length;
                        PolylineRanges.Add(new int2(startIndex, currentPolyline.Length));
                        AllPoints.AddRange(currentPolyline.AsArray());

                        // Generate collider data if polyline is long enough
                        if (currentPolyline.Length >= MinPointsForCollider)
                        {
                            // Check if it's a closed loop
                            bool isClosed = PointsEqual(currentPolyline[0], currentPolyline[currentPolyline.Length - 1]);

                            ColliderData.Add(new ColliderData
                            {
                                StartIndex = startIndex,
                                PointCount = currentPolyline.Length,
                                IsClosed = isClosed
                            });
                        }
                    }
                }

                usedSegments.Dispose();
                currentPolyline.Dispose();
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct ImprovedPolylineExtractionJob : IJob
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

                var usedSegments = new NativeArray<bool>(Segments.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
                var currentPolyline = new NativeList<float2>(Allocator.Temp);

                for (int startIdx = 0; startIdx < Segments.Length; startIdx++)
                {
                    if (usedSegments[startIdx]) continue;

                    currentPolyline.Clear();
                    usedSegments[startIdx] = true;

                    var startSegment = Segments[startIdx];
                    currentPolyline.Add(startSegment.xy);
                    currentPolyline.Add(startSegment.zw);

                    bool foundConnection = true;
                    while (foundConnection)
                    {
                        foundConnection = false;

                        float2 endPoint = currentPolyline[currentPolyline.Length - 1];
                        for (int i = 0; i < Segments.Length; i++)
                        {
                            if (usedSegments[i]) continue;

                            var segment = Segments[i];
                            if (PointsEqual(endPoint, segment.xy))
                            {
                                currentPolyline.Add(segment.zw);
                                usedSegments[i] = true;
                                foundConnection = true;
                                break;
                            }
                            else if (PointsEqual(endPoint, segment.zw))
                            {
                                currentPolyline.Add(segment.xy);
                                usedSegments[i] = true;
                                foundConnection = true;
                                break;
                            }
                        }

                        if (foundConnection) continue;

                        float2 startPoint = currentPolyline[0];
                        for (int i = 0; i < Segments.Length; i++)
                        {
                            if (usedSegments[i]) continue;

                            var segment = Segments[i];
                            if (PointsEqual(startPoint, segment.zw))
                            {
                                currentPolyline.InsertRange(0, 1);
                                currentPolyline[0] = segment.xy;
                                usedSegments[i] = true;
                                foundConnection = true;
                                break;
                            }
                            else if (PointsEqual(startPoint, segment.xy))
                            {
                                currentPolyline.InsertRange(0, 1);
                                currentPolyline[0] = segment.zw;
                                usedSegments[i] = true;
                                foundConnection = true;
                                break;
                            }
                        }
                    }

                    if (currentPolyline.Length > 1)
                    {
                        PolylineRanges.Add(new int2(AllPoints.Length, currentPolyline.Length));
                        AllPoints.AddRange(currentPolyline.AsArray());
                    }
                }

                usedSegments.Dispose();
                currentPolyline.Dispose();
            }
        }
    }
}