using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Base
{
    /// <summary>
    /// Base class for all noise generator nodes.
    /// These nodes have no inputs and always output a FloatPort with noise data.
    /// </summary>
    public abstract class NoiseGeneratorNode : BaseNode, IFloatOutput
    {
        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public FloatPort output;

        // All noise generators output float arrays
        // public override bool CanOutputFloat => true;

        // Standard noise parameters that most generators will use
        [Header("Noise Parameters")]
        public float frequency = 10f;
        public float amplitude = 1f;
        public float seed = 12345f;

        public override object GetValue(NodePort port)
        {
            if (port?.fieldName == "output" || port == null)
            {
                return this;
            }
            return null;
        }

        public JobHandle ScheduleFloat(JobHandle dependency, int textureSize, 
            List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
        {
            return ScheduleNoiseGeneration(dependency, textureSize, tempBuffers, ref outputBuffer);
        }

        
        protected abstract JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize, 
            List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer);
    }
}