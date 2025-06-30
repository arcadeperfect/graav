using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph
{
    public struct TempBufferManager
    {
        public List<NativeArray<float>> FloatBuffers;
        public List<NativeArray<float4>> Float4Buffers;
        public List<NativeArray<int>> IntBuffers;
        
        public TempBufferManager(bool initialize = true)
        {
            if (initialize)
            {
                FloatBuffers = new List<NativeArray<float>>();
                Float4Buffers = new List<NativeArray<float4>>();
                IntBuffers = new List<NativeArray<int>>();
            }
            else
            {
                FloatBuffers = null;
                Float4Buffers = null;
                IntBuffers = null;
            }
        }
        
        // Convenience methods for adding PlanetData buffers
        public void AddPlanetData(PlanetData planetData)
        {
            FloatBuffers.Add(planetData.Scalar);
            FloatBuffers.Add(planetData.Altitude);
            FloatBuffers.Add(planetData.Angle);
            Float4Buffers.Add(planetData.Color);
        }
        
        // Method to dispose all buffers (called by FieldGen2)
        public void DisposeAll()
        {
            if (FloatBuffers != null)
            {
                foreach (var buffer in FloatBuffers)
                {
                    if (buffer.IsCreated)
                        buffer.Dispose();
                }
                FloatBuffers.Clear();
            }
            
            if (Float4Buffers != null)
            {
                foreach (var buffer in Float4Buffers)
                {
                    if (buffer.IsCreated)
                        buffer.Dispose();
                }
                Float4Buffers.Clear();
            }
            
            if (IntBuffers != null)
            {
                foreach (var buffer in IntBuffers)
                {
                    if (buffer.IsCreated)
                        buffer.Dispose();
                }
                IntBuffers.Clear();
            }
        }
    }

    public struct OutputBufferManager
    {
        // Simple outputs: Scalar and Color only
        public NativeArray<float> ScalarOutput;
        public NativeArray<float4> ColorOutput;
        public PlanetData PlanetDataOutput;
        
        // Flags to track which outputs are valid/requested
        public bool HasScalarOutput;
        public bool HasColorOutput;
        public bool HasPlanetDataOutput;
        
        public OutputBufferManager(bool initialize = true)
        {
            if (initialize)
            {
                ScalarOutput = default;
                ColorOutput = default;
                PlanetDataOutput = default;
                
                HasScalarOutput = false;
                HasColorOutput = false;
                HasPlanetDataOutput = false;
            }
            else
            {
                ScalarOutput = default;
                ColorOutput = default;
                PlanetDataOutput = default;
                
                HasScalarOutput = false;
                HasColorOutput = false;
                HasPlanetDataOutput = false;
            }
        }
        
        // Methods to set specific output types
        public void SetScalarOutput(NativeArray<float> buffer)
        {
            ScalarOutput = buffer;
            HasScalarOutput = true;
        }
        
        public void SetColorOutput(NativeArray<float4> buffer)
        {
            ColorOutput = buffer;
            HasColorOutput = true;
        }
        
        public void SetPlanetDataOutput(PlanetData buffer)
        {
            PlanetDataOutput = buffer;
            HasPlanetDataOutput = true;
        }
    }
}
