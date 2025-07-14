using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using PlanetGen.FieldGen2.Graph.Types;
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
        [ReadOnly] public NativeArray<float> DeformationNoise; // 2D texture map
        [WriteOnly] public NativeArray<float2> OutputVertices;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float deformationAmplitude;
        [ReadOnly] public NativeArray<float> globalContributionMask;
        [ReadOnly] public bool hasGlobalMask;

        public void Execute(int index)
        {
            float2 inputVertex = InputVertices[index];
            
            // Convert Cartesian to polar for deformation calculation
            float angle = math.atan2(inputVertex.y, inputVertex.x);
            float radius = math.length(inputVertex);

            // Sample deformation noise using the Cartesian position
            float deformation = SampleDeformationNoise(inputVertex, textureSize, DeformationNoise);
            
            // Apply deformation in the radial direction
            float2 radialDirection = math.normalize(inputVertex);
            float2 deformedPosition = inputVertex + (radialDirection * deformation * deformationAmplitude);
            
            float contribution = 1.0f;
            
            if (hasGlobalMask && globalContributionMask.IsCreated)
            {
                contribution = SampleGlobalMask(inputVertex, textureSize, globalContributionMask);
            }
            
            // Blend between original and deformed position
            float2 finalPosition = math.lerp(inputVertex, deformedPosition, contribution);
            
            OutputVertices[index] = finalPosition;
        }
        
        // Sample from 2D texture using Cartesian coordinates
        private float SampleDeformationNoise(float2 position, int texSize, NativeArray<float> noiseMap)
        {
            // Convert world position to texture UV coordinates [0, 1]
            // Assuming the texture represents a [-1, 1] world space
            float u = (position.x + 1.0f) * 0.5f;
            float v = (position.y + 1.0f) * 0.5f;

            return SampleTextureBilinear(u, v, texSize, noiseMap);
        }
        
        private float SampleGlobalMask(float2 position, int texSize, NativeArray<float> mask)
        {
            // Same UV mapping as deformation noise
            float u = (position.x + 1.0f) * 0.5f;
            float v = (position.y + 1.0f) * 0.5f;

            return SampleTextureBilinear(u, v, texSize, mask);
        }
        
        // Bilinear sampling from 2D texture stored as 1D array
        private float SampleTextureBilinear(float u, float v, int texSize, NativeArray<float> texture)
        {
            // Clamp UV to [0, 1]
            u = math.saturate(u);
            v = math.saturate(v);
            
            // Convert to pixel coordinates
            float pixelX = u * (texSize - 1);
            float pixelY = v * (texSize - 1);

            // Get integer coordinates
            int x0 = (int)math.floor(pixelX);
            int y0 = (int)math.floor(pixelY);
            int x1 = math.min(x0 + 1, texSize - 1);
            int y1 = math.min(y0 + 1, texSize - 1);

            // Get fractional parts
            float fx = pixelX - x0;
            float fy = pixelY - y0;

            // Sample the four corner pixels
            float c00 = texture[y0 * texSize + x0];
            float c10 = texture[y0 * texSize + x1];
            float c01 = texture[y1 * texSize + x0];
            float c11 = texture[y1 * texSize + x1];

            // Bilinear interpolation
            float c0 = math.lerp(c00, c10, fx);
            float c1 = math.lerp(c01, c11, fx);
            return math.lerp(c0, c1, fy);
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
            var inputVectorBuffer = new VectorData(outputBuffer.Vertices.Length);
            tempBuffers.AddVectorData(inputVectorBuffer);

            JobHandle vectorHandle = vectorInput.ScheduleVector(dependency, textureSize, tempBuffers, ref inputVectorBuffer);

            var deformationBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            tempBuffers.FloatBuffers.Add(deformationBuffer);

            JobHandle deformationHandle = deformationInput.ScheduleFloat(vectorHandle, textureSize, tempBuffers, ref deformationBuffer);

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
            
            outputBuffer.SetVertexCount(inputVectorBuffer.Count);

            return deformHandle;
        }
    }
}