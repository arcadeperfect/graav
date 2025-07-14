using System;
using PlanetGen.FieldGen2.Types;
using UnityEngine;

namespace PlanetGen.Compute
{
    public class ComputePipeline : IDisposable
    {
        private readonly PlanetGenMain _parent;
        private readonly ResourceTracker _resources = new();

        private ComputeShader _marchingSquaresShader;
        private int _marchingSquaresKernel;

        private JumpFlooder _jumpFlooder;
        private PingPongPipeline _sdfDomainWarpPingPong;
        private UdfFromSegments _udfGen;

        private int markTilesKernel;
        private int distanceFieldKernel;


        public ComputeBuffer SegmentsBuffer { get; private set; }
        public ComputeBuffer SegmentColorsBuffer { get; private set; }
        public ComputeBuffer SegmentCountBuffer { get; private set; }
        public ComputeBuffer DrawArgsBuffer { get; private set; }

        public RenderTexture JumpFloodSdfTexture { get; private set; }
        public RenderTexture WarpedSdfTexture { get; private set; }
        public RenderTexture SurfaceUdfTexture { get; private set; }

        private int _fieldResolution;
        private int _textureResolution;
        private bool _isInitialized = false;


        public ComputePipeline(PlanetGenMain parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            LoadShaders();
            CreatePipelineComponents();
        }

        private void LoadShaders()
        {
            _marchingSquaresShader = CSP.MarchingSquares.Get();
            if (_marchingSquaresShader == null)
                throw new InvalidOperationException("Failed to load MarchingSquares shader");

            _marchingSquaresKernel = CSP.MarchingSquares.Kernels.MarchingSquares;
            if (_marchingSquaresKernel < 0)
                throw new InvalidOperationException("Failed to get MarchingSquares kernel");
        }

        private void CreatePipelineComponents()
        {
            try
            {
                _jumpFlooder = _resources.Track(new JumpFlooder());
                _udfGen = _resources.Track(new UdfFromSegments());
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create pipeline components: {e.Message}");
                throw;
            }
    }

        public void Init(int fieldWidth, int textureRes, int gridResolution, int maxSegmentsPerCell)
        {
            if (fieldWidth <= 0) throw new ArgumentException("Field width must be positive", nameof(fieldWidth));
            if (textureRes <= 0) throw new ArgumentException("Texture resolution must be positive", nameof(textureRes));
            if (gridResolution <= 0)
                throw new ArgumentException("Grid resolution must be positive", nameof(gridResolution));
            if (maxSegmentsPerCell <= 0)
                throw new ArgumentException("Max segments per cell must be positive", nameof(maxSegmentsPerCell));

            _fieldResolution = fieldWidth;
            _textureResolution = textureRes;

            try
            {
                CreateBuffers(fieldWidth);
                CreateTextures(textureRes);
                InitializePipelines();
                InitializeSubComponents(textureRes, gridResolution, maxSegmentsPerCell);

                _isInitialized = true;

                var counts = _resources.GetResourceCounts();
                Debug.Log(
                    $"ComputePipeline initialized: {counts.buffers} buffers, {counts.textures} textures, {counts.disposables} other resources");
            }
            catch (Exception e)
            {
                Debug.LogError($"ComputePipeline initialization failed: {e.Message}");

                ClearResources();
                throw;
            }
        }

        private void CreateBuffers(int fieldWidth)
        {
            int maxSegments = fieldWidth * fieldWidth * 2;

            SegmentsBuffer = null;
            SegmentColorsBuffer = null;
            SegmentCountBuffer = null;
            DrawArgsBuffer = null;

            SegmentsBuffer = _resources.CreateBuffer(maxSegments, sizeof(float) * 4, ComputeBufferType.Append);
            SegmentColorsBuffer = _resources.CreateBuffer(maxSegments, sizeof(float) * 8, ComputeBufferType.Append);
            SegmentCountBuffer = _resources.CreateBuffer(1, sizeof(int), ComputeBufferType.Raw);
            DrawArgsBuffer = _resources.CreateBuffer(1, sizeof(int) * 4, ComputeBufferType.IndirectArguments);

            DrawArgsBuffer.SetData(new int[] { 0, 1, 0, 0 });
        }

        private void CreateTextures(int textureRes)
        {
            JumpFloodSdfTexture = null;
            WarpedSdfTexture = null;
            SurfaceUdfTexture = null;

            JumpFloodSdfTexture = _resources.CreateTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBHalf,
                enableRandomWrite: true, filterMode: FilterMode.Bilinear);
            WarpedSdfTexture = _resources.CreateTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBHalf,
                enableRandomWrite: true, FilterMode.Bilinear);
            SurfaceUdfTexture = _resources.CreateTexture(textureRes, textureRes, 0, RenderTextureFormat.RFloat,
                enableRandomWrite: true, filterMode: FilterMode.Bilinear);
        }

        private void InitializePipelines()
        {
            var domainWarpShader = CSP.DomainWarp.GetShader();
            var gaussianBlurShader = CSP.GaussianBlur.GetShader();

            if (!domainWarpShader || !gaussianBlurShader)
            {
                throw new InvalidOperationException("Failed to load shaders for SDF domain warp pipeline");
            }

            _sdfDomainWarpPingPong?.Dispose();

            _sdfDomainWarpPingPong = _resources.Track(new PingPongPipeline()
                .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
                .AddStep(domainWarpShader,
                    CSP.DomainWarp.Kernels.Warp, conf => conf.WithIterations(() => _parent.domainWarp2Iterations)
                        .WithFloatParam("amplitude", () => _parent.domainWarp2)
                        .WithFloatParam("frequency", () => _parent.domainWarp2Scale))
                .AddStep(gaussianBlurShader,
                    CSP.GaussianBlur.Kernels.GaussianBlur,
                    conf => conf.WithFloatParam("blur", () => _parent.blur2)));

            _sdfDomainWarpPingPong.Init(_textureResolution);
        }

        private void InitializeSubComponents(int textureRes, int gridResolution, int maxSegmentsPerCell)
        {
            _jumpFlooder.InitJFATextures(textureRes);
            _udfGen.Init(gridResolution, maxSegmentsPerCell);
        }

        private void ClearResources()
        {
            SegmentsBuffer = null;
            SegmentColorsBuffer = null;
            SegmentCountBuffer = null;
            DrawArgsBuffer = null;
            JumpFloodSdfTexture = null;
            WarpedSdfTexture = null;
            SurfaceUdfTexture = null;
            _sdfDomainWarpPingPong = null;
            // _sdfDomainWarpPingPong.Dispose(); //TODO: should i use dispose?

            _isInitialized = false;
        }

        public void Dispatch(DeformableFieldData workingData, int gridResolution)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("ComputePipeline must be initialized before dispatching");
            }

            if (workingData?.IsValid != true)
            {
                throw new ArgumentException("working data is invalid", nameof(workingData));
            }

            try
            {
                GenerateSegments(workingData);
                GenerateSignedDistanceField(workingData.ModifiedScalarTexture);
                GenerateWarpedSDF();
                GenerateSurfaceUdf(gridResolution);
            }
            catch (Exception e)
            {
                Debug.LogError($"ComputePipeline dispatch failed: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generate line segments from the original scalar field using marching squares
        /// </summary>
        /// <param name="fieldData"></param>
        void GenerateSegments(DeformableFieldData fieldData)
        {
            var msShader = _marchingSquaresShader;

            // Reset segment buffers
            SegmentsBuffer.SetCounterValue(0);
            SegmentColorsBuffer.SetCounterValue(0);


            msShader.SetBuffer(_marchingSquaresKernel, "SegmentsBuffer", SegmentsBuffer);
            msShader.SetBuffer(_marchingSquaresKernel, "SegmentColorsBuffer", SegmentColorsBuffer);
            msShader.SetTexture(_marchingSquaresKernel, "ScalarFieldTexture",
                fieldData.ModifiedScalarTexture); // Use original texture
            msShader.SetTexture(_marchingSquaresKernel, "ColorFieldTexture", fieldData.ColorTexture);

            msShader.SetFloat("IsoValue", 0.5f);
            msShader.SetInt("TextureWidth", _fieldResolution);
            msShader.SetInt("TextureHeight", _fieldResolution);

            int fieldThreadGroups = Mathf.CeilToInt(_fieldResolution / 8.0f);
            msShader.Dispatch(_marchingSquaresKernel, fieldThreadGroups, fieldThreadGroups, 1);
            ComputeBuffer.CopyCount(SegmentsBuffer, SegmentCountBuffer, 0);
        }

        /// <summary>
        /// Use jump flood to create a SDF directly from the original scalar field
        /// </summary>
        /// <param name="scalarField"></param>
        void GenerateSignedDistanceField(RenderTexture scalarField)
        {
            _jumpFlooder.GenerateSeedsFromScalarField(scalarField, 0.5f);
            _jumpFlooder.RunJumpFlood();
            _jumpFlooder.FinalizeSDF(JumpFloodSdfTexture, false, scalarField, 0.5f);
        }

        /// <summary>
        /// Apply domain warping to the course SDF to create interesting interior bands
        /// </summary>
        void GenerateWarpedSDF()
        {
            var sdfInput = new ComputeResources();
            sdfInput.Textures["field"] = JumpFloodSdfTexture;

            var warpedSdfResults = _sdfDomainWarpPingPong.Dispatch(sdfInput);

            if (WarpedSdfTexture != null && WarpedSdfTexture != warpedSdfResults.Textures["field"])
                WarpedSdfTexture.Release();

            WarpedSdfTexture = warpedSdfResults.Textures["field"];
        }

        void GenerateSurfaceUdf(int gridResolution)
        {
            if (_parent.bruteForce)
                _udfGen.GenerateUdf_BruteForce(SegmentsBuffer, SegmentCountBuffer, SurfaceUdfTexture);
            else
                _udfGen.GenerateUdf(SegmentsBuffer, SegmentCountBuffer, SurfaceUdfTexture);
        }

        public void Dispose()
        {
            ClearResources();
            _resources.Dispose();
        }
    }
}