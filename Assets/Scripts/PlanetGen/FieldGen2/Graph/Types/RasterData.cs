using PlanetGen.FieldGen;
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

        private readonly int _size;

        public RasterData(int size, Allocator allocator = Allocator.Persistent)
        {
            this._size = size;
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

        public float GetScalarAt(int x, int y)
        {
            // return Scalar.GetScalarValue(x, y, _size);
            return Scalar.GetValue(x, y, _size);
        }

        public float4 GetColorAt(int x, int y)
        {
            return Color.GetValue(x, y, _size);
        }
    }
}