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

        private int markTilesKernel;
        private int distanceFieldKernel;

        private const int TILE_SIZE = 8;

        public RenderTexture WarpedSdfTexture { get; private set; }
        public RenderTexture PreciseDistanceTexture { get; private set; } // NEW: For surface contour
        public ComputeBuffer SegmentsBuffer { get; private set; }
        public ComputeBuffer SegmentColorsBuffer { get; private set; }
        public ComputeBuffer SegmentCountBuffer { get; private set; }
        public ComputeBuffer DrawArgsBuffer { get; private set; }
        public RenderTexture SdfTexture { get; private set; } // Keep for bands

        private int fieldResolution;
        private int textureResolution;

        int gridResolution = 64;
        int maxSegmentsPerCell = 32;

        private JumpFlooder jumpFlooder;

        public ComputePipeline(PlanetGenMain parent)
        {
            this.parent = parent;

            MarchingSquaresShader = CSP.MarchingSquares.Get();
            if (MarchingSquaresShader == null)
            {
                Debug.LogError("Failed to load MarchingSquares shader through provider");
                return;
            }

            _marchingSquaresKernel = CSP.MarchingSquares.Kernels.MarchingSquares;
            if (_marchingSquaresKernel < 0)
            {
                Debug.LogError("Failed to get MarchingSquares kernel through provider");
                return;
            }

            jumpFlooder = new JumpFlooder();
        }

        public void Init(int newFieldWidth, int newTextureRes)
        {
            fieldResolution = newFieldWidth;
            textureResolution = newTextureRes;

            InitBuffers(newFieldWidth, newTextureRes);
            InitPingPongPipelines();

            jumpFlooder.InitJFATextures(newTextureRes);
        }

        void InitBuffers(int fieldWidth, int textureRes)
        {
            // Dispose existing buffers
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
            SdfTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBHalf)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear
            };
            SdfTexture.Create();

            // NEW: Precise distance texture for surface contour
            if (PreciseDistanceTexture != null) PreciseDistanceTexture.Release();
            PreciseDistanceTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear
            };
            PreciseDistanceTexture.Create();

            // Warped SDF for bands
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
            var domainWarpShader = CSP.DomainWarp.Get();
            var warpKernel = CSP.DomainWarp.Kernels.Warp;

            var gaussianBlurShader = CSP.GaussianBlur.Get();
            var blurKernel = CSP.GaussianBlur.Kernels.GaussianBlur;

            if (domainWarpShader == null || gaussianBlurShader == null)
            {
                Debug.LogError("Failed to load shaders for field preprocessing pipeline");
                return;
            }

            fieldPreprocessingPipeline = new PingPongPipeline()
                .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
                .AddStep(domainWarpShader,
                    warpKernel, conf => conf
                        .WithIterations(() => parent.domainWarpIterations)
                        .WithFloatParam("amplitude", () => parent.domainWarp)
                        .WithFloatParam("frequency", () => parent.domainWarpScale)
                )
                .AddStep(gaussianBlurShader,
                    blurKernel,
                    conf => conf
                        .WithFloatParam("blur", () => parent.blur1)
                );
            fieldPreprocessingPipeline.Init(fieldResolution);

            sdfDomainWarpPingPong = new PingPongPipeline()
                .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
                .AddStep(domainWarpShader,
                    CSP.DomainWarp.Kernels.Warp, conf => conf
                        .WithIterations(() => parent.domainWarpIterations2)
                        .WithFloatParam("amplitude", () => parent.domainWarp2)
                        .WithFloatParam("frequency", () => parent.domainWarpScale2))
                .AddStep(gaussianBlurShader,
                    CSP.GaussianBlur.Kernels.GaussianBlur,
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


            GenerateSegments(preprocessedResults, textures);

            GenerateSignedDistanceField(preprocessedResults.Textures["field"]);

            // GeneratePreciseDistanceField();


            GenerateWarpedSDF();
        }

        void GenerateSegments(ComputeResources preprocessedResults, FieldGen.FieldData textures)
        {
            var msShader = MarchingSquaresShader;

            // Reset segment buffers
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
        }

        void GenerateSignedDistanceField(RenderTexture scalarField)
        {
            jumpFlooder.GenerateSeedsFromScalarField(scalarField, 0.5f);
            jumpFlooder.RunJumpFlood();
            jumpFlooder.FinalizeSDF(SdfTexture, false, scalarField, 0.5f);
        }

        void GenerateWarpedSDF()
        {
            var sdfInput = new ComputeResources();
            sdfInput.Textures["field"] = SdfTexture;

            var warpedSdfResults = sdfDomainWarpPingPong.Dispatch(sdfInput);
            var warpedResultTexture = warpedSdfResults.Textures["field"];
            Graphics.Blit(warpedResultTexture, WarpedSdfTexture);
        }

        public void Dispose()
        {
            SegmentsBuffer?.Dispose();
            SegmentColorsBuffer?.Dispose();
            DrawArgsBuffer?.Dispose();
            SegmentCountBuffer?.Dispose();`

            if (SdfTexture != null) SdfTexture.Release();
            if (WarpedSdfTexture != null) WarpedSdfTexture.Release();
            if (PreciseDistanceTexture != null) PreciseDistanceTexture.Release(); // NEW

            fieldPreprocessingPipeline?.Dispose();
            sdfDomainWarpPingPong?.Dispose();

            jumpFlooder?.Dispose();
        }
    }
}