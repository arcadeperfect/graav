using PlanetGen.FieldGen2.Graph.Nodes.Base;
using PlanetGen.FieldGen2.Graph.Types;
using Unity.Jobs;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.IO
{
    /// <summary>
    /// Output node that captures VectorData from the graph for external use
    /// </summary>
    [Node.CreateNodeMenu("IO/Vector Output")]
    public class VectorOutputNode : BaseNode, IVectorOutput
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public VectorPort input;

        [Header("Vector Output")] [Tooltip("Outputs VectorData for use by orchestrator or next graph")]
        public bool showInfo = true;

        public override object GetValue(NodePort port)
        {
            return GetInputValue<BaseNode>(nameof(input));
        }

        public JobHandle ScheduleVector(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref VectorData outputBuffer)
        {
            var inputNode = GetInputValue<BaseNode>(nameof(input));
            if (!(inputNode is IVectorOutput vectorOutput))
            {
                Debug.LogError("VectorOutputNode: No valid Vector input connected");
                outputBuffer.VertexCount[0] = 0;
                return dependency;
            }

            return vectorOutput.ScheduleVector(dependency, textureSize, tempBuffers, ref outputBuffer);
        }
    }
}