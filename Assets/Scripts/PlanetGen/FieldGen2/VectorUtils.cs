using PlanetGen.FieldGen2.Graph;
using Unity.Collections;
using Unity.Mathematics;

namespace PlanetGen.FieldGen2
{
    public static class VectorUtils
    {
        public static VectorData CreateCircle(float radius, int vertexCount, Allocator allocator = Allocator.Persistent)
        {
            var vectorData = new VectorData(vertexCount, allocator);
    
            for (int i = 0; i < vertexCount; i++)
            {
                // Calculate angle from 0 to 2π
                float angle = (float)i / vertexCount * 2f * math.PI;
        
                // Convert to [-π, π] range as specified in your system
                if (angle > math.PI)
                    angle -= 2f * math.PI;
        
                // Store as polar coordinates (angle, radius)
                vectorData.Vertices[i] = new float2(angle, radius);
            }
    
            // THIS WAS MISSING - set the vertex count!
            vectorData.SetVertexCount(vertexCount);
    
            return vectorData;
        }
    }
}