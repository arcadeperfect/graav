using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph.Jobs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes
{
    [CreateNodeMenu("Operators/Add")]
    public class AddNode : BaseNode
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public BaseNode inputA;
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public BaseNode inputB;

        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public BaseNode output;
        
        public override object GetValue(NodePort port)
        {
            return this;
        }
        
        public override JobHandle Schedule(JobHandle dependency, int textureSize, List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
        {
            var nodeA = GetInputValue<BaseNode>("inputA");
            var nodeB = GetInputValue<BaseNode>("inputB");
            
            if(nodeA == null || nodeB == null)
                Debug.LogError("AddNode: missing input(s)");
            
            var bufferA = new NativeArray<float>(textureSize * textureSize, Allocator.TempJob);
            var bufferB = new NativeArray<float>(textureSize * textureSize, Allocator.TempJob);
            
            tempBuffers.Add(bufferA);
            tempBuffers.Add(bufferB);
            
            JobHandle handleA = nodeA.Schedule(dependency, textureSize, tempBuffers, ref bufferA);
            JobHandle handleB = nodeB.Schedule(dependency, textureSize, tempBuffers, ref bufferB);
            
            JobHandle combinedDeps = JobHandle.CombineDependencies(handleA, handleB);
        
            var addJob = new AddJob()
            {
                InputA = bufferA,
                InputB = bufferB,
                Output = outputBuffer
            };
        
            return addJob.Schedule(textureSize * textureSize, 64, combinedDeps);
        
            // return addJob.Schedule()
        }
        
    }
}