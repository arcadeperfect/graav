using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph.Jobs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes
{
    [CreateNodeMenu("Generators/Simplex Noise")]

    public class SimplexNoiseNode: BaseNode
    {
        [Output(backingValue = ShowBackingValue.Never)] public BaseNode output;

        public float frequency = 10f;
        public float amplitude = 1f;
        
        public override object GetValue(NodePort port)
        {
            Debug.Log($"SimplexNoiseNode.GetValue called for port: {port?.fieldName}");
    
            if (port?.fieldName == "output")
            {
                Debug.Log("SimplexNoiseNode returning self");
                return this;
            }
    
            return null;
        }

        public override JobHandle Schedule(JobHandle depedency, int textureSize, List<NativeArray<float>> tempBuffers,
            ref NativeArray<float> outputBuffer)
        {
            var noiseJob = new SimplexNoiseJob
            {
                outputBuffer = outputBuffer,
                textureSize = textureSize,
                frequency = this.frequency,
                amplitude = this.amplitude,
                seed = 12345
            };

            return noiseJob.Schedule(textureSize * textureSize, 64, depedency);
        }

    }
}