using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph.Jobs;
using PlanetGen.FieldGen2.Graph.Jobs.Generator;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Generator
{
    [CreateNodeMenu("Generators/Deform Circle")]
    public class CircleNode : BaseNode, IPlanetDataOutput
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public PlanetDataPort planetDataInput;

        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public FloatPort deformationInput;

        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public PlanetDataPort output;

        [Range(0f, 1f)] public float radius = 0.5f;
        public float deformationAmplitude = 0.1f;

        public override object GetValue(NodePort port)
        {
            if (port?.fieldName == "output" || port == null)
            {
                return this;
            }
            return null;
        }

        public JobHandle SchedulePlanetData(JobHandle dependency, int textureSize, 
            List<NativeArray<float>> tempBuffers, ref PlanetData outputBuffer)
        {
            var planetDataNode = GetInputValue<BaseNode>(nameof(planetDataInput));
            var deformationNode = GetInputValue<BaseNode>(nameof(deformationInput));
            
            // Use interface checks instead of capability bools
            if (!(planetDataNode is IPlanetDataOutput planetDataOutput))
            {
                Debug.LogError("CircleNode: No valid PlanetData input connected!");
                return dependency;
            }

            if (!(deformationNode is IFloatOutput floatOutput))
            {
                Debug.LogError("CircleNode: No valid deformation input connected!");
                return dependency;
            }

            // Create temp buffer for planet data input
            var inputPlanetData = new PlanetData(textureSize);
            tempBuffers.Add(inputPlanetData.Altitude);
            tempBuffers.Add(inputPlanetData.Angle);
            tempBuffers.Add(inputPlanetData.Scalar);

            // Schedule the planet data input
            JobHandle planetHandle = planetDataOutput.SchedulePlanetData(dependency, textureSize, tempBuffers, ref inputPlanetData);

            // Create temp buffer for deformation noise
            var noiseBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            tempBuffers.Add(noiseBuffer);

            // Schedule the deformation noise
            JobHandle noiseHandle = floatOutput.ScheduleFloat(planetHandle, textureSize, tempBuffers, ref noiseBuffer);

            // Create and schedule the circle job
            var circleJob = new CircleJob
            {
                InputPlanetData = inputPlanetData,
                DeformationNoise = noiseBuffer,
                Output = outputBuffer,
                textureSize = textureSize,
                radius = this.radius,
                deformationAmplitude = this.deformationAmplitude
            };

            return circleJob.Schedule(textureSize * textureSize, 64, noiseHandle);
        }
    }
}