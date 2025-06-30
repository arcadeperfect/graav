using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Outputs
{
    [Node.CreateNodeMenu("Output/Final Output")]
    public class OutputNode : BaseNode, IPlanetDataOutput
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public PlanetDataPort input;

        public override object GetValue(NodePort port)
        {
            return GetInputValue<BaseNode>(nameof(input));
        }

        public JobHandle SchedulePlanetData(JobHandle dependency, int textureSize, List<NativeArray<float>> tempBuffers,
            ref PlanetData outputBuffer)
        {
            var inputNode = GetInputValue<BaseNode>(nameof(input));
            if(!(inputNode is IPlanetDataOutput planetDataOutput))
            {
                Debug.LogError("OutputNode: No valid PlanetData input connected");
                return dependency;
            }
            return planetDataOutput.SchedulePlanetData(dependency, textureSize, tempBuffers, ref outputBuffer);
        }
    }
    
}