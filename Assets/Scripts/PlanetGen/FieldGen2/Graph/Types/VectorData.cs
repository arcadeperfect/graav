using Unity.Collections;
using Unity.Mathematics;

namespace PlanetGen.FieldGen2.Graph
{
    public struct VectorData
    {
        // Polyline vertices in polar coordinates (angle, radius)
        // angle: [-π, π], radius: [0, 1]
        public NativeArray<float2> Vertices;
        
        // Actual number of vertices in use (vertices array may be larger for efficiency)
        public NativeArray<int> VertexCount;
        
        // Optional per-vertex data for advanced operations
        public NativeArray<float> VertexWeights;    // Individual vertex influence [0,1]
        public NativeArray<float4> VertexColors;    // Per-vertex colors for visualization

        public VectorData(int maxVertices, Allocator allocator = Allocator.Persistent)
        {
            Vertices = new NativeArray<float2>(maxVertices, allocator);
            VertexCount = new NativeArray<int>(1, allocator);
            VertexWeights = new NativeArray<float>(maxVertices, allocator);
            VertexColors = new NativeArray<float4>(maxVertices, allocator);
            
            // Initialize vertex count to 0
            VertexCount[0] = 0;
        }
        
        public void Dispose()
        {
            if (Vertices.IsCreated) Vertices.Dispose();
            if (VertexCount.IsCreated) VertexCount.Dispose();
            if (VertexWeights.IsCreated) VertexWeights.Dispose();
            if (VertexColors.IsCreated) VertexColors.Dispose();
        }
        
        // Helper properties
        public int Count => VertexCount.IsCreated ? VertexCount[0] : 0;
        public bool IsValid => Vertices.IsCreated && VertexCount.IsCreated;
        
        // Helper method to set vertex count safely
        public void SetVertexCount(int count)
        {
            if (VertexCount.IsCreated)
            {
                VertexCount[0] = math.min(count, Vertices.Length);
            }
        }
    }
}