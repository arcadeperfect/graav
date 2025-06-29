using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes
{
    [CreateNodeMenu("Output/Final Output")]
    public class OutputNode : BaseNode
    {
        [Input(backingValue = ShowBackingValue.Never)] public BaseNode input;

        /// <summary>
        /// This is a key part of the fix. We now properly participate in the GetValue chain
        /// by getting the value from our input node and passing it through. xNode uses this
        /// internally to resolve the graph connections correctly.
        /// </summary>
        public override object GetValue(NodePort port)
        {
            // Ask our input port "who is connected to you?" and return that node.
            return GetInputValue<BaseNode>("input");
        }
    
        public override JobHandle Schedule(JobHandle dependency, int textureSize, List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
        {
            Debug.Log("=== OutputNode.Schedule DEBUG ===");
    
            var inputPort = GetInputPort("input");
            Debug.Log($"Input port exists: {inputPort != null}");
            Debug.Log($"Input port connection count: {inputPort?.ConnectionCount ?? 0}");
    
            if (inputPort != null)
            {
                for (int i = 0; i < inputPort.ConnectionCount; i++)
                {
                    var connection = inputPort.GetConnection(i);
                    Debug.Log($"  Raw Connection {i}: node={connection.node?.name}, type={connection.node?.GetType().Name}");
                }
            }
    
            BaseNode inputNode1 = GetInputValue<BaseNode>("input");
            object inputNode2 = GetInputValue<object>("input");
    
            Debug.Log($"GetInputValue<BaseNode>: {inputNode1?.name} ({inputNode1?.GetType().Name})");
            Debug.Log($"GetInputValue<object>: {inputNode2?.GetType().Name}");
    
            if (inputNode1 != null)
            {
                Debug.Log($"Calling Schedule on: {inputNode1.name}");
                return inputNode1.Schedule(dependency, textureSize, tempBuffers, ref outputBuffer);
            }

            Debug.LogWarning("OutputNode is not connected to anything!");
            return dependency;
        }
    }
}