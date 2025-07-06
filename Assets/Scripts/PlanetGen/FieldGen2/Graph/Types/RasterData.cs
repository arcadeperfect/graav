using Unity.Collections;
using Unity.Mathematics;

namespace PlanetGen.FieldGen2.Graph
{
    public struct RasterData
    {
        public NativeArray<float> Scalar;
        public NativeArray<float> Altitude;
        public NativeArray<float4> Color;
        public NativeArray<float> Angle;

        public RasterData(int size, Allocator allocator = Allocator.Persistent)
        {
            size = size * size;
            Scalar = new NativeArray<float>(size, allocator);
            Altitude = new NativeArray<float>(size, allocator);
            Color = new NativeArray<float4>(size, allocator);
            Angle = new NativeArray<float>(size, allocator);
        }
        
        public void Dispose()
        {
            Scalar.Dispose();
            Altitude.Dispose();
            Color.Dispose();
            Angle.Dispose();
        }
    }
}