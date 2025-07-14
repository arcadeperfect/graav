using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.IO
{
    /// <summary>
    /// Input node for the raster stage - receives rasterized vector data
    /// </summary>
    [Node.CreateNodeMenu("IO/Raster Input")]
    public class RasterInputNode : BaseNode, IPlanetDataOutput
    {
        [Output(ShowBackingValue.Never, ConnectionType.Multiple, TypeConstraint.Strict)]
        public PlanetDataPort output;

        [Header("Raster Input")]
        [Tooltip("Receives rasterized vector data from the orchestrator")]
        public bool showInfo = true;

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
            // Get external raster input from the graph
            if (graph is GeneratorGraph generatorGraph &&
                generatorGraph.TryGetRasterInput(out RasterData externalRaster))
            {
                // Copy external raster data to output buffer
                if (externalRaster.Scalar.IsCreated && outputBuffer.Scalar.IsCreated)
                {
                    NativeArray<float>.Copy(externalRaster.Scalar, outputBuffer.Scalar);
                    NativeArray<float>.Copy(externalRaster.Altitude, outputBuffer.Altitude);
                    NativeArray<float>.Copy(externalRaster.Angle, outputBuffer.Angle);
                    NativeArray<float4>.Copy(externalRaster.Color, outputBuffer.Color);
                }
                else
                {
                    Debug.LogError("RasterInputNode: Invalid raster data buffers");
                }
            }
            else
            {
                Debug.LogError("RasterInputNode: No external raster input available");
            }

            return dependency;
        }
    }

    // /// <summary>
    // /// Output node for the raster stage
    // /// </summary>
    // [Node.CreateNodeMenu("IO/Raster Output")]
    // public class RasterOutputNode : BaseNode, IPlanetDataOutput
    // {
    //     [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
    //     public PlanetDataPort input;
    //
    //     [Header("Raster Output")]
    //     [Tooltip("Outputs rasterized PlanetData")]
    //     public bool showInfo = true;
    //
    //     public override object GetValue(NodePort port)
    //     {
    //         return GetInputValue<BaseNode>(nameof(input));
    //     }
    //
    //     public JobHandle SchedulePlanetData(JobHandle dependency, int textureSize,
    //         TempBufferManager tempBuffers, ref PlanetData outputBuffer)
    //     {
    //         var inputNode = GetInputValue<BaseNode>(nameof(input));
    //         if (!(inputNode is IPlanetDataOutput planetDataOutput))
    //         {
    //             Debug.LogError("RasterOutputNode: No valid PlanetData input connected");
    //             return dependency;
    //         }
    //
    //         return planetDataOutput.SchedulePlanetData(dependency, textureSize, tempBuffers, ref outputBuffer);
    //     }
    // }
}