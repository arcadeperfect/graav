using System;
using System.Linq;
using PlanetGen.Core;
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

        private int _markTilesKernel;
        private int _distanceFieldKernel;
        
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
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            _parent = parent;

            var loadResult = LoadShaders();
            if (!loadResult.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to create ComputePipeline: {loadResult.ErrorMessage}");
            }

            var componentResult = CreatePipelineComponents();
            if (!componentResult.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Failed to create ComputePipeline components: {componentResult.ErrorMessage}");
            }
        }

        private Result LoadShaders()
        {
            return ErrorHandler.TryExecute("ComputePipeline.LoadShaders", () =>
            {
                _marchingSquaresShader = CSP.MarchingSquares.Get();
                if (_marchingSquaresShader == null)
                {
                    throw new InvalidOperationException("Failed to load MarchingSquares shader");
                }

                _marchingSquaresKernel = CSP.MarchingSquares.Kernels.MarchingSquares;
                if (_marchingSquaresKernel < 0)
                    throw new InvalidOperationException("Failed to load MarchingSquares kernel");
            });
        }

        private Result CreatePipelineComponents()
        {
            return ErrorHandler.TryExecute("ComputePipeline.CreategPipelineComponents", () =>
            {
                _jumpFlooder = _resources.Track(new JumpFlooder());
                _udfGen = _resources.Track(new UdfFromSegments());
            });
        }
        
        public Result Init(int fieldWidth, int textureRes, int gridResolution, int maxSegmentsPerCell)
        {
            var validation =
                PipelineValidators.ValidateComputePipelineInit(fieldWidth, textureRes, gridResolution,
                    maxSegmentsPerCell);

            if (!validation.IsValid)
            {
                ErrorHandler.LogValidationResult("ComputePipeline.Init", validation);
                return Result.Failure($"Parameter validation failed: {validation.GetSummary()}");
            }

            if (validation.HasWarnings)
            {
                ErrorHandler.LogValidationResult("ComputePipeline.Init", validation);
            }

            _fieldResolution = fieldWidth;
            _textureResolution = textureRes;

            var initResult = ErrorHandler.TryExecute("ComputePipeline.Init", () =>
                {
                    CreateBuffers(fieldWidth);
                    CreateTextures(textureRes);
                    InitializePipelines();
                    InitializeSubComponents(textureRes, gridResolution, maxSegmentsPerCell);
                    _isInitialized = true;
                    var counts = _resources.GetResourceCounts();
                    Debug.Log(
                        $"ComputePipeline initialized successfully: {counts.buffers} buffers, {counts.textures} textures, {counts.disposables} other resources");
                }
            );

            if (!initResult.IsSuccess)
            {
                _isInitialized = false;
                ClearResources();
            }

            return initResult;
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


        /// <summary>
        /// Generate line segments from the original scalar field using marching squares
        /// </summary>
        /// <param name="fieldData"></param>
        void GenerateSegments(DeformableFieldData fieldData)
        {
            if (_marchingSquaresShader == null)
                throw new InvalidOperationException("MarchingSquares shader is not loaded");

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
            if (_jumpFlooder == null)
                throw new InvalidOperationException("JumpFlooder is not initialized");
            _jumpFlooder.GenerateSeedsFromScalarField(scalarField, 0.5f);
            _jumpFlooder.RunJumpFlood();
            _jumpFlooder.FinalizeSDF(JumpFloodSdfTexture, false, scalarField, 0.5f);
        }

        /// <summary>
        /// Apply domain warping to the course SDF to create interesting interior bands
        /// </summary>
        void GenerateWarpedSDF()
        {
            if (_sdfDomainWarpPingPong == null)
                throw new InvalidOperationException("SDF domain warp pipeline is not initialized");
            var sdfInput = new ComputeResources();
            sdfInput.Textures["field"] = JumpFloodSdfTexture;

            var warpedSdfResults = _sdfDomainWarpPingPong.Dispatch(sdfInput);


            WarpedSdfTexture = warpedSdfResults.Textures["field"];
        }

        void GenerateSurfaceUdf(int gridResolution)
        {
            if (_udfGen == null)
                throw new InvalidOperationException("UDF generator is not initialized");

            if (_parent.bruteForce)
                _udfGen.GenerateUdf_BruteForce(SegmentsBuffer, SegmentCountBuffer, SurfaceUdfTexture);
            else
                _udfGen.GenerateUdf(SegmentsBuffer, SegmentCountBuffer, SurfaceUdfTexture);
        }

        public string GetStatus()
        {
            if(!_isInitialized)
                return "Not initialized";
            
            var counts = _resources.GetResourceCounts();
            return $"Initialized - Field: {_fieldResolution}x{_fieldResolution}, " +
                   $"Texture: {_textureResolution}x{_textureResolution}, " +
                   $"Resources: {counts.buffers} buffers, {counts.textures} textures";
        }

        public void Dispose()
        {
            ClearResources();
            _resources.Dispose();
        }


        /// <summary>
        /// Dispatch pipeline with validation and comprehensive error handling
        /// </summary>
        public Result Dispatch(DeformableFieldData workingData, int gridResolution)
        {
            // Pre-dispatch validation
            if (!_isInitialized)
                return Result.Failure("ComputePipeline must be initialized before dispatch. Call Init() first.");

            if (workingData?.IsValid != true)
                return Result.Failure("Working data is invalid or null. Ensure field data is properly generated.");

            if (gridResolution <= 0)
                return Result.Failure($"Grid resolution must be positive, got {gridResolution}");

            // Validate that working data textures are accessible
            var validation = ParameterValidator.Create()
                .ValidateTexture(workingData.ModifiedScalarTexture, "ModifiedScalarTexture")
                .ValidateTexture(workingData.ColorTexture, "ColorTexture")
                .Build();

            if (!validation.IsValid)
            {
                ErrorHandler.LogValidationResult("ComputePipeline.Dispatch", validation);
                return Result.Failure($"Working data validation failed: {validation.GetSummary()}");
            }

            // Execute dispatch with comprehensive error handling
            return ErrorHandler.TryExecute("ComputePipeline.Dispatch", () =>
            {
                GenerateSegments(workingData);
                GenerateSignedDistanceField(workingData.ModifiedScalarTexture);
                GenerateWarpedSDF();
                GenerateSurfaceUdf(gridResolution);
            });
        }
    }
}