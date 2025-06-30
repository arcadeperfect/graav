using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph.Jobs;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes
{
    [Node.CreateNodeMenu("Operators/Add")]
    public class AddNode : BaseNode, IFloatOutput
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public FloatPort inputA;
        
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public FloatPort inputB;

        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public FloatPort output;
        
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
            var nodeA = GetInputValue<BaseNode>("inputA") as IFloatOutput;
            var nodeB = GetInputValue<BaseNode>("inputB") as IFloatOutput;
            
            if (nodeA == null)
            {
                Debug.LogError("AddNode: inputA is null or does not implement IFloatOutput");
                return dependency;
            }
            
            if (nodeB == null)
            {
                Debug.LogError("AddNode: inputB is null or does not implement IFloatOutput");
                return dependency;
            }
            
            // Create temp buffers for inputs
            var bufferA = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            var bufferB = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            
            tempBuffers.Add(bufferA);
            tempBuffers.Add(bufferB);
            
            // Schedule both input nodes
            JobHandle handleA = nodeA.ScheduleFloat(dependency, textureSize, tempBuffers, ref bufferA);
            JobHandle handleB = nodeB.ScheduleFloat(dependency, textureSize, tempBuffers, ref bufferB);
            
            // Combine dependencies to ensure both inputs complete before addition
            JobHandle combinedDeps = JobHandle.CombineDependencies(handleA, handleB);
        
            // Schedule the add job
            var addJob = new AddJob
            {
                InputA = bufferA,
                InputB = bufferB,
                Output = outputBuffer
            };
        
            return addJob.Schedule(textureSize * textureSize, 64, combinedDeps);
        }
    }
}