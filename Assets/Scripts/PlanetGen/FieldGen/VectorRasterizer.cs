using PlanetGen.FieldGen2.Graph.Types;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace PlanetGen.FieldGen2.Graph
{
    [BurstCompile(CompileSynchronously = true)]
    public struct VectorRasterizeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> Vertices;
        [ReadOnly] public int vertexCount;
        [ReadOnly] public int textureSize;
        [ReadOnly] public float worldSize; // Size of the world space that maps to the texture

        [WriteOnly] public NativeArray<float> ScalarOutput;
        [WriteOnly] public NativeArray<float> AltitudeOutput;
        [WriteOnly] public NativeArray<float4> ColorOutput;
        [WriteOnly] public NativeArray<float> AngleOutput;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;

            // Convert pixel to world coordinates
            float worldX = ((x / (float)(textureSize - 1)) * 2f - 1f) * worldSize;
            float worldY = ((y / (float)(textureSize - 1)) * 2f - 1f) * worldSize;
            float2 worldPos = new float2(worldX, worldY);

            // Test if point is inside the polygon
            bool isInside = IsPointInsidePolygon(worldPos, Vertices, vertexCount);

            // Calculate distance to nearest edge
            float distanceToEdge = DistanceToNearestEdge(worldPos, Vertices, vertexCount);

            // Calculate angle from center to point
            float angle = math.atan2(worldY, worldX);

            // Generate outputs based on whether point is inside
            if (isInside)
            {
                ScalarOutput[index] = 1f;
                AltitudeOutput[index] = math.saturate(1f - (distanceToEdge * 10f)); // Falloff from edge
                ColorOutput[index] = new float4(0.2f, 0.8f, 0.3f, 1f); // Green for land
            }
            else
            {
                ScalarOutput[index] = 0f;
                AltitudeOutput[index] = 0f;
                ColorOutput[index] = new float4(0.1f, 0.3f, 0.8f, 1f); // Blue for water
            }

            AngleOutput[index] = angle;
        }

        // Ray casting algorithm for point-in-polygon test
        private bool IsPointInsidePolygon(float2 point, NativeArray<float2> vertices, int count)
        {
            bool inside = false;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float2 vi = vertices[i];
                float2 vj = vertices[j];

                if (((vi.y > point.y) != (vj.y > point.y)) &&
                    (point.x < (vj.x - vi.x) * (point.y - vi.y) / (vj.y - vi.y) + vi.x))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        // Calculate shortest distance from point to any edge of the polygon
        private float DistanceToNearestEdge(float2 point, NativeArray<float2> vertices, int count)
        {
            float minDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                int nextIndex = (i + 1) % count;
                float2 edgeStart = vertices[i];
                float2 edgeEnd = vertices[nextIndex];

                float distance = DistancePointToLineSegment(point, edgeStart, edgeEnd);
                minDistance = math.min(minDistance, distance);
            }

            return minDistance;
        }

        // Calculate distance from point to line segment
        private float DistancePointToLineSegment(float2 point, float2 lineStart, float2 lineEnd)
        {
            float2 lineVec = lineEnd - lineStart;
            float2 pointVec = point - lineStart;

            float lineLength = math.length(lineVec);
            if (lineLength < 1e-6f) return math.distance(point, lineStart);

            float t = math.dot(pointVec, lineVec) / (lineLength * lineLength);
            t = math.saturate(t);

            float2 projection = lineStart + t * lineVec;
            return math.distance(point, projection);
        }
    }

    public static class VectorRasterizer
    {
        public static JobHandle RasterizeVector(
            VectorData vectorData,
            int textureSize,
            float worldSize,
            ref RasterData rasterData,
            JobHandle dependency = default)
        {
            if (!vectorData.IsValid || vectorData.Count < 3)
            {
                // Fill with default values for invalid input
                var fillJob = new FillDefaultPlanetDataJob
                {
                    ScalarOutput = rasterData.Scalar,
                    AltitudeOutput = rasterData.Altitude,
                    ColorOutput = rasterData.Color,
                    AngleOutput = rasterData.Angle
                };
                return fillJob.Schedule(textureSize * textureSize, 64, dependency);
            }

            var rasterizeJob = new VectorRasterizeJob
            {
                Vertices = vectorData.Vertices,
                vertexCount = vectorData.Count,
                textureSize = textureSize,
                worldSize = worldSize,
                ScalarOutput = rasterData.Scalar,
                AltitudeOutput = rasterData.Altitude,
                ColorOutput = rasterData.Color,
                AngleOutput = rasterData.Angle
            };

            return rasterizeJob.Schedule(textureSize * textureSize, 64, dependency);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct FillDefaultPlanetDataJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float> ScalarOutput;
        [WriteOnly] public NativeArray<float> AltitudeOutput;
        [WriteOnly] public NativeArray<float4> ColorOutput;
        [WriteOnly] public NativeArray<float> AngleOutput;

        public void Execute(int index)
        {
            ScalarOutput[index] = 0f;
            AltitudeOutput[index] = 0f;
            ColorOutput[index] = new float4(0.1f, 0.3f, 0.8f, 1f); // Blue for water
            AngleOutput[index] = 0f;
        }
    }
}