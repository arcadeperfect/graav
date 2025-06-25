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
        public RenderTexture WarpedSdfTexture { get; private set; }
        public ComputeBuffer SegmentsBuffer { get; private set; }
        public ComputeBuffer SegmentColorsBuffer { get; private set; }
        public ComputeBuffer SegmentCountBuffer { get; private set; }
        public ComputeBuffer DrawArgsBuffer { get; private set; }
        public RenderTexture SdfTexture { get; private set; }

        private int fieldResolution;
        private int textureResolution;

        int gridResolution = 64;
        int maxSegmentsPerCell = 32;

        private JumpFlooder jumpFlooder1;

        public ComputePipeline(PlanetGenMain parent)
        {
            this.parent = parent;

            // Use the provider instead of direct Resources.Load
            MarchingSquaresShader = ComputeShaderConstants.MarchingSquaresCompute.GetShader();
            if (MarchingSquaresShader == null)
            {
                Debug.LogError("Failed to load MarchingSquares shader through provider");
                return;
            }

            // Use the provider to get the kernel
            _marchingSquaresKernel = ComputeShaderConstants.MarchingSquaresCompute.GetMarchingSquaresKernel();
            if (_marchingSquaresKernel < 0)
            {
                Debug.LogError("Failed to get MarchingSquares kernel through provider");
                return;
            }

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
            SegmentsBuffer?.Dispose();
            SegmentColorsBuffer?.Dispose();
            DrawArgsBuffer?.Dispose();
            SegmentCountBuffer?.Dispose();

            // Recreate buffers
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
        }

        void InitPingPongPipelines()
        {
            var domainWarpShader = ComputeShaderConstants.PingPongDomainWarpCompute.GetShader();
            var gaussianBlurShader = ComputeShaderConstants.GaussianBlurCompute.GetShader();

            if (domainWarpShader == null || gaussianBlurShader == null)
            {
                Debug.LogError("Failed to load shaders for field preprocessing pipeline");
                return;
            }

            fieldPreprocessingPipeline = new PingPongPipeline()
                .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
                .AddStep(domainWarpShader,
                    ComputeShaderConstants.PingPongDomainWarpCompute.Kernels.Warp, conf => conf
                        .WithIterations(() => parent.domainWarpIterations)
                        .WithFloatParam("amplitude", () => parent.domainWarp)
                        .WithFloatParam("frequency", () => parent.domainWarpScale)
                )
                .AddStep(gaussianBlurShader,
                    ComputeShaderConstants.GaussianBlurCompute.Kernels.GaussianBlur,
                    conf => conf
                        .WithFloatParam("blur", () => parent.blur1)
                );
            fieldPreprocessingPipeline.Init(fieldResolution);

            var sdfDomainWarpShader = ComputeShaderConstants.PingPongDomainWarpCompute.GetShader();
            var sdfGaussianBlurShader = ComputeShaderConstants.GaussianBlurCompute.GetShader();

            if (sdfDomainWarpShader == null || sdfGaussianBlurShader == null)
            {
                Debug.LogError("Failed to load shaders for SDF domain warp pipeline");
                return;
            }

            sdfDomainWarpPingPong = new PingPongPipeline()
                .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
                .AddStep(sdfDomainWarpShader,
                    ComputeShaderConstants.PingPongDomainWarpCompute.Kernels.Warp, conf => conf
                        .WithIterations(() => parent.domainWarpIterations2)
                        .WithFloatParam("amplitude", () => parent.domainWarp2)
                        .WithFloatParam("frequency", () => parent.domainWarpScale2)
                )
                .AddStep(sdfGaussianBlurShader,
                    ComputeShaderConstants.GaussianBlurCompute.Kernels.GaussianBlur,
                    conf => conf
                        .WithFloatParam("blur", () => parent.blur2)
                );

            sdfDomainWarpPingPong.Init(textureResolution);
        }

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

        // void GenerateSurfaceAndSDF(ComputeResources preprocessedResults, FieldGen.FieldData textures)
        // {
        //     var msShader = MarchingSquaresShader;
        //
        //     // --- Part 1: Marching Squares (Complete setup) ---
        //     SegmentsBuffer.SetCounterValue(0);
        //     SegmentColorsBuffer.SetCounterValue(0);
        //
        //     // Set all required buffers and textures for marching squares
        //     msShader.SetBuffer(_marchingSquaresKernel, "SegmentsBuffer", SegmentsBuffer);
        //     msShader.SetBuffer(_marchingSquaresKernel, "SegmentColorsBuffer", SegmentColorsBuffer);
        //     msShader.SetTexture(_marchingSquaresKernel, "ScalarFieldTexture", preprocessedResults.Textures["field"]);
        //     msShader.SetTexture(_marchingSquaresKernel, "ColorFieldTexture", textures.Colors);
        //     msShader.SetFloat("IsoValue", 0.5f);
        //     msShader.SetInt("TextureWidth", fieldResolution);
        //     msShader.SetInt("TextureHeight", fieldResolution);
        //
        //     int fieldThreadGroups = Mathf.CeilToInt(fieldResolution / 8.0f);
        //     msShader.Dispatch(_marchingSquaresKernel, fieldThreadGroups, fieldThreadGroups, 1);
        //     ComputeBuffer.CopyCount(SegmentsBuffer, SegmentCountBuffer, 0);
        //
        //
        //     var scalarFieldForSDF = preprocessedResults.Textures["field"];
        //
        //     if (parent.seedMode == 1) // Original Brute-Force Path
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

            var scalarFieldForSDF = preprocessedResults.Textures["field"];

            // Enhanced seed mode selection
            switch (parent.seedMode)
            {
                case 0: // Scalar Field Path (original - fast but potentially jaggy)
                    // Debug.Log("0");
                    jumpFlooder1.GenerateSeedsFromScalarField(scalarFieldForSDF, 0.5f);
                    jumpFlooder1.RunJumpFlood();
                    jumpFlooder1.FinalizeSDF(SdfTexture, false, scalarFieldForSDF, 0.5f);
                    break;

                case 1: // Original Brute-Force Path (slow but high quality)
                    // Debug.Log("1");
                    jumpFlooder1.GenerateSeedsFromSegments(SegmentsBuffer, SegmentCountBuffer);
                    jumpFlooder1.RunJumpFlood();
                    jumpFlooder1.FinalizeSDF(SdfTexture, false, scalarFieldForSDF, 0.5f);
                    break;

                case 2: // NEW: High-Quality JFA Path (fast and high quality)
                    // Debug.Log("2");
                    // Calculate appropriate seed distance based on resolutions
                    float maxSeedDistance = CalculateOptimalSeedDistance();
                    jumpFlooder1.GenerateSeedsFromSegmentsHQ(SegmentsBuffer, SegmentCountBuffer, maxSeedDistance);
                    jumpFlooder1.RunJumpFlood();
                    jumpFlooder1.FinalizeSDF(SdfTexture, false, scalarFieldForSDF, 0.5f);
                    break;

                default:
                    // Fallback to mode 2
                    goto case 2;
            }
        }
        /// <summary>
        /// Calculates optimal seeding distance based on texture resolution and field resolution
        /// </summary>
        private float CalculateOptimalSeedDistance()
        {
            // Base the seed distance on the relationship between field and texture resolution
            float resolutionRatio = (float)textureResolution / fieldResolution;
    
            // For higher texture resolutions relative to field resolution, we need larger seed distances
            // This ensures we capture enough detail around the contours
            float baseSeedDistance = 2.0f / textureResolution; // Base distance in world space
            float adaptiveFactor = Mathf.Max(1.0f, resolutionRatio * 0.5f);
    
            return baseSeedDistance * adaptiveFactor;
        }

        public void Dispose()
        {
            SegmentsBuffer?.Dispose();
            SegmentColorsBuffer?.Dispose();
            DrawArgsBuffer?.Dispose();
            SegmentCountBuffer?.Dispose();
            if (SdfTexture != null) SdfTexture.Release();
            if (WarpedSdfTexture != null) WarpedSdfTexture.Release();

            fieldPreprocessingPipeline?.Dispose();
            sdfDomainWarpPingPong?.Dispose();

            jumpFlooder1?.Dispose();
        }
    }
}