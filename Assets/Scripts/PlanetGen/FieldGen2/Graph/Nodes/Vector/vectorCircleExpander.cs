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
    public struct VectorExpandJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> InputVertices;
        [WriteOnly] public NativeArray<float2> OutputVertices;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float expansionAmount;
        [ReadOnly] public NativeArray<float> globalContributionMask;
        [ReadOnly] public bool hasGlobalMask;

        public void Execute(int index)
        {
            float2 inputVertex = InputVertices[index];
            
            // Calculate expansion direction (radial outward from origin)
            float2 direction = math.normalize(inputVertex);
            
            // If vertex is at origin, can't expand radially - skip it
            if (math.length(inputVertex) < 1e-6f)
            {
                OutputVertices[index] = inputVertex;
                return;
            }
            
            // Calculate expanded position
            float2 expandedVertex = inputVertex + (direction * expansionAmount);
            
            float contribution = 1.0f;
            
            if (hasGlobalMask && globalContributionMask.IsCreated)
            {
                contribution = SampleGlobalMask(inputVertex, textureSize, globalContributionMask);
            }
            
            // Blend between original and expanded position
            float2 finalVertex = math.lerp(inputVertex, expandedVertex, contribution);
            
            OutputVertices[index] = finalVertex;
        }
        
        // Sample from 2D texture using Cartesian coordinates
        private float SampleGlobalMask(float2 position, int texSize, NativeArray<float> mask)
        {
            // Convert world position to texture UV coordinates [0, 1]
            // Assuming the texture represents a [-1, 1] world space
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

    [Node.CreateNodeMenu("Vector/Circle Expander")]
    public class VectorCircleExpanderNode : BaseNode, IVectorOutput
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public VectorPort vectorInput;

        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public VectorPort output;

        [Header("Expansion Parameters")]
        [Range(-0.5f, 0.5f)]
        [Tooltip("Amount to expand the circle (positive = expand, negative = contract)")]
        public float expansionAmount = 0.1f;

        public override object GetValue(NodePort port)
        {
            if (port?.fieldName == "output" || port == null)
            {
                return this;
            }
            return null;
        }

        public JobHandle ScheduleVector(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref VectorData outputBuffer)
        {
            var vectorNode = GetInputValue<BaseNode>(nameof(vectorInput));

            if (!(vectorNode is IVectorOutput vectorOutput))
            {
                Debug.LogError($"{GetType().Name}: No valid Vector input connected!");
                return dependency;
            }

            var context = GetContext();
            
            // Create temp buffer for input vector
            var inputVectorBuffer = new VectorData(outputBuffer.Vertices.Length);
            tempBuffers.AddVectorData(inputVectorBuffer);

            // Schedule the input vector
            JobHandle vectorHandle = vectorOutput.ScheduleVector(dependency, textureSize, tempBuffers, ref inputVectorBuffer);

            // Create and schedule the expansion job
            var expandJob = new VectorExpandJob
            {
                InputVertices = inputVectorBuffer.Vertices,
                OutputVertices = outputBuffer.Vertices,
                textureSize = textureSize,
                expansionAmount = this.expansionAmount,
                globalContributionMask = context.hasGlobalMask ? context.globalContributionMask : default,
                hasGlobalMask = context.hasGlobalMask
            };

            JobHandle expandHandle = expandJob.Schedule(inputVectorBuffer.Count, 64, vectorHandle);
            
            // Set output vertex count to match input
            outputBuffer.SetVertexCount(inputVectorBuffer.Count);

            return expandHandle;
        }
    }
}