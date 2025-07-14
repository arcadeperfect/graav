using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;
using PlanetGen.FieldGen2.Graph.Nodes.Base; // Needed for BaseNode, IPlanetDataOutput
using PlanetGen.FieldGen2.Graph.Types;
using Unity.Burst; // Needed for RasterData, TempBufferManager

namespace PlanetGen.FieldGen2.Graph.Nodes.Raster
{
    /// <summary>
    /// A raster node that fills the entire color channel of a RasterData with a specified color.
    /// It can optionally take an input RasterData; if connected, it passes through scalar, altitude, and angle data.
    /// If unconnected, it generates a new raster with only the specified color.
    /// </summary>
    [CreateNodeMenu("Raster/Fill Color")]
    [NodeTint("#8888FF")] // A light blue tint for raster generator/source nodes
    public class FillColorNode : BaseNode, IPlanetDataOutput
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        [Tooltip("Optional: Connect an input RasterData to pass through its scalar, altitude, and angle channels. " +
                 "If left unconnected, these channels in the output will be default (zeros).")]
        public PlanetDataPort inputRaster;

        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public PlanetDataPort output;

        [SerializeField]
        [Tooltip("The color to fill all pixels with.")]
        public Color fillColor = Color.white;

        public override object GetValue(NodePort port)
        {
            if (port.fieldName == "output")
            {
                return this;
            }
            return null;
        }

        public JobHandle SchedulePlanetData(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref RasterData outputBuffer)
        {
            JobHandle currentDependency = dependency;

            // Try to get input RasterData from a connected node
            var inputNode = GetInputValue<BaseNode>(nameof(inputRaster));
            if (inputNode is IPlanetDataOutput planetDataInputNode)
            {
                // If there's an input, we need a temporary buffer to receive its data.
                // This temp buffer will then be copied to the final outputBuffer.
                RasterData tempInputRaster = new RasterData(textureSize, Allocator.TempJob);
                tempBuffers.AddPlanetData(tempInputRaster); // Ensure this temp buffer is disposed later

                // Schedule the input node's job to fill our temp input buffer
                currentDependency = planetDataInputNode.SchedulePlanetData(currentDependency, textureSize, tempBuffers, ref tempInputRaster);

                // Schedule a copy job from tempInputRaster to the final outputBuffer.
                // This carries over scalar, altitude, and angle data from the input.
                var copyJob = new CopyRasterDataJob
                {
                    InputScalar = tempInputRaster.Scalar,
                    InputAltitude = tempInputRaster.Altitude,
                    InputAngle = tempInputRaster.Angle,
                    InputColor = tempInputRaster.Color, // This will be the base for color, but overwritten shortly

                    OutputScalar = outputBuffer.Scalar,
                    OutputAltitude = outputBuffer.Altitude,
                    OutputAngle = outputBuffer.Angle,
                    OutputColor = outputBuffer.Color // The color channel will be overwritten by FillColorJob
                };
                currentDependency = copyJob.Schedule(textureSize * textureSize, 64, currentDependency);
            }
            // If no input, the 'outputBuffer' provided by FieldGen2 is already a fresh, empty RasterData.
            // We just proceed to fill its color.

            // Now, schedule the job to fill the color of the outputBuffer's color array
            var fillColorJob = new FillColorJob
            {
                TargetColor = new float4(fillColor.r, fillColor.g, fillColor.b, fillColor.a),
                OutputColors = outputBuffer.Color // Directly modify the outputBuffer's color array
            };
            return fillColorJob.Schedule(textureSize * textureSize, 64, currentDependency);
        }

        /// <summary>
        /// Unity Job to set all elements of a NativeArray<float4> to a specified color.
        /// </summary>
        [BurstCompile(CompileSynchronously = true)] // Add BurstCompile for job performance
        private struct FillColorJob : IJobParallelFor
        {
            [ReadOnly] public float4 TargetColor;
            [WriteOnly] public NativeArray<float4> OutputColors;

            public void Execute(int index)
            {
                OutputColors[index] = TargetColor;
            }
        }

        /// <summary>
        /// Unity Job to copy the contents of one RasterData to another.
        /// Used when an input raster is provided to pass through its non-color channels.
        /// </summary>
        [BurstCompile(CompileSynchronously = true)] // Add BurstCompile for job performance
        private struct CopyRasterDataJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> InputScalar;
            [ReadOnly] public NativeArray<float> InputAltitude;
            [ReadOnly] public NativeArray<float> InputAngle;
            [ReadOnly] public NativeArray<float4> InputColor; // Though this will be overridden by FillColorJob

            [WriteOnly] public NativeArray<float> OutputScalar;
            [WriteOnly] public NativeArray<float> OutputAltitude;
            [WriteOnly] public NativeArray<float> OutputAngle;
            [WriteOnly] public NativeArray<float4> OutputColor;

            public void Execute(int index)
            {
                // Only copy if the input array is valid (i.e., created).
                // RasterData constructor ensures all arrays are created, so these checks are for robustness.
                if (InputScalar.IsCreated) OutputScalar[index] = InputScalar[index];
                if (InputAltitude.IsCreated) OutputAltitude[index] = InputAltitude[index];
                if (InputAngle.IsCreated) OutputAngle[index] = InputAngle[index];
                if (InputColor.IsCreated) OutputColor[index] = InputColor[index]; // Copy color too, before it's set
            }
        }
    }
}