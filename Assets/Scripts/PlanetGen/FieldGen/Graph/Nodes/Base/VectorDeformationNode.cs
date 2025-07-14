using PlanetGen.FieldGen2.Graph.Types;
using Unity.Jobs;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Base
{
    /// <summary>
    /// Base class for vector deformation nodes.
    /// These nodes take vector input, deformation data, and automatically apply global contribution mask.
    /// </summary>
    public abstract class VectorDeformationNode : BaseNode, IVectorOutput
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public VectorPort vectorInput;

        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public FloatPort deformationInput;

        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public VectorPort output;

        public override object GetValue(NodePort port)
        {
            if (port?.fieldName == "output" || port == null)
            {
                return this;
            }
            return null;
        }

        public JobHandle ScheduleVector(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref VectorData outputBuffer)
        {
            var vectorNode = GetInputValue<BaseNode>(nameof(vectorInput));
            var deformationNode = GetInputValue<BaseNode>(nameof(deformationInput));

            if (!(vectorNode is IVectorOutput vectorOutput))
            {
                Debug.LogError($"{GetType().Name}: No valid Vector input connected!");
                return dependency;
            }

            if (!(deformationNode is IFloatOutput deformationOutput))
            {
                Debug.LogError($"{GetType().Name}: No valid deformation input connected!");
                return dependency;
            }

            var context = GetContext();
            return ScheduleVectorDeformation(dependency, textureSize, tempBuffers, ref outputBuffer, 
                vectorOutput, deformationOutput, context);
        }

        /// <summary>
        /// Implement this method to deform input vectors based on deformation data.
        /// Global contribution mask is automatically applied via the context.
        /// </summary>
        protected abstract JobHandle ScheduleVectorDeformation(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref VectorData outputBuffer,
            IVectorOutput vectorInput, IFloatOutput deformationInput, EvaluationContext context);
    }
}