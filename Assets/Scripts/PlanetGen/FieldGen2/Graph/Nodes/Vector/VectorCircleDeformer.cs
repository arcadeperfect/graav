using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Vector
{
    [BurstCompile(CompileSynchronously = true)]
    public struct VectorCircleDeformJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> InputVertices;
        [ReadOnly] public NativeArray<float> DeformationNoise;
        [WriteOnly] public NativeArray<float2> OutputVertices;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float deformationAmplitude;
        [ReadOnly] public NativeArray<float> globalContributionMask;
        [ReadOnly] public bool hasGlobalMask;

        public void Execute(int index)
        {
            // Get input vertex in polar coordinates (angle, radius)
            float2 inputVertex = InputVertices[index];
            float angle = inputVertex.x;
            float radius = inputVertex.y;

            // Sample deformation noise consistently regardless of vertex count
            float deformation = SampleDeformationNoise(angle);
            float deformedRadius = radius + (deformation * deformationAmplitude);
            deformedRadius = math.clamp(deformedRadius, 0f, 1f);
    
            // Sample global contribution mask at vertex position in texture space
            float contribution = 1.0f; // Default to full contribution
    
            if (hasGlobalMask)
            {
                if (globalContributionMask.IsCreated)
                {
                    contribution = SampleGlobalMask(angle, radius);
                }
                // If hasGlobalMask is true but array isn't created, we'll get a job system error anyway
            }
            // If hasGlobalMask is false, use default contribution of 1.0f
    
            // Blend between original and deformed based on global contribution
            float finalRadius = math.lerp(radius, deformedRadius, contribution);
    
            // Output final vertex in polar coordinates
            OutputVertices[index] = new float2(angle, finalRadius);
        }
        
        private float SampleDeformationNoise(float angle)
        {
            // Sample noise consistently regardless of vertex count
            float normalizedAngle = (angle + math.PI) / (2.0f * math.PI); // Convert [-π, π] to [0, 1]
            
            // Sample the noise texture along a circular path
            int sampleCount = textureSize;
            float samplePosition = normalizedAngle * sampleCount;
            int baseSample = (int)math.floor(samplePosition);
            float lerpFactor = samplePosition - baseSample;
            
            // Get two samples for interpolation
            int sample1 = baseSample % sampleCount;
            int sample2 = (baseSample + 1) % sampleCount;
            
            // Convert samples to 2D texture coordinates on a circle
            float angle1 = (float)sample1 / sampleCount * 2.0f * math.PI;
            float angle2 = (float)sample2 / sampleCount * 2.0f * math.PI;
            
            // Sample noise at consistent radius
            float sampleRadius = 0.5f;
            
            // Get noise values for interpolation
            float noise1 = SampleNoiseAtPosition(angle1, sampleRadius);
            float noise2 = SampleNoiseAtPosition(angle2, sampleRadius);
            
            // Interpolate between the two samples
            return math.lerp(noise1, noise2, lerpFactor);
        }
        
        private float SampleGlobalMask(float angle, float radius)
        {
            // Convert polar vertex position to texture coordinates
            float x = math.cos(angle) * radius;
            float y = math.sin(angle) * radius;
            
            // Map from [-1,1] to texture coordinates [0, textureSize-1]
            int texX = (int)((x + 1.0f) * 0.5f * (textureSize - 1));
            int texY = (int)((y + 1.0f) * 0.5f * (textureSize - 1));
            texX = math.clamp(texX, 0, textureSize - 1);
            texY = math.clamp(texY, 0, textureSize - 1);
            
            int maskIndex = texY * textureSize + texX;
            return globalContributionMask[math.clamp(maskIndex, 0, globalContributionMask.Length - 1)];
        }
        
        private float SampleNoiseAtPosition(float angle, float radius)
        {
            // Convert polar to cartesian for noise sampling
            float noiseX = math.cos(angle) * radius;
            float noiseY = math.sin(angle) * radius;

            // Map to texture coordinates
            int noiseIndexX = (int)((noiseX + 1.0f) * 0.5f * (textureSize - 1));
            int noiseIndexY = (int)((noiseY + 1.0f) * 0.5f * (textureSize - 1));
            noiseIndexX = math.clamp(noiseIndexX, 0, textureSize - 1);
            noiseIndexY = math.clamp(noiseIndexY, 0, textureSize - 1);
            int noiseIndex = noiseIndexY * textureSize + noiseIndexX;
            noiseIndex = math.clamp(noiseIndex, 0, DeformationNoise.Length - 1);

            return DeformationNoise[noiseIndex];
        }
    }

    [Node.CreateNodeMenu("Vector/Circle Deformer")]
    public class VectorCircleDeformerNode : VectorDeformationNode
    {
        [Header("Deformation Parameters")]
        public float deformationAmplitude = 0.1f;

        protected override JobHandle ScheduleVectorDeformation(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref VectorData outputBuffer,
            IVectorOutput vectorInput, IFloatOutput deformationInput, EvaluationContext context)
        {
            // Create temp buffer for input vector
            var inputVectorBuffer = new VectorData(outputBuffer.Vertices.Length);
            tempBuffers.AddVectorData(inputVectorBuffer);

            // Schedule the input vector
            JobHandle vectorHandle = vectorInput.ScheduleVector(dependency, textureSize, tempBuffers, ref inputVectorBuffer);

            // Create temp buffer for deformation noise
            var deformationBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            tempBuffers.FloatBuffers.Add(deformationBuffer);

            // Schedule the deformation input
            JobHandle deformationHandle = deformationInput.ScheduleFloat(vectorHandle, textureSize, tempBuffers, ref deformationBuffer);

            // Create and schedule the deformation job
            var deformJob = new VectorCircleDeformJob
            {
                InputVertices = inputVectorBuffer.Vertices,
                DeformationNoise = deformationBuffer,
                OutputVertices = outputBuffer.Vertices,
                textureSize = textureSize,
                deformationAmplitude = this.deformationAmplitude,
                globalContributionMask = context.globalContributionMask,
                hasGlobalMask = context.hasGlobalMask
            };

            JobHandle deformHandle = deformJob.Schedule(inputVectorBuffer.Count, 64, deformationHandle);
            
            // Set output vertex count to match input
            outputBuffer.SetVertexCount(inputVectorBuffer.Count);

            return deformHandle;
        }
    }
}