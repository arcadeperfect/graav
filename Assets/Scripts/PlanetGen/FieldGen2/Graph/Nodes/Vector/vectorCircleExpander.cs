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
            // Get input vertex in polar coordinates (angle, radius)
            float2 inputVertex = InputVertices[index];
            float angle = inputVertex.x;
            float radius = inputVertex.y;

            // Calculate expanded radius (uniform expansion)
            float expandedRadius = radius + expansionAmount;
            expandedRadius = math.clamp(expandedRadius, 0f, 1f);
            
            // Sample global contribution mask at vertex position in texture space
            float contribution = 1.0f; // Default to full contribution
            
            if (hasGlobalMask && globalContributionMask.IsCreated)
            {
                contribution = SampleGlobalMask(angle, radius);
            }
            
            // Blend between original and expanded based on global contribution
            float finalRadius = math.lerp(radius, expandedRadius, contribution);
            
            // Output final vertex in polar coordinates
            OutputVertices[index] = new float2(angle, finalRadius);
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