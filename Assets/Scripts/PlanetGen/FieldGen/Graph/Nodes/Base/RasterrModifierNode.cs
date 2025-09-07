using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XNode;
using PlanetGen.FieldGen2.Graph.Types; // Ensure this is present

namespace PlanetGen.FieldGen2.Graph.Nodes.Base
{
    /// <summary>
    /// Base class for all nodes that take an existing RasterData as input and modify it.
    /// Handles the common logic of scheduling the input raster and setting up the output buffer.
    /// </summary>
    [NodeTint("#568551")] // Orange tint for all RasterModifierNode derived classes
    public abstract class RasterModifierNode : BaseNode, IPlanetDataOutput
    {
        [Input(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        [Tooltip("The input RasterData to be processed by this node.")]
        public PlanetDataPort rasterInput;

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
            TempBufferManager tempBuffers, ref RasterData outputBuffer)
        {
            JobHandle currentDependency = dependency;
            RasterData inputRasterBuffer = default; // Will be initialized if an input is connected

            // 1. Get and schedule the upstream raster input
            var inputNode = GetInputValue<BaseNode>(nameof(rasterInput));
            if (inputNode is IPlanetDataOutput planetDataInputNode)
            {
                // Create a temporary buffer to hold the input raster data
                inputRasterBuffer = new RasterData(textureSize, Allocator.TempJob);
                tempBuffers.AddPlanetData(inputRasterBuffer); // Ensure this temp buffer is disposed later

                // Schedule the upstream node's job to fill our temp input buffer
                currentDependency = planetDataInputNode.SchedulePlanetData(currentDependency, textureSize, tempBuffers, ref inputRasterBuffer);
            }
            else
            {
                Debug.LogError($"{GetType().Name}: No valid Raster input connected! This node requires an input raster.", this);
                // Return the current dependency. The caller (FieldGen2) will still try to complete it.
                // In a more robust system, you might want to signal failure or return default empty data.
                return currentDependency;
            }

            // 2. Allow subclasses to schedule their specific control inputs (e.g., noise, masks)
            // and pass out the filled NativeArrays to be used by the main modification job.
            SpecificInputsBuffers specificInputBuffers = new SpecificInputsBuffers();
            currentDependency = ScheduleSpecificInputs(currentDependency, textureSize, tempBuffers, ref specificInputBuffers);
            
            // 3. Schedule the main raster modification job
            var context = GetContext(); // Get the evaluation context
            return ScheduleRasterModificationJob(currentDependency, textureSize, tempBuffers, inputRasterBuffer, ref specificInputBuffers, ref outputBuffer, context);
        }

        /// <summary>
        /// A struct to hold NativeArrays for specific inputs scheduled by subclasses.
        /// Subclasses will define what these buffers are.
        /// </summary>
        protected struct SpecificInputsBuffers
        {
            // Example for RasterDomainWarpNode:
            public NativeArray<float> NoiseX;
            public NativeArray<float> NoiseY;
            // Add other specific input buffers as needed by different modifier nodes
        }

        /// <summary>
        /// Abstract method for subclasses to schedule any additional control inputs (e.g., noise, blend masks)
        /// and combine their dependencies with the provided `currentDependency`.
        /// The filled NativeArrays for these specific inputs should be returned via `out specificInputBuffers`.
        /// </summary>
        /// <param name="currentDependency">The dependency from the upstream raster input.</param>
        /// <param name="textureSize">The size of the texture (e.g., width or height).</param>
        /// <param name="tempBuffers">The temporary buffer manager to register created NativeArrays for disposal.</param>
        /// <param name="specificInputBuffers">Out parameter to return the NativeArrays filled by this method.</param>
        /// <returns>A JobHandle representing the combined dependencies of all scheduled specific inputs.</returns>
        protected abstract JobHandle ScheduleSpecificInputs(JobHandle currentDependency, int textureSize,
            TempBufferManager tempBuffers, ref SpecificInputsBuffers specificInputBuffers);

        /// <summary>
        /// Abstract method for subclasses to implement their core raster modification logic.
        /// This method should create and schedule the IJobParallelFor for raster modification.
        /// </summary>
        /// <param name="dependency">The dependency from all previous input scheduling (primary raster + specific inputs).</param>
        /// <param name="textureSize">The size of the texture (e.g., width or height).</param>
        /// <param name="tempBuffers">The temporary buffer manager.</param>
        /// <param name="inputRaster">The input raster data (already scheduled).</param>
        /// <param name="specificInputBuffers">The NativeArrays holding data from specific inputs, filled by ScheduleSpecificInputs.</param>
        /// <param name="outputRaster">The output raster data (to be written to).</param>
        /// <param name="context">The evaluation context containing global parameters.</param>
        /// <returns>The JobHandle for the completed raster modification job.</returns>
        protected abstract JobHandle ScheduleRasterModificationJob(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, RasterData inputRaster, ref SpecificInputsBuffers specificInputBuffers, ref RasterData outputRaster, EvaluationContext context);
    }
}