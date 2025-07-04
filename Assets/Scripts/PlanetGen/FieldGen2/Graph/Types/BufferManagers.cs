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
        public List<VectorData> VectorBuffers; // Add this
    
        public TempBufferManager(bool initialize = true)
        {
            if (initialize)
            {
                FloatBuffers = new List<NativeArray<float>>();
                Float4Buffers = new List<NativeArray<float4>>();
                IntBuffers = new List<NativeArray<int>>();
                VectorBuffers = new List<VectorData>(); // Add this
            }
            else
            {
                FloatBuffers = null;
                Float4Buffers = null;
                IntBuffers = null;
                VectorBuffers = null; // Add this
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
        
        // Convenience method for adding VectorData buffers
        public void AddVectorData(VectorData vectorData)
        {
            VectorBuffers.Add(vectorData);
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
            if (VectorBuffers != null)
            {
                foreach (var buffer in VectorBuffers)
                {
                    if (buffer.IsValid)
                        buffer.Dispose();
                }
                VectorBuffers.Clear();
            }
        }
    }

    // ... rest of the file remains the same
}