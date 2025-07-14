using System;
using Unity.Collections;

namespace PlanetGen.FieldGen2.Graph.Types
{
    public static class NativeArray2DExtensions
    {
        public static T GetValue<T>(this NativeArray<T> array, int x, int y, int width) where T : struct
        {
            if(!array.IsCreated)
                throw new InvalidOperationException("NativeArray is not created");
            
            if(x<0 || x >= width || y < 0 || y >= width)
                throw new ArgumentOutOfRangeException("Coordinates out of bounds");

            int index = y * width + x;
            
            if(index >= array.Length)
                throw new ArgumentOutOfRangeException("Calculated index is out of bounds of the NativeArray");

            return array[index];
        }
    }
}