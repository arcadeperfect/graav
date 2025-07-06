using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using PlanetGen.FieldGen2.Graph.Types;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Raster
{
    [BurstCompile(CompileSynchronously = true)]
    public struct RasterDomainWarpJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> InputScalar;
        [ReadOnly] public NativeArray<float> InputAltitude;
        [ReadOnly] public NativeArray<float4> InputColor;
        [ReadOnly] public NativeArray<float> InputAngle;
        
        [ReadOnly] public NativeArray<float> WarpNoiseX;
        [ReadOnly] public NativeArray<float> WarpNoiseY;
        
        [WriteOnly] public NativeArray<float> OutputScalar;
        [WriteOnly] public NativeArray<float> OutputAltitude;
        [WriteOnly] public NativeArray<float4> OutputColor;
        [WriteOnly] public NativeArray<float> OutputAngle;

        [ReadOnly] public int textureSize;
        [ReadOnly] public float warpStrength;
        [ReadOnly] public NativeArray<float> globalContributionMask;
        [ReadOnly] public bool hasGlobalMask;

        public void Execute(int index)
        {
            int x = index % textureSize;
            int y = index / textureSize;

            // Apply global contribution mask to scale warp amplitude
            float contribution = 1.0f;
            if (hasGlobalMask && globalContributionMask.IsCreated)
            {
                contribution = globalContributionMask[index];
            }
            
            // Get warp displacement from noise, scaled by contribution mask
            float warpX = WarpNoiseX[index] * warpStrength * contribution;
            float warpY = WarpNoiseY[index] * warpStrength * contribution;

            // Calculate warped sampling position
            float sourceX = x + warpX;
            float sourceY = y + warpY;

            // Sample from the warped position using bilinear interpolation
            OutputScalar[index] = SampleBilinear(InputScalar, sourceX, sourceY, textureSize);
            OutputAltitude[index] = SampleBilinear(InputAltitude, sourceX, sourceY, textureSize);
            OutputColor[index] = SampleBilinearFloat4(InputColor, sourceX, sourceY, textureSize);
            OutputAngle[index] = SampleBilinear(InputAngle, sourceX, sourceY, textureSize);
        }

        // Bilinear sampling for float arrays
        private float SampleBilinear(NativeArray<float> data, float x, float y, int size)
        {
            // Clamp to texture bounds
            x = math.clamp(x, 0f, size - 1f);
            y = math.clamp(y, 0f, size - 1f);

            // Get integer coordinates
            int x0 = (int)math.floor(x);
            int y0 = (int)math.floor(y);
            int x1 = math.min(x0 + 1, size - 1);
            int y1 = math.min(y0 + 1, size - 1);

            // Get fractional parts
            float fx = x - x0;
            float fy = y - y0;

            // Sample the four corner pixels
            float c00 = data[y0 * size + x0];
            float c10 = data[y0 * size + x1];
            float c01 = data[y1 * size + x0];
            float c11 = data[y1 * size + x1];

            // Bilinear interpolation
            float c0 = math.lerp(c00, c10, fx);
            float c1 = math.lerp(c01, c11, fx);
            return math.lerp(c0, c1, fy);
        }

        // Bilinear sampling for float4 arrays
        private float4 SampleBilinearFloat4(NativeArray<float4> data, float x, float y, int size)
        {
            // Clamp to texture bounds
            x = math.clamp(x, 0f, size - 1f);
            y = math.clamp(y, 0f, size - 1f);

            // Get integer coordinates
            int x0 = (int)math.floor(x);
            int y0 = (int)math.floor(y);
            int x1 = math.min(x0 + 1, size - 1);
            int y1 = math.min(y0 + 1, size - 1);

            // Get fractional parts
            float fx = x - x0;
            float fy = y - y0;

            // Sample the four corner pixels
            float4 c00 = data[y0 * size + x0];
            float4 c10 = data[y0 * size + x1];
            float4 c01 = data[y1 * size + x0];
            float4 c11 = data[y1 * size + x1];

            // Bilinear interpolation
            float4 c0 = math.lerp(c00, c10, fx);
            float4 c1 = math.lerp(c01, c11, fx);
            return math.lerp(c0, c1, fy);
        }
    }

    [Node.CreateNodeMenu("Raster/Domain Warp")]
    public class RasterDomainWarpNode : BaseNode, IPlanetDataOutput
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public PlanetDataPort rasterInput;

        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public FloatPort warpNoiseX;

        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public FloatPort warpNoiseY;

        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public PlanetDataPort output;

        [Header("Domain Warp Parameters")]
        [Range(0f, 50f)]
        [Tooltip("Strength of the domain warp displacement")]
        public float warpStrength = 10f;

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
            var rasterNode = GetInputValue<BaseNode>(nameof(rasterInput));
            var noiseXNode = GetInputValue<BaseNode>(nameof(warpNoiseX));
            var noiseYNode = GetInputValue<BaseNode>(nameof(warpNoiseY));

            if (!(rasterNode is IPlanetDataOutput rasterOutput))
            {
                Debug.LogError($"{GetType().Name}: No valid Raster input connected!");
                return dependency;
            }

            if (!(noiseXNode is IFloatOutput noiseXOutput))
            {
                Debug.LogError($"{GetType().Name}: No valid X warp noise input connected!");
                return dependency;
            }

            if (!(noiseYNode is IFloatOutput noiseYOutput))
            {
                Debug.LogError($"{GetType().Name}: No valid Y warp noise input connected!");
                return dependency;
            }

            var context = GetContext();

            // Create temp buffer for input raster
            var inputRasterBuffer = new RasterData(textureSize);
            tempBuffers.AddPlanetData(inputRasterBuffer);

            // Create temp buffers for noise
            var warpNoiseXBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            var warpNoiseYBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            tempBuffers.FloatBuffers.Add(warpNoiseXBuffer);
            tempBuffers.FloatBuffers.Add(warpNoiseYBuffer);

            // Schedule input jobs
            JobHandle rasterHandle = rasterOutput.SchedulePlanetData(dependency, textureSize, tempBuffers, ref inputRasterBuffer);
            JobHandle noiseXHandle = noiseXOutput.ScheduleFloat(dependency, textureSize, tempBuffers, ref warpNoiseXBuffer);
            JobHandle noiseYHandle = noiseYOutput.ScheduleFloat(dependency, textureSize, tempBuffers, ref warpNoiseYBuffer);

            // Combine dependencies
            JobHandle combinedHandle = JobHandle.CombineDependencies(rasterHandle, noiseXHandle, noiseYHandle);

            // Create and schedule the domain warp job
            var warpJob = new RasterDomainWarpJob
            {
                InputScalar = inputRasterBuffer.Scalar,
                InputAltitude = inputRasterBuffer.Altitude,
                InputColor = inputRasterBuffer.Color,
                InputAngle = inputRasterBuffer.Angle,
                WarpNoiseX = warpNoiseXBuffer,
                WarpNoiseY = warpNoiseYBuffer,
                OutputScalar = outputBuffer.Scalar,
                OutputAltitude = outputBuffer.Altitude,
                OutputColor = outputBuffer.Color,
                OutputAngle = outputBuffer.Angle,
                textureSize = textureSize,
                warpStrength = this.warpStrength,
                globalContributionMask = context.hasGlobalMask ? context.globalContributionMask : default,
                hasGlobalMask = context.hasGlobalMask
            };

            return warpJob.Schedule(textureSize * textureSize, 64, combinedHandle);
        }
    }
}