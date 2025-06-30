using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Initializer
{
    [BurstCompile(CompileSynchronously = true)]
    public struct InitializePlanetDataJob : IJobParallelFor
    {
        [WriteOnly] public PlanetData outputBuffer;
        
        [ReadOnly] public int textureSize;
        [ReadOnly] public float globalContribution;
        [ReadOnly] public float globalSeed;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;
            
            // Calculate normalized UV coordinates [0,1]
            float2 uv = new float2(x, y) / (float)(textureSize - 1);
            
            // Calculate distance from center (for altitude)
            float2 center = new float2(0.5f, 0.5f);
            float distanceFromCenter = math.distance(uv, center);
            
            // Calculate angle from center (for angle field)
            float2 fromCenter = uv - center;
            float angle = math.atan2(fromCenter.y, fromCenter.x);
            
            // Set all PlanetData fields
            outputBuffer.Scalar[index] = 0f; // Initialize to zero
            outputBuffer.Altitude[index] = distanceFromCenter * 2f; // Normalize to [0,1] approximately
            outputBuffer.Angle[index] = angle; // [-π, π]
            outputBuffer.Color[index] = new float4(0f, 0f, 0f, 1f); // Black with full alpha
        }
    }

    [Node.CreateNodeMenu("Generators/Initialize Planet Data")]
    public class InitializePlanetDataNode : BaseNode, IPlanetDataOutput
    {
        [Header("Initialization")]
        [Tooltip("Creates base coordinate data for planet generation")]
        public bool showInfo = true;

        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public PlanetDataPort output;

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
            var context = GetContext();
            
            var initJob = new InitializePlanetDataJob
            {
                outputBuffer = outputBuffer,
                textureSize = textureSize,
                globalContribution = context.contribution,
                globalSeed = context.seed
            };

            return initJob.Schedule(textureSize * textureSize, 64, dependency);
        }
    }
}
