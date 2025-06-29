using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Jobs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes
{
    [Node.CreateNodeMenuAttribute("Generators/Deformable Circle")]
    public class CircleNode : BaseNode
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public BaseNode deformationInput;

        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public BaseNode output;

        [Range(0f, 1f)] public float radius;

        public float deformationAmplitude;

        public override object GetValue(NodePort port)
        {
            Debug.Log($"CircleNode.GetValue called for port: {port?.fieldName}");
    
            // When someone asks for our output, return this
            if (port?.fieldName == "output" || port == null)
            {
                Debug.Log("CircleNode returning self for output");
                return this;
            }
    
            Debug.Log($"CircleNode: Unexpected port request: {port.fieldName}");
            return null;
        }

        
        public override JobHandle Schedule(JobHandle dependency, int textureSize, List<NativeArray<float>> tempBuffers,
            ref NativeArray<float> outputBuffer)
        {
            Debug.Log("=== CircleNode.Schedule CALLED ===");
            Debug.Log($"Circle params - radius: {radius}, deformationAmplitude: {deformationAmplitude}");
    
            var deformationNode = GetInputValue<BaseNode>("deformationInput");
            Debug.Log($"CircleNode deformation input: {deformationNode?.name} ({deformationNode?.GetType().Name})");

            if (deformationNode == null)
            {
                Debug.LogError("No deformation input - this will cause issues!");
                return dependency;
            }

            var noiseBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.TempJob);
            tempBuffers.Add(noiseBuffer);

            JobHandle noiseHandle = deformationNode.Schedule(dependency, textureSize, tempBuffers, ref noiseBuffer);

            var circleJob = new CircleJob
            {
                DeformationNoise = noiseBuffer,
                Output = outputBuffer,
                textureSize = textureSize,
                radius = this.radius / 2,
                deformationAmplitude = this.deformationAmplitude
            };

            Debug.Log($"CircleJob final params - scaledRadius: {circleJob.radius}, textureSize: {textureSize}");

            return circleJob.Schedule(textureSize * textureSize, 64, noiseHandle);
        }
    }
}