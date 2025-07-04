using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Generator
{
    [BurstCompile(CompileSynchronously = true)]
    public struct CircleJob : IJobParallelFor
    {
        [ReadOnly] public PlanetData InputPlanetData;
        [ReadOnly] public NativeArray<float> DeformationNoise;
        [WriteOnly] public PlanetData Output;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float radius;
        [ReadOnly] public float deformationAmplitude;

        public void Execute(int index)
        {
            // Bounds checking
            if (index >= InputPlanetData.Altitude.Length || index >= DeformationNoise.Length ||
                index >= Output.Altitude.Length)
            {
                return;
            }

            // Get pre-calculated values from input PlanetData
            float altitude = InputPlanetData.Altitude[index]; // This is now distance from center normalized
            float angle = InputPlanetData.Angle[index]; // Angle in radians [-π, π]

            // Sample noise in circular space to avoid seams
            // Convert polar coordinates (angle, radius) to Cartesian for noise sampling
            float noiseRadius = 1.0f; // Fixed radius for noise sampling
            float noiseX = math.cos(angle) * noiseRadius;
            float noiseY = math.sin(angle) * noiseRadius;

            // Map noise coordinates to noise array index
            // Scale and offset to map from [-1,1] to [0, textureSize-1]
            int noiseIndexX = (int)((noiseX + 1.0f) * 0.5f * (textureSize - 1));
            int noiseIndexY = (int)((noiseY + 1.0f) * 0.5f * (textureSize - 1));
            noiseIndexX = math.clamp(noiseIndexX, 0, textureSize - 1);
            noiseIndexY = math.clamp(noiseIndexY, 0, textureSize - 1);
            int noiseIndex = noiseIndexY * textureSize + noiseIndexX;
            noiseIndex = math.clamp(noiseIndex, 0, DeformationNoise.Length - 1);

            // Apply deformation to radius based on circular noise sampling
            float deformation = DeformationNoise[noiseIndex] * deformationAmplitude;
            float deformedRadius = radius + deformation;

            // Circle test: true for points INSIDE the circle
            // altitude is normalized distance from center (0 = center, 1 = edge)
            float circleValue = altitude <= deformedRadius ? 1.0f : 0.0f;

            // Apply smooth falloff at the edge
            float falloffWidth = 0;
            if (altitude > deformedRadius - falloffWidth && altitude <= deformedRadius + falloffWidth)
            {
                float falloff = (deformedRadius + falloffWidth - altitude) / (2.0f * falloffWidth);
                circleValue = math.smoothstep(0.0f, 1.0f, falloff);
            }

            // Copy input data and set the scalar value
            Output.Altitude[index] = InputPlanetData.Altitude[index];
            Output.Angle[index] = InputPlanetData.Angle[index];
            Output.Scalar[index] = circleValue;
            Output.Color[index] = new float4(circleValue, circleValue, circleValue, 1.0f);
        }
    }

    [Node.CreateNodeMenu("Generators/Deform Circle")]
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
            TempBufferManager tempBuffers, ref PlanetData outputBuffer)
        {
            var planetDataNode = GetInputValue<BaseNode>(nameof(planetDataInput));
            var deformationNode = GetInputValue<BaseNode>(nameof(deformationInput));

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
            tempBuffers.AddPlanetData(inputPlanetData);

            // Schedule the planet data input
            JobHandle planetHandle = planetDataOutput.SchedulePlanetData(dependency, textureSize, tempBuffers, ref inputPlanetData);

            // Create temp buffer for deformation noise
            var noiseBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            tempBuffers.FloatBuffers.Add(noiseBuffer);

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