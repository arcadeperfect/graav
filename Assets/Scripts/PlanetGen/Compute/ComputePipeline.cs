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
        private UdfFromSegments udfGen;

        private int markTilesKernel;
        private int distanceFieldKernel;

        
        public ComputeBuffer SegmentsBuffer { get; private set; }
        public ComputeBuffer SegmentColorsBuffer { get; private set; }
        public ComputeBuffer SegmentCountBuffer { get; private set; }
        public ComputeBuffer DrawArgsBuffer { get; private set; }
        
        public RenderTexture JumpFloodSdfTexture { get; private set; } // Keep for bands
        public RenderTexture WarpedSdfTexture { get; private set; }
        public RenderTexture SurfaceUdfTexture { get; private set; }

        private int fieldResolution;
        private int textureResolution;

        // int gridResolution = 32;
        // int maxSegmentsPerCell = 64;
        
        

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
            
            // udfGen = new UdfFromSegments(gridResolution, maxSegmentsPerCell);
            udfGen = new UdfFromSegments();

        }

        public void Init(int newFieldWidth, int newTextureRes, int newGridResolution, int maxSegmentsPerCell)
        {
            fieldResolution = newFieldWidth;
            textureResolution = newTextureRes;

            InitBuffers(newFieldWidth, newTextureRes);
            InitPingPongPipelines();

            jumpFlooder.InitJFATextures(newTextureRes);
            udfGen.Init(newGridResolution, maxSegmentsPerCell );
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
            if (JumpFloodSdfTexture) JumpFloodSdfTexture.Release();
            JumpFloodSdfTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBHalf)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear
            };
            JumpFloodSdfTexture.Create();
            

            // Warped SDF for bands
            if (WarpedSdfTexture) WarpedSdfTexture.Release();
            WarpedSdfTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBHalf)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear
            };
            WarpedSdfTexture.Create();
            
            if (SurfaceUdfTexture) SurfaceUdfTexture.Release();
            SurfaceUdfTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                // useMipMap = true,          // Add this
                // autoGenerateMips = true    // Add this
            };
            SurfaceUdfTexture.Create();
        }


        void InitPingPongPipelines()
        {
            var domainWarpShader = CSP.DomainWarp.GetShader();
            var warpKernel = CSP.DomainWarp.Kernels.Warp;

            var gaussianBlurShader = CSP.GaussianBlur.GetShader();
            var blurKernel = CSP.GaussianBlur.Kernels.GaussianBlur;

            if (!domainWarpShader || !gaussianBlurShader)
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

        public void Dispatch(FieldGen.FieldData textures, int gridResolution)
        {
            if (SegmentsBuffer == null) return;

            // STEP 1: Enhanced preprocessing with domain warp AND blur
            var input = new ComputeResources();
            input.Textures["field"] = textures.ScalarField;
            var preprocessedResults = fieldPreprocessingPipeline.Dispatch(input);


            GenerateSegments(preprocessedResults, textures);

            GenerateSignedDistanceField(preprocessedResults.Textures["field"]);

            GenerateWarpedSDF();
            
            GenerateSurfaceUDF(gridResolution);

        }

        /// <summary>
        /// Generate line segments from the scalar field using marching squares
        /// </summary>
        /// <param name="preprocessedResults"></param>
        /// <param name="textures"></param>
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

        /// <summary>
        /// Use jump flood to create a SDF directly from the scalar field
        /// Bypasses the segments from marching squares
        /// Is fast, but results in low quality surface contours if the texture res is higher than the field res
        /// </summary>
        /// <param name="scalarField"></param>
        void GenerateSignedDistanceField(RenderTexture scalarField)
        {
            jumpFlooder.GenerateSeedsFromScalarField(scalarField, 0.5f);
            jumpFlooder.RunJumpFlood();
            jumpFlooder.FinalizeSDF(JumpFloodSdfTexture, false, scalarField, 0.5f);
        }

        /// <summary>
        /// Apply domain warping to the course SDF to create interesting interior bands
        /// </summary>
        void GenerateWarpedSDF()
        {
            var sdfInput = new ComputeResources();
            sdfInput.Textures["field"] = JumpFloodSdfTexture;

            var warpedSdfResults = sdfDomainWarpPingPong.Dispatch(sdfInput);
            
            if (WarpedSdfTexture != null && WarpedSdfTexture != warpedSdfResults.Textures["field"])
                WarpedSdfTexture.Release();
            
            WarpedSdfTexture = warpedSdfResults.Textures["field"];
        }

        void GenerateSurfaceUDF(int gridResolution)
        {
            // Graphics.Blit(JumpFloodSdfTexture, SurfaceUdfTexture);
            if (parent.bruteForce)
                udfGen.GenerateUdf_BruteForce(SegmentsBuffer, SegmentCountBuffer, SurfaceUdfTexture);
            else
                udfGen.GenerateUdf(SegmentsBuffer, SegmentCountBuffer, SurfaceUdfTexture);
                
            // udfGen.GenerateUdf(SegmentsBuffer, SegmentCountBuffer, SurfaceUdfTexture);
        }
        
        public void Dispose()
        {
            SegmentsBuffer?.Dispose();
            SegmentColorsBuffer?.Dispose();
            DrawArgsBuffer?.Dispose();
            SegmentCountBuffer?.Dispose();

            if (JumpFloodSdfTexture != null) JumpFloodSdfTexture.Release();
            if (WarpedSdfTexture != null) WarpedSdfTexture.Release();
            

            fieldPreprocessingPipeline?.Dispose();
            sdfDomainWarpPingPong?.Dispose();

            jumpFlooder?.Dispose();
            udfGen?.Dispose();

        }
    }
}