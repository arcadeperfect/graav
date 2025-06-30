using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
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

        public abstract override object GetValue(NodePort port);
    }

// Type-specific port wrapper classes for compile-time type safety
    [System.Serializable]
    public class FloatPort : BaseNode
    {
        // This is just a type marker - the actual node logic is in the containing class
        public override object GetValue(NodePort port) => null;
    }

    [System.Serializable]
    public class PlanetDataPort : BaseNode
    {
        // This is just a type marker - the actual node logic is in the containing class
        public override object GetValue(NodePort port) => null;
    }

    [System.Serializable]
    public class IntArrayPort : BaseNode
    {
        // This is just a type marker - the actual node logic is in the containing class
        public override object GetValue(NodePort port) => null;
    }

    public interface IFloatOutput
    {
        JobHandle ScheduleFloat(JobHandle dependency, int textureSize,
            List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer);
    }

    public interface IPlanetDataOutput
    {
        JobHandle SchedulePlanetData(JobHandle dependency, int textureSize,
            List<NativeArray<float>> tempBuffers, ref PlanetData outputBuffer);
    }

    public interface IIntArrayOutput
    {
        JobHandle ScheduleIntArray(JobHandle dependency, int textureSize,
            List<NativeArray<float>> tempBuffers, ref NativeArray<int> outputBuffer);
    }
}