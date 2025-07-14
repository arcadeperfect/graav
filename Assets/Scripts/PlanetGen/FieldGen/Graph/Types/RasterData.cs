using System;
using PlanetGen.FieldGen2.Graph.Types;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen.FieldGen2.Graph
{
    public struct RasterData
    {
        public NativeArray<float> Scalar;
        public NativeArray<float> Altitude;
        public NativeArray<float4> Color;
        public NativeArray<float> Angle;

        private readonly int _size;
        private bool _disposed;
        
        public bool IsValid => _isValid();
        private bool _isValid()
        {
            var valid = true;

            if (!Scalar.IsCreated)
            {
                Debug.LogWarning("RasterData: Scalar array is not created.");
                valid = false;
            }
            if (!Color.IsCreated)
            {
                Debug.LogWarning("RasterData: Color array is not created.");
                valid = false;
            }
            if (!Altitude.IsCreated)
            {
                Debug.LogWarning("RasterData: Altitude array is not created.");
                valid = false;
            }
            if (!Angle.IsCreated)
            {
                Debug.LogWarning("RasterData: Angle array is not created.");
                valid = false;
            }
            if (_size <= 0)
            {
                Debug.LogWarning("RasterData: Size is not positive.");
                valid = false;
            }

            if (_disposed)
            {
                Debug.LogWarning("RasterData: Disposed.");
                valid = false;
            }
            return valid;
        }

        public RasterData(int size, Allocator allocator = Allocator.Persistent)
        {
            this._size = size;
            size = size * size;
            Scalar = new NativeArray<float>(size, allocator);
            Altitude = new NativeArray<float>(size, allocator);
            Color = new NativeArray<float4>(size, allocator);
            Angle = new NativeArray<float>(size, allocator);
            _disposed = false;
        }
        
        // public void Dispose()
        // {
        //     Scalar.Dispose();
        //     Altitude.Dispose();
        //     Color.Dispose();
        //     Angle.Dispose();
        // }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                if (Scalar.IsCreated)
                {
                    Scalar.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
                // Already disposed elsewhere - this is fine
            }

            try
            {
                if (Color.IsCreated)
                {
                    Color.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
                // Already disposed elsewhere - this is fine
            }

            // Handle other arrays (Altitude, Angle, etc.) similarly...

            _disposed = true;
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