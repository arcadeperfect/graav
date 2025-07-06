using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Outputs
{
    [BurstCompile(CompileSynchronously = true)]
    public struct NoiseVisualizerJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> noiseInput;
        [WriteOnly] public RasterData outputBuffer;
        
        [ReadOnly] public int textureSize;
        [ReadOnly] public float colorBrightness;
        [ReadOnly] public bool useAbsoluteValue;
        [ReadOnly] public bool remapToPositive;
        [ReadOnly] public float globalContribution;
        [ReadOnly] public float globalSeed;

        public void Execute(int index)
        {
            // Bounds checking
            if (index >= noiseInput.Length || index >= outputBuffer.Scalar.Length)
            {
                return;
            }

            float noiseValue = noiseInput[index];
            
            // Apply absolute value if requested
            if (useAbsoluteValue)
            {
                noiseValue = math.abs(noiseValue);
            }
            
            // Remap from [-1,1] to [0,1] if requested
            if (remapToPositive)
            {
                noiseValue = noiseValue * 0.5f + 0.5f;
            }
            
            // Clamp to [0,1] for safety
            noiseValue = math.clamp(noiseValue, 0f, 1f);
            
            // Apply brightness multiplier and global contribution
            noiseValue *= colorBrightness * globalContribution;
            
            // Calculate 2D coordinates for generating basic coordinate data
            int x = index % textureSize;
            int y = index / textureSize;
            
            // Generate normalized UV coordinates [0,1]
            float2 uv = new float2(x, y) / (float)(textureSize - 1);
            
            // Calculate distance from center (for altitude)
            float2 center = new float2(0.5f, 0.5f);
            float distanceFromCenter = math.distance(uv, center);
            
            // Calculate angle from center (for angle field)
            float2 fromCenter = uv - center;
            float angle = math.atan2(fromCenter.y, fromCenter.x);
            
            // Set all PlanetData fields
            outputBuffer.Scalar[index] = noiseValue;
            outputBuffer.Altitude[index] = distanceFromCenter * 2f; // Normalize to [0,1] approximately
            outputBuffer.Angle[index] = angle; // [-π, π]
            outputBuffer.Color[index] = new float4(noiseValue, noiseValue, noiseValue, 1.0f); // Grayscale
        }
    }

    [Node.CreateNodeMenu("Utility/Noise Visualizer")]
    public class NoiseVisualizerNode : BaseNode, IPlanetDataOutput
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public FloatPort noiseInput;

        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public PlanetDataPort output;

        [Header("Visualization Options")]
        [Range(0f, 2f)]
        [Tooltip("Brightness multiplier for the visualization")]
        public float colorBrightness = 1f;
        
        [Tooltip("Whether to use absolute value of noise (no negative values)")]
        public bool useAbsoluteValue = false;
        
        [Tooltip("Remap noise from [-1,1] to [0,1] instead of using raw values")]
        public bool remapToPositive = true;

        public override object GetValue(NodePort port)
        {
            if (port?.fieldName == "output" || port == null)
            {
                return this;
            }
            return null;
        }

        public JobHandle SchedulePlanetData(JobHandle dependency, int textureSize, 
            TempBufferManager tempBuffers, ref RasterData outputBuffer)
        {
            var noiseNode = GetInputValue<BaseNode>(nameof(noiseInput));
            
            if (!(noiseNode is IFloatOutput floatOutput))
            {
                Debug.LogError("NoiseVisualizerNode: No valid noise input connected!");
                return dependency;
            }

            var context = GetContext();

            // Create temp buffer for noise data
            var noiseBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            tempBuffers.FloatBuffers.Add(noiseBuffer);

            // Schedule the noise generation
            JobHandle noiseHandle = floatOutput.ScheduleFloat(dependency, textureSize, tempBuffers, ref noiseBuffer);

            // Create and schedule the visualization job
            var visualizerJob = new NoiseVisualizerJob
            {
                noiseInput = noiseBuffer,
                outputBuffer = outputBuffer,
                textureSize = textureSize,
                colorBrightness = this.colorBrightness,
                useAbsoluteValue = this.useAbsoluteValue,
                remapToPositive = this.remapToPositive,
                globalContribution = context.contribution,
                globalSeed = context.seed
            };

            return visualizerJob.Schedule(textureSize * textureSize, 64, noiseHandle);
        }
    }
}