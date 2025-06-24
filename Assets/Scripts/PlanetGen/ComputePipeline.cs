using System;
using UnityEngine;

namespace PlanetGen
{
    public class ComputePipeline : IDisposable
    {
        private PlanetGenMain parent;
        private int _marchingSquaresKernel;

        private ComputeShader MarchingSquaresShader;

        private PingPongPipeline fieldPreprocessingPipeline; // Renamed and enhanced
        private PingPongPipeline sdfDomainWarpPingPong;

        // This will now be the output of the domain warp pass
        public RenderTexture WarpedSdfTexture { get; private set; }

        // Original surface buffers (Band buffers are now gone)
        public ComputeBuffer SegmentsBuffer { get; private set; }
        public ComputeBuffer SegmentColorsBuffer { get; private set; }
        public ComputeBuffer SegmentCountBuffer { get; private set; }
        public ComputeBuffer DrawArgsBuffer { get; private set; }
        public ComputeBuffer gridCountsBuffer { get; private set; }
        public ComputeBuffer gridIdxBuffer { get; private set; }
        public RenderTexture SdfTexture { get; private set; }

        private int fieldResolution;
        private int textureResolution;

        int gridResolution = 64;
        int maxSegmentsPerCell = 32;

        private JumpFlooder jumpFlooder1; // We only need one JumpFlooder now

        public ComputePipeline(PlanetGenMain parent)
        {
            this.parent = parent;
            MarchingSquaresShader = Resources.Load<ComputeShader>(ComputeShaderConstants.MarchingSquaresCompute.Path);
            if (MarchingSquaresShader == null) Debug.LogWarning("MarchingSquares shader is null");
            _marchingSquaresKernel =
                MarchingSquaresShader.FindKernel(ComputeShaderConstants.MarchingSquaresCompute.Kernels.MarchingSquares);

            // Only one JumpFlooder is needed for the main SDF
            jumpFlooder1 = new JumpFlooder();
        }

        public void Init(int newFieldWidth, int newTextureRes)
        {
            fieldResolution = newFieldWidth;
            textureResolution = newTextureRes;

            InitBuffers(newFieldWidth, newTextureRes);
            InitPingPongPipelines();

            jumpFlooder1.InitJFATextures(newTextureRes);
        }

        void InitBuffers(int fieldWidth, int textureRes)
        {
            // Dispose of any existing buffers first
            SegmentsBuffer?.Dispose();
            SegmentColorsBuffer?.Dispose();
            DrawArgsBuffer?.Dispose();
            SegmentCountBuffer?.Dispose();

            // Recreate essential buffers
            int maxSegments = fieldWidth * fieldWidth * 2;
            SegmentsBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 4, ComputeBufferType.Append);
            SegmentColorsBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 8, ComputeBufferType.Append);
            SegmentCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            DrawArgsBuffer = new ComputeBuffer(1, sizeof(int) * 4, ComputeBufferType.IndirectArguments);
            DrawArgsBuffer.SetData(new int[] { 0, 1, 0, 0 });

            // Recreate textures
            if (SdfTexture != null) SdfTexture.Release();
            SdfTexture =
                new RenderTexture(textureRes, textureRes, 0,
                        RenderTextureFormat.ARGBHalf) // Using Half for better performance
                    {
                        enableRandomWrite = true,
                        filterMode = FilterMode.Bilinear
                    };
            SdfTexture.Create();

            // This texture will hold the warped SDF for the bands
            if (WarpedSdfTexture != null) WarpedSdfTexture.Release();
            WarpedSdfTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBHalf)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear
            };
            WarpedSdfTexture.Create();


            gridCountsBuffer =
                new ComputeBuffer(gridResolution * gridResolution, sizeof(uint), ComputeBufferType.Raw);
            gridIdxBuffer = new ComputeBuffer(gridResolution * gridResolution * maxSegmentsPerCell, sizeof(uint));
        }

        void InitPingPongPipelines()
        {
            // Enhanced field preprocessing pipeline with domain warp AND blur
            fieldPreprocessingPipeline = new PingPongPipeline()
                .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
                .AddStep(Resources.Load<ComputeShader>(ComputeShaderConstants.PingPongDomainWarpCompute.Path),
                    ComputeShaderConstants.PingPongDomainWarpCompute.Kernels.Warp, conf => conf
                        .WithIterations(() => parent.domainWarpIterations)
                        .WithFloatParam("amplitude", () => parent.domainWarp)
                        .WithFloatParam("frequency", () => parent.domainWarpScale)
                )
                .AddStep(ComputeShaderConstants.GaussianBlurCompute.Get(),
                    ComputeShaderConstants.GaussianBlurCompute.Kernels.GaussianBlur,
                    conf => conf
                        .WithFloatParam("blur", () => parent.blur1)
                );
            fieldPreprocessingPipeline.Init(fieldResolution);

            // SDF domain warp pipeline remains the same
            sdfDomainWarpPingPong = new PingPongPipeline()
                .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
                .AddStep(Resources.Load<ComputeShader>(ComputeShaderConstants.PingPongDomainWarpCompute.Path),
                    ComputeShaderConstants.PingPongDomainWarpCompute.Kernels.Warp, conf => conf
                        .WithIterations(() => parent.domainWarpIterations2)
                        .WithFloatParam("amplitude", () => parent.domainWarp2)
                        .WithFloatParam("frequency", () => parent.domainWarpScale2)
                )
                .AddStep(ComputeShaderConstants.GaussianBlurCompute.Get(),
                    ComputeShaderConstants.GaussianBlurCompute.Kernels.GaussianBlur,
                    conf => conf
                        .WithFloatParam("blur", () => parent.blur2)
                );

            sdfDomainWarpPingPong.Init(textureResolution);
        }

        /// <summary>
        /// The Dispatch method now uses the enhanced preprocessing pipeline.
        /// </summary>
        public void Dispatch(FieldGen.FieldData textures)
        {
            if (SegmentsBuffer == null) return;

            // STEP 1: Enhanced preprocessing with domain warp AND blur
            var input = new ComputeResources();
            input.Textures["field"] = textures.ScalarField;
            var preprocessedResults = fieldPreprocessingPipeline.Dispatch(input);

            // STEP 2: Generate the main surface SDF using the preprocessed field
            GenerateSurfaceAndSDF(preprocessedResults, textures);

            // STEP 3: Domain warp the main SDF to create the basis for the bands
            var sdfInput = new ComputeResources();
            sdfInput.Textures["field"] = SdfTexture;

            // Call the existing Dispatch method which returns the result
            var warpedSdfResults = sdfDomainWarpPingPong.Dispatch(sdfInput);

            // Get the final texture from the ping-pong operation
            var warpedResultTexture = warpedSdfResults.Textures["field"];

            // Copy the result into our persistent WarpedSdfTexture
            Graphics.Blit(warpedResultTexture, WarpedSdfTexture);
        }

        // GenerateSurfaceAndSDF is unchanged from your refactor
        // void GenerateSurfaceAndSDF(ComputeResources preprocessedResults, FieldGen.FieldData textures)
        // {
        //     SegmentsBuffer.SetCounterValue(0);
        //     SegmentColorsBuffer.SetCounterValue(0);
        //     // ... marching squares for debug ...
        //     var msShader = MarchingSquaresShader;
        //     msShader.SetBuffer(_marchingSquaresKernel, "SegmentsBuffer", SegmentsBuffer);
        //     msShader.SetBuffer(_marchingSquaresKernel, "SegmentColorsBuffer", SegmentColorsBuffer);
        //     msShader.SetTexture(_marchingSquaresKernel, "ScalarFieldTexture",
        //         preprocessedResults.Textures["field"]);
        //     msShader.SetTexture(_marchingSquaresKernel, "ColorFieldTexture", textures.Colors);
        //     msShader.SetFloat("IsoValue", 0.5f);
        //     msShader.SetInt("TextureWidth", fieldResolution);
        //     msShader.SetInt("TextureHeight", fieldResolution);
        //     int fieldThreadGroups = Mathf.CeilToInt(fieldResolution / 8.0f);
        //     msShader.Dispatch(_marchingSquaresKernel, fieldThreadGroups, fieldThreadGroups, 1);
        //     ComputeBuffer.CopyCount(SegmentsBuffer, SegmentCountBuffer, 0);
        //
        //     // JFA from scalar field
        //     var scalarFieldForSDF = preprocessedResults.Textures["field"];
        //     if (parent.seedMode == 0)
        //         jumpFlooder1.GenerateSeedsFromScalarField(scalarFieldForSDF, 0.5f);
        //     else
        //         jumpFlooder1.GenerateSeedsFromSegments(SegmentsBuffer, SegmentCountBuffer);
        //     jumpFlooder1.RunJumpFlood();
        //     jumpFlooder1.FinalizeSDF(SdfTexture, false, scalarFieldForSDF, 0.5f);
        // }
        // void GenerateSurfaceAndSDF(ComputeResources preprocessedResults, FieldGen.FieldData textures)
        // {
        //     // --- Part 1: Marching Squares (Unchanged) ---
        //     SegmentsBuffer.SetCounterValue(0);
        //     SegmentColorsBuffer.SetCounterValue(0);
        //     var msShader = MarchingSquaresShader;
        //     msShader.SetBuffer(_marchingSquaresKernel, "SegmentsBuffer", SegmentsBuffer);
        //     msShader.SetBuffer(_marchingSquaresKernel, "SegmentColorsBuffer", SegmentColorsBuffer);
        //     msShader.SetTexture(_marchingSquaresKernel, "ScalarFieldTexture",
        //         preprocessedResults.Textures["field"]);
        //     msShader.SetTexture(_marchingSquaresKernel, "ColorFieldTexture", textures.Colors);
        //     msShader.SetFloat("IsoValue", 0.5f);
        //     msShader.SetInt("TextureWidth", fieldResolution);
        //     msShader.SetInt("TextureHeight", fieldResolution);
        //     int fieldThreadGroups = Mathf.CeilToInt(fieldResolution / 8.0f);
        //     msShader.Dispatch(_marchingSquaresKernel, fieldThreadGroups, fieldThreadGroups, 1);
        //     ComputeBuffer.CopyCount(SegmentsBuffer, SegmentCountBuffer, 0);
        //
        //     // --- Part 2: Build Spatial Grid (NEW) ---
        //     // This is the new step. It runs right after we know the segments.
        //     int maxSegments = fieldResolution * fieldResolution * 2;
        //     jumpFlooder1.BuildSegmentGrid(SegmentsBuffer, SegmentCountBuffer, gridCountsBuffer, gridIdxBuffer, gridResolution, maxSegmentsPerCell, maxSegments);
        //
        //     // --- Part 3: JFA / SDF Generation (Unchanged for now) ---
        //     // This part will eventually be replaced by the RefineSDF logic,
        //     // but for now, the grid is being built and is ready to be used.
        //     var scalarFieldForSDF = preprocessedResults.Textures["field"];
        //     if (parent.seedMode == 0)
        //         jumpFlooder1.GenerateSeedsFromScalarField(scalarFieldForSDF, 0.5f);
        //     else
        //         jumpFlooder1.GenerateSeedsFromSegments(SegmentsBuffer, SegmentCountBuffer);
        //
        //     jumpFlooder1.RunJumpFlood();
        //     jumpFlooder1.FinalizeSDF(SdfTexture, false, scalarFieldForSDF, 0.5f);
        // }
        // void GenerateSurfaceAndSDF(ComputeResources preprocessedResults, FieldGen.FieldData textures)
        // {
        //     var msShader = MarchingSquaresShader;
        //
        //     // --- Part 1: Marching Squares (Unchanged) ---
        //     SegmentsBuffer.SetCounterValue(0);
        //     SegmentColorsBuffer.SetCounterValue(0);
        //     // ... (dispatch logic for marching squares is the same) ...
        //     int fieldThreadGroups = Mathf.CeilToInt(fieldResolution / 8.0f);
        //     msShader.Dispatch(_marchingSquaresKernel, fieldThreadGroups, fieldThreadGroups, 1);
        //     ComputeBuffer.CopyCount(SegmentsBuffer, SegmentCountBuffer, 0);
        //
        //     // --- Part 2: Build Spatial Grid (Now used by multiple modes) ---
        //     int maxSegments = fieldResolution * fieldResolution * 2;
        //     jumpFlooder1.BuildSegmentGrid(SegmentsBuffer, SegmentCountBuffer, gridCountsBuffer, gridIdxBuffer, gridResolution, maxSegmentsPerCell, maxSegments);
        //
        //     // --- Part 3: SDF Generation ---
        //     var scalarFieldForSDF = preprocessedResults.Textures["field"];
        //
        //     // Add a new case for the grid-accelerated method (e.g., seedMode == 2)
        //     if (parent.seedMode == 2) // NEW: Grid-Accelerated Path
        //     {
        //         // Step 1: Densely calculate the distance to the nearest segment using the grid
        //         jumpFlooder1.GenerateSeedsFromSegments_SP(SegmentsBuffer, gridCountsBuffer, gridIdxBuffer, gridResolution, maxSegmentsPerCell);
        //
        //         // Step 2: Apply the sign to the dense distance data. JFA is skipped.
        //         jumpFlooder1.FinalizeSDF_FromDense(SdfTexture, scalarFieldForSDF, 0.5f);
        //     }
        //     else if (parent.seedMode == 1) // Original Brute-Force Path
        //     {
        //         jumpFlooder1.GenerateSeedsFromSegments(SegmentsBuffer, SegmentCountBuffer);
        //         jumpFlooder1.RunJumpFlood();
        //         jumpFlooder1.FinalizeSDF(SdfTexture, false, scalarFieldForSDF, 0.5f);
        //     }
        //     else // seedMode == 0, Scalar Field Path
        //     {
        //         jumpFlooder1.GenerateSeedsFromScalarField(scalarFieldForSDF, 0.5f);
        //         jumpFlooder1.RunJumpFlood();
        //         jumpFlooder1.FinalizeSDF(SdfTexture, false, scalarFieldForSDF, 0.5f);
        //     }
        // }

        void GenerateSurfaceAndSDF(ComputeResources preprocessedResults, FieldGen.FieldData textures)
        {
            var msShader = MarchingSquaresShader;

            // --- Part 1: Marching Squares (Complete setup) ---
            SegmentsBuffer.SetCounterValue(0);
            SegmentColorsBuffer.SetCounterValue(0);

            // Set all required buffers and textures for marching squares
            msShader.SetBuffer(_marchingSquaresKernel, "SegmentsBuffer", SegmentsBuffer);
            msShader.SetBuffer(_marchingSquaresKernel, "SegmentColorsBuffer", SegmentColorsBuffer);
            msShader.SetTexture(_marchingSquaresKernel, "ScalarFieldTexture", preprocessedResults.Textures["field"]);
            msShader.SetTexture(_marchingSquaresKernel, "ColorFieldTexture", textures.Colors);
            msShader.SetFloat("IsoValue", 0.5f);
            msShader.SetInt("TextureWidth", fieldResolution);
            msShader.SetInt("TextureHeight", fieldResolution);

            int fieldThreadGroups = Mathf.CeilToInt(fieldResolution / 8.0f);
            msShader.Dispatch(_marchingSquaresKernel, fieldThreadGroups, fieldThreadGroups, 1);
            ComputeBuffer.CopyCount(SegmentsBuffer, SegmentCountBuffer, 0);

            // --- Part 2: Build Spatial Grid (Now used by multiple modes) ---
            int maxSegments = fieldResolution * fieldResolution * 2;
            var clearData = new uint[gridResolution * gridResolution];
            gridCountsBuffer.SetData(clearData);
            jumpFlooder1.BuildSegmentGrid(SegmentsBuffer, SegmentCountBuffer, gridCountsBuffer, gridIdxBuffer,
                gridResolution, maxSegmentsPerCell, maxSegments);

            // --- Part 3: SDF Generation ---
            var scalarFieldForSDF = preprocessedResults.Textures["field"];

            // Add a new case for the grid-accelerated method (e.g., seedMode == 2)
            if (parent.seedMode == 2) // NEW: Grid-Accelerated Path
            {
                // Step 1: Densely calculate the distance to the nearest segment using the grid
                jumpFlooder1.GenerateSeedsFromSegments_SP(SegmentsBuffer, gridCountsBuffer, gridIdxBuffer,
                    gridResolution, maxSegmentsPerCell);

                // Step 2: Apply the sign to the dense distance data. JFA is skipped.
                jumpFlooder1.FinalizeSDF_FromDense(SdfTexture, scalarFieldForSDF, 0.5f);
            }
            else if (parent.seedMode == 1) // Original Brute-Force Path
            {
                jumpFlooder1.GenerateSeedsFromSegments(SegmentsBuffer, SegmentCountBuffer);
                jumpFlooder1.RunJumpFlood();
                jumpFlooder1.FinalizeSDF(SdfTexture, false, scalarFieldForSDF, 0.5f);
            }
            else // seedMode == 0, Scalar Field Path
            {
                jumpFlooder1.GenerateSeedsFromScalarField(scalarFieldForSDF, 0.5f);
                jumpFlooder1.RunJumpFlood();
                jumpFlooder1.FinalizeSDF(SdfTexture, false, scalarFieldForSDF, 0.5f);
            }
        }

        public void Dispose()
        {
            // Clean up everything that remains
            SegmentsBuffer?.Dispose();
            SegmentColorsBuffer?.Dispose();
            DrawArgsBuffer?.Dispose();
            SegmentCountBuffer?.Dispose();
            if (SdfTexture != null) SdfTexture.Release();
            if (WarpedSdfTexture != null) WarpedSdfTexture.Release();

            fieldPreprocessingPipeline?.Dispose(); // Updated name
            sdfDomainWarpPingPong?.Dispose();

            jumpFlooder1?.Dispose();
        }
    }
}