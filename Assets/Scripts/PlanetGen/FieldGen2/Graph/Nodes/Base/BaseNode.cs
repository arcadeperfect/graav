using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Base
{
    // Base abstract node class
    public abstract class BaseNode : Node
    {
        public static System.Action OnAnyNodeChanged;
        protected virtual void OnValidate()
        {
            if (Application.isPlaying)
            {
                OnAnyNodeChanged?.Invoke();
            }
        }

        protected EvaluationContext GetContext()
        {
            // Access the graph instance (inherited from Node.graph)
            if (graph is GeneratorGraph generatorGraph)
            {
                return generatorGraph.GetEvaluationContext();
            }
            
            // Fallback if graph is null or wrong type
            return new EvaluationContext
            {
                contribution = 1f,
                seed = 0f
            };
        }

        public abstract override object GetValue(NodePort port);
    }

    // Type-specific port wrapper classes for compile-time type safety
    [System.Serializable]
    public class FloatPort : BaseNode
    {
        public override object GetValue(NodePort port) => null;
    }

    [System.Serializable]
    public class Float4Port : BaseNode
    {
        public override object GetValue(NodePort port) => null;
    }

    [System.Serializable]
    public class PlanetDataPort : BaseNode
    {
        public override object GetValue(NodePort port) => null;
    }

    [System.Serializable]
    public class IntArrayPort : BaseNode
    {
        public override object GetValue(NodePort port) => null;
    }

    // Simple interfaces for Scalar and Color outputs
    public interface IScalarOutput
    {
        JobHandle ScheduleScalar(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref NativeArray<float> outputBuffer);
    }

    public interface IColorOutput
    {
        JobHandle ScheduleColor(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref NativeArray<float4> outputBuffer);
    }

    public interface IPlanetDataOutput
    {
        JobHandle SchedulePlanetData(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref RasterData outputBuffer);
    }

    // Legacy interface for backward compatibility
    public interface IFloatOutput
    {
        JobHandle ScheduleFloat(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref NativeArray<float> outputBuffer);
    }

    public interface IIntArrayOutput
    {
        JobHandle ScheduleIntArray(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref NativeArray<int> outputBuffer);
    }
}