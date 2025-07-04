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
        [ReadOnly] public NativeArray<float> DeformationNoise; // This is now treated as a 2D texture map
        [WriteOnly] public NativeArray<float2> OutputVertices;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float deformationAmplitude;
        [ReadOnly] public NativeArray<float> globalContributionMask;
        [ReadOnly] public bool hasGlobalMask;

        public void Execute(int index)
        {
            float2 inputVertex = InputVertices[index];
            float angle = inputVertex.x;
            float radius = inputVertex.y;

            // *** CHANGED: Sample deformation noise using bilinear filtering from the 2D map ***
            float deformation = SampleDeformationNoiseBilinear(angle, radius, textureSize, DeformationNoise);
            float deformedRadius = radius + (deformation * deformationAmplitude);
            deformedRadius = math.clamp(deformedRadius, 0f, 1f);
    
            float contribution = 1.0f;
    
            if (hasGlobalMask && globalContributionMask.IsCreated)
            {
                // *** CHANGED: Use bilinear filtering for global contribution mask ***
                contribution = SampleGlobalMaskBilinear(angle, radius, textureSize, globalContributionMask);
            }
            
            float finalRadius = math.lerp(radius, deformedRadius, contribution);
    
            OutputVertices[index] = new float2(angle, finalRadius);
        }
        
        // *** NEW/MODIFIED: Helper function for bilinear sampling of a 2D mask from polar coordinates ***
        private float SampleGlobalMaskBilinear(float angle, float radius, int texSize, NativeArray<float> mask)
        {
            // Convert polar vertex position to texture UV coordinates [0, 1]
            float x_cartesian = math.cos(angle) * radius;
            float y_cartesian = math.sin(angle) * radius;
            
            float u = (x_cartesian + 1.0f) * 0.5f; // u in [0, 1]
            float v = (y_cartesian + 1.0f) * 0.5f; // v in [0, 1]

            // Convert to pixel coordinates (float precision for interpolation)
            float pixelX = u * (texSize - 1);
            float pixelY = v * (texSize - 1);

            // Get the integer coordinates of the top-left pixel for interpolation
            int x0 = (int)math.floor(pixelX);
            int y0 = (int)math.floor(pixelY);
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            // Get the fractional parts for lerping
            float fx = pixelX - x0;
            float fy = pixelY - y0;

            // Clamp coordinates to valid texture range
            x0 = math.clamp(x0, 0, texSize - 1);
            y0 = math.clamp(y0, 0, texSize - 1);
            x1 = math.clamp(x1, 0, texSize - 1);
            y1 = math.clamp(y1, 0, texSize - 1);

            // Get values from the 1D NativeArray, treating it as 2D
            float c00 = mask[y0 * texSize + x0];
            float c10 = mask[y0 * texSize + x1];
            float c01 = mask[y1 * texSize + x0];
            float c11 = mask[y1 * texSize + x1];

            // Bilinear interpolation
            float c0 = math.lerp(c00, c10, fx);
            float c1 = math.lerp(c01, c11, fx);
            return math.lerp(c0, c1, fy);
        }
        
        // *** NEW/MODIFIED: Renamed and adjusted to use bilinear for internal deformation noise ***
        private float SampleDeformationNoiseBilinear(float angle, float radius, int texSize, NativeArray<float> noiseMap)
        {
            // This now simply reuses the bilinear sampling logic since DeformationNoise
            // is now also treated as a 2D map.
            return SampleGlobalMaskBilinear(angle, radius, texSize, noiseMap);
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