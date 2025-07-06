using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Types;
using Unity.Collections;
using Unity.Mathematics;

namespace PlanetGen.FieldGen2
{
    public static class VectorUtils
    {
        // create a circle with polar coordinates
        // public static VectorData CreateCircle(float radius, int vertexCount, Allocator allocator = Allocator.Persistent)
        // {
        //     var vectorData = new VectorData(vertexCount, allocator);
        //
        //     for (int i = 0; i < vertexCount; i++)
        //     {
        //         // Calculate angle from 0 to 2π
        //         float angle = (float)i / vertexCount * 2f * math.PI;
        //
        //         // Convert to [-π, π] range as specified in your system
        //         if (angle > math.PI)
        //             angle -= 2f * math.PI;
        //
        //         // Store as polar coordinates (angle, radius)
        //         vectorData.Vertices[i] = new float2(angle, radius);
        //     }
        //
        //     // THIS WAS MISSING - set the vertex count!
        //     vectorData.SetVertexCount(vertexCount);
        //
        //     return vectorData;
        // }

        // create a circle with cartesian coordinates
        public static VectorData CreateCircle(float radius, int vertexCount, Allocator allocator = Allocator.Persistent)
        {
            var vectorData = new VectorData(vertexCount, allocator);
            vectorData.SetVertexCount(vertexCount);

            for (int i = 0; i < vertexCount; i++)
            {
                float angle = (i / (float)vertexCount) * 2f * math.PI;
                float x = math.cos(angle) * radius;
                float y = math.sin(angle) * radius;

                vectorData.Vertices[i] = new float2(x, y);

                // Initialize optional data
                vectorData.VertexWeights[i] = 1f;
                vectorData.VertexColors[i] = new float4(1f, 1f, 1f, 1f);
            }

            return vectorData;
        }
    }
}