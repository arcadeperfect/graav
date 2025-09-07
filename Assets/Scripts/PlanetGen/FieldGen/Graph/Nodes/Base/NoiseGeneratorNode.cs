using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Base
{
    /// <summary>
    /// Base class for all noise generator nodes.
    /// These nodes have no inputs and always output a FloatPort with noise data.
    /// They are data sources and do not apply global contribution masks.
    /// </summary>
    [NodeTint("#855157")]
    public abstract class NoiseGeneratorNode : BaseNode, IFloatOutput
    {
        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public FloatPort output;

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
            TempBufferManager tempBuffers, ref NativeArray<float> outputBuffer)
        {
            var context = GetContext();
            return ScheduleNoiseGeneration(dependency, textureSize, tempBuffers, ref outputBuffer, context);
        }

        /// <summary>
        /// Implement this method to generate noise data.
        /// This is a data source - no global contribution mask is applied.
        /// </summary>
        protected abstract JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize, 
            TempBufferManager tempBuffers, ref NativeArray<float> outputBuffer, EvaluationContext context);
    }
}