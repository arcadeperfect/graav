using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph.Jobs.Generator;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Collections;
using Unity.Jobs;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Initializer
{
    [Node.CreateNodeMenu("Initializers/Initialize PlanetDataNode")]
    public class InitializePlanetDataNode : BaseNode, IPlanetDataOutput
    {
        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public PlanetDataPort planetDataOutput;

        public override object GetValue(NodePort port)
        {
            if (port?.fieldName == nameof(planetDataOutput) || port == null)
            {
                return this;
            }

            return null;
        }

        public JobHandle SchedulePlanetData(JobHandle dependency, int textureSize, List<NativeArray<float>> tempBuffers, ref PlanetData outputBuffer)
        {
            var initJob = new InitializePlanetDataJob
            {
                Output = outputBuffer,
                textureSize = textureSize,
            };
            return initJob.Schedule(textureSize * textureSize, 64, dependency);
        }
    }
}