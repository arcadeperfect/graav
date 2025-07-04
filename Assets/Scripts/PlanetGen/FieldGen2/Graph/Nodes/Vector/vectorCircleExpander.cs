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
            float angle = inputVertex.x;
            float radius = inputVertex.y;

            float expandedRadius = radius + expansionAmount;
            expandedRadius = math.clamp(expandedRadius, 0f, 1f);
            
            float contribution = 1.0f;
            
            if (hasGlobalMask && globalContributionMask.IsCreated)
            {
                // *** CHANGED: Use bilinear filtering for smoother sampling ***
                contribution = SampleGlobalMaskBilinear(angle, radius, textureSize, globalContributionMask);
            }
            
            float finalRadius = math.lerp(radius, expandedRadius, contribution);
            
            OutputVertices[index] = new float2(angle, finalRadius);
        }
        
        // *** NEW/MODIFIED: Helper function for bilinear sampling of a 2D mask from polar coordinates ***
        private float SampleGlobalMaskBilinear(float angle, float radius, int texSize, NativeArray<float> mask)
        {
            // Convert polar vertex position to texture UV coordinates [0, 1]
            // Map from circle (radius 0-1) to square texture region
            // (x,y) from [-1,1] to (uv) from [0,1]
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

            // Clamp coordinates to valid texture range to prevent out-of-bounds access
            x0 = math.clamp(x0, 0, texSize - 1);
            y0 = math.clamp(y0, 0, texSize - 1);
            x1 = math.clamp(x1, 0, texSize - 1); // Clamp x1 to texSize - 1 for edge cases
            y1 = math.clamp(y1, 0, texSize - 1); // Clamp y1 to texSize - 1 for edge cases
            
            // Handle edge case where x1 or y1 might go out of bounds if pixelX/Y is exactly texSize-1
            // By clamping x1 and y1 as well, we ensure we always sample valid indices.
            // If x0 = texSize-1, then x1 will also be texSize-1, effectively repeating the edge pixel.

            // Get values from the 1D NativeArray, treating it as 2D
            float c00 = mask[y0 * texSize + x0];
            float c10 = mask[y0 * texSize + x1];
            float c01 = mask[y1 * texSize + x0];
            float c11 = mask[y1 * texSize + x1];

            // Bilinear interpolation (linear interpolation in X, then in Y)
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