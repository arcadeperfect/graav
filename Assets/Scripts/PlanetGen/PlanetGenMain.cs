using System;
using System.Collections.Generic;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.Animations;
using static PlanetGen.ComputeShaderConstants;

namespace PlanetGen
{
    public class PlanetGenMain : MonoBehaviour
    {
        [Header("Field Generation")] [TriggerFieldRegen] [Min(2)]
        public int fieldWidth = 1024;

        [Range(0, 0.5f)] [TriggerFieldRegen] public float radius = 0.5f;
        [Range(0, 10f)] [TriggerFieldRegen] public float amplitude = 0.5f;
        [Range(0, 10f)] [TriggerFieldRegen] public float frequency = 0.5f;
        [Range(0, 5)] [TriggerFieldRegen] public int blur;

        [Header("Field Preview")] [TriggerFieldRegen]
        public bool enableFieldPreview = true;

        [Range(0, 2)] [TriggerFieldRegen] public int fieldDisplayMode;
        [Range(0f, 1f)] [TriggerFieldRegen] public float fieldDisplayOpacity = 1f;

        [Header("Field Processing")] [TriggerComputeRegen]
        public float domainWarp = 0f;

        [TriggerComputeRegen] public float domainWarpScale;
        [TriggerComputeRegen] public int domainWarpIterations = 1;
        [TriggerComputeRegen] public float blur1 = 0.1f;

        [Header("Rendering")] [TriggerComputeRegen]
        public bool renderActive;

        [Header("SDF 1 - used to render the surface")] [Header("Preview")] [TriggerComputeRegen]
        public bool enableSdfPreview = true;

        [TriggerComputeRegen] [Range(0, 3)] public int sdfDisplayMode = 0; // 0 = SDF, 1 = Warped SDF
        [TriggerComputeRegen] public float sdfDisplayOpacity = 1f;
        [TriggerComputeRegen] public float sdfDisplayMult = 1f;

        [Header("Generation")] [TriggerComputeRegen]
        public int textureRes = 1024;

        [Range(0, 1f)] [TriggerComputeRegen] public float lineWidth;
        [Range(0, 2)] [TriggerComputeRegen] public int seedMode;

        [Header("SDF 2 - used to render bands")] [TriggerComputeRegen]
        public float domainWarp2 = 0f;

        [TriggerComputeRegen] public float domainWarpScale2;
        [TriggerComputeRegen] public int domainWarpIterations2 = 1;
        [TriggerComputeRegen] public float blur2 = 0.1f;

        [Header("Bands")] [TriggerComputeRegen]
        public int numberOfBands = 5;

        [TriggerComputeRegen] public float bandStartOffset = -0.05f;
        [TriggerComputeRegen] public float bandInterval = 0.02f;

        [Header("Debug")] public bool enableDebugDraw = false;
        public Color debugLineColor = Color.red;
        public float debugLineDuration = 0.1f;
        public int maxDebugSegments = 20000;
        public bool computeConstantly;

        // Core systems
        private FieldGen fieldGen;
        private ComputePipeline computePipeline;
        private ParameterWatcher paramWatcher;

        public Renderer fieldRenderer;
        public Renderer sdfRenderer;
        public Renderer resultRenderer;
        private FieldGen.FieldData field_textures;

        public void Start()
        {
            paramWatcher = new ParameterWatcher(this);
            Init();
            RegenField();
        }

        public void Update()
        {
            var changes = paramWatcher.CheckForChanges();

            if (changes.HasFieldRegen())
            {
                computePipeline.Init(fieldWidth, textureRes);
                RegenField(); // This includes compute regen
            }
            else if (changes.HasComputeRegen() || computeConstantly)
            {
                RegenCompute();
            }

            if (enableDebugDraw)
            {
                DebugDrawMarchingSquaresBuffer();
            }
        }

        void Init()
        {
            field_textures = new FieldGen.FieldData(fieldWidth);
            fieldGen = new FieldGen();
            computePipeline = new ComputePipeline(this);
            computePipeline.Init(fieldWidth, textureRes);
        }


        void RegenField()
        {
            fieldGen.GetTex(ref field_textures, 0, radius, amplitude, frequency, fieldWidth, blur);
            computePipeline.Init(fieldWidth, textureRes);
            fieldRenderer.material.SetTexture("_FieldTex", field_textures.ScalarField);
            fieldRenderer.material.SetTexture("_ColorTex", field_textures.Colors);
            fieldRenderer.material.SetInt("_Mode", fieldDisplayMode);
            fieldRenderer.material.SetFloat("_Alpha", fieldDisplayOpacity);
            fieldRenderer.enabled = enableFieldPreview;


            RegenCompute();
        }

        void RegenCompute()
        {
            computePipeline.Dispatch(field_textures);


            // Set textures on the final renderer
            resultRenderer.material.SetTexture("_ColorTexture", field_textures.Colors);
            resultRenderer.material.SetTexture("_SDFTexture", computePipeline.SdfTexture);
            resultRenderer.material.SetTexture("_WarpedSDFTexture", computePipeline.WarpedSdfTexture);

            // Pass all parameters needed for procedural rendering to the shader
            resultRenderer.material.SetFloat("_LineWidth", lineWidth);
            resultRenderer.material.SetFloat("_BandLineWidth", lineWidth * 0.5f);
            resultRenderer.material.SetInt("_NumberOfBands", numberOfBands);
            resultRenderer.material.SetFloat("_BandStartOffset", bandStartOffset);
            resultRenderer.material.SetFloat("_BandInterval", bandInterval);

            resultRenderer.enabled = renderActive;


            // Set texture for SDF preview renderer
            sdfRenderer.material.SetTexture("_SDFTex", computePipeline.SdfTexture);
            sdfRenderer.material.SetInt("_Mode", sdfDisplayMode);
            sdfRenderer.material.SetFloat("_Alpha", sdfDisplayOpacity);
            sdfRenderer.material.SetFloat("_Mult", sdfDisplayMult);
            sdfRenderer.enabled = enableSdfPreview;
        }

        public void OnDestroy()
        {
            computePipeline?.Dispose();
            fieldGen?.Dispose();
        }


        // private class ComputePipeline : IDisposable
        // {
        //     private PlanetGenMain parent;
        //     private int _marchingSquaresKernel;
        //
        //     private ComputeShader MarchingSquaresShader;
        //
        //     private PingPongPipeline fieldPreprocessingPipeline; // Renamed and enhanced
        //     private PingPongPipeline sdfDomainWarpPingPong;
        //
        //     // This will now be the output of the domain warp pass
        //     public RenderTexture WarpedSdfTexture { get; private set; }
        //
        //     // Original surface buffers (Band buffers are now gone)
        //     public ComputeBuffer SegmentsBuffer { get; private set; }
        //     public ComputeBuffer SegmentColorsBuffer { get; private set; }
        //     public ComputeBuffer SegmentCountBuffer { get; private set; }
        //     public ComputeBuffer DrawArgsBuffer { get; private set; }
        //     public ComputeBuffer gridCountsBuffer { get; private set; }
        //     public ComputeBuffer gridIdxBuffer { get; private set; }
        //     public RenderTexture SdfTexture { get; private set; }
        //
        //     private int fieldResolution;
        //     private int textureResolution;
        //     
        //     int gridResolution = 64;
        //     int maxSegmentsPerCell = 32;
        //
        //     private JumpFlooder jumpFlooder1; // We only need one JumpFlooder now
        //
        //     public ComputePipeline(PlanetGenMain parent)
        //     {
        //         this.parent = parent;
        //         MarchingSquaresShader = Resources.Load<ComputeShader>(MarchingSquaresCompute.Path);
        //         if (MarchingSquaresShader == null) Debug.LogWarning("MarchingSquares shader is null");
        //         _marchingSquaresKernel =
        //             MarchingSquaresShader.FindKernel(MarchingSquaresCompute.Kernels.MarchingSquares);
        //
        //         // Only one JumpFlooder is needed for the main SDF
        //         jumpFlooder1 = new JumpFlooder();
        //     }
        //
        //     public void Init(int newFieldWidth, int newTextureRes)
        //     {
        //         fieldResolution = newFieldWidth;
        //         textureResolution = newTextureRes;
        //
        //         InitBuffers(newFieldWidth, newTextureRes);
        //         InitPingPongPipelines();
        //
        //         jumpFlooder1.InitJFATextures(newTextureRes);
        //     }
        //
        //     void InitBuffers(int fieldWidth, int textureRes)
        //     {
        //         // Dispose of any existing buffers first
        //         SegmentsBuffer?.Dispose();
        //         SegmentColorsBuffer?.Dispose();
        //         DrawArgsBuffer?.Dispose();
        //         SegmentCountBuffer?.Dispose();
        //
        //         // Recreate essential buffers
        //         int maxSegments = fieldWidth * fieldWidth * 2;
        //         SegmentsBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 4, ComputeBufferType.Append);
        //         SegmentColorsBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 8, ComputeBufferType.Append);
        //         SegmentCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        //         DrawArgsBuffer = new ComputeBuffer(1, sizeof(int) * 4, ComputeBufferType.IndirectArguments);
        //         DrawArgsBuffer.SetData(new int[] { 0, 1, 0, 0 });
        //
        //         // Recreate textures
        //         if (SdfTexture != null) SdfTexture.Release();
        //         SdfTexture =
        //             new RenderTexture(textureRes, textureRes, 0,
        //                     RenderTextureFormat.ARGBHalf) // Using Half for better performance
        //                 {
        //                     enableRandomWrite = true,
        //                     filterMode = FilterMode.Bilinear
        //                 };
        //         SdfTexture.Create();
        //
        //         // This texture will hold the warped SDF for the bands
        //         if (WarpedSdfTexture != null) WarpedSdfTexture.Release();
        //         WarpedSdfTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBHalf)
        //         {
        //             enableRandomWrite = true,
        //             filterMode = FilterMode.Bilinear
        //         };
        //         WarpedSdfTexture.Create();
        //
        //
        //
        //         gridCountsBuffer =
        //             new ComputeBuffer(gridResolution * gridResolution, sizeof(uint), ComputeBufferType.Raw);
        //         gridIdxBuffer = new ComputeBuffer(gridResolution * gridResolution * maxSegmentsPerCell, sizeof(uint));
        //     }
        //
        //     void InitPingPongPipelines()
        //     {
        //         // Enhanced field preprocessing pipeline with domain warp AND blur
        //         fieldPreprocessingPipeline = new PingPongPipeline()
        //             .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
        //             .AddStep(Resources.Load<ComputeShader>(PingPongDomainWarpCompute.Path),
        //                 PingPongDomainWarpCompute.Kernels.Warp, conf => conf
        //                     .WithIterations(() => parent.domainWarpIterations)
        //                     .WithFloatParam("amplitude", () => parent.domainWarp)
        //                     .WithFloatParam("frequency", () => parent.domainWarpScale)
        //             )
        //             .AddStep(GaussianBlurCompute.Get(),
        //                 GaussianBlurCompute.Kernels.GaussianBlur,
        //                 conf => conf
        //                     .WithFloatParam("blur", () => parent.blur1)
        //             );
        //         fieldPreprocessingPipeline.Init(fieldResolution);
        //
        //         // SDF domain warp pipeline remains the same
        //         sdfDomainWarpPingPong = new PingPongPipeline()
        //             .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
        //             .AddStep(Resources.Load<ComputeShader>(PingPongDomainWarpCompute.Path),
        //                 PingPongDomainWarpCompute.Kernels.Warp, conf => conf
        //                     .WithIterations(() => parent.domainWarpIterations2)
        //                     .WithFloatParam("amplitude", () => parent.domainWarp2)
        //                     .WithFloatParam("frequency", () => parent.domainWarpScale2)
        //             )
        //             .AddStep(GaussianBlurCompute.Get(),
        //                 GaussianBlurCompute.Kernels.GaussianBlur,
        //                 conf => conf
        //                     .WithFloatParam("blur", () => parent.blur2)
        //             );
        //
        //         sdfDomainWarpPingPong.Init(textureResolution);
        //     }
        //
        //     /// <summary>
        //     /// The Dispatch method now uses the enhanced preprocessing pipeline.
        //     /// </summary>
        //     public void Dispatch(FieldGen.FieldData textures)
        //     {
        //         if (SegmentsBuffer == null) return;
        //
        //         // STEP 1: Enhanced preprocessing with domain warp AND blur
        //         var input = new ComputeResources();
        //         input.Textures["field"] = textures.ScalarField;
        //         var preprocessedResults = fieldPreprocessingPipeline.Dispatch(input);
        //
        //         // STEP 2: Generate the main surface SDF using the preprocessed field
        //         GenerateSurfaceAndSDF(preprocessedResults, textures);
        //
        //         // STEP 3: Domain warp the main SDF to create the basis for the bands
        //         var sdfInput = new ComputeResources();
        //         sdfInput.Textures["field"] = SdfTexture;
        //
        //         // Call the existing Dispatch method which returns the result
        //         var warpedSdfResults = sdfDomainWarpPingPong.Dispatch(sdfInput);
        //
        //         // Get the final texture from the ping-pong operation
        //         var warpedResultTexture = warpedSdfResults.Textures["field"];
        //
        //         // Copy the result into our persistent WarpedSdfTexture
        //         Graphics.Blit(warpedResultTexture, WarpedSdfTexture);
        //     }
        //
        //     // GenerateSurfaceAndSDF is unchanged from your refactor
        //     void GenerateSurfaceAndSDF(ComputeResources preprocessedResults, FieldGen.FieldData textures)
        //     {
        //         SegmentsBuffer.SetCounterValue(0);
        //         SegmentColorsBuffer.SetCounterValue(0);
        //         // ... marching squares for debug ...
        //         var msShader = MarchingSquaresShader;
        //         msShader.SetBuffer(_marchingSquaresKernel, "SegmentsBuffer", SegmentsBuffer);
        //         msShader.SetBuffer(_marchingSquaresKernel, "SegmentColorsBuffer", SegmentColorsBuffer);
        //         msShader.SetTexture(_marchingSquaresKernel, "ScalarFieldTexture",
        //             preprocessedResults.Textures["field"]);
        //         msShader.SetTexture(_marchingSquaresKernel, "ColorFieldTexture", textures.Colors);
        //         msShader.SetFloat("IsoValue", 0.5f);
        //         msShader.SetInt("TextureWidth", fieldResolution);
        //         msShader.SetInt("TextureHeight", fieldResolution);
        //         int fieldThreadGroups = Mathf.CeilToInt(fieldResolution / 8.0f);
        //         msShader.Dispatch(_marchingSquaresKernel, fieldThreadGroups, fieldThreadGroups, 1);
        //         ComputeBuffer.CopyCount(SegmentsBuffer, SegmentCountBuffer, 0);
        //
        //         // JFA from scalar field
        //         var scalarFieldForSDF = preprocessedResults.Textures["field"];
        //         if (parent.seedMode == 0)
        //             jumpFlooder1.GenerateSeedsFromScalarField(scalarFieldForSDF, 0.5f);
        //         else
        //             jumpFlooder1.GenerateSeedsFromSegments(SegmentsBuffer, SegmentCountBuffer);
        //         jumpFlooder1.RunJumpFlood();
        //         jumpFlooder1.FinalizeSDF(SdfTexture, false, scalarFieldForSDF, 0.5f);
        //     }
        //
        //     public void Dispose()
        //     {
        //         // Clean up everything that remains
        //         SegmentsBuffer?.Dispose();
        //         SegmentColorsBuffer?.Dispose();
        //         DrawArgsBuffer?.Dispose();
        //         SegmentCountBuffer?.Dispose();
        //         if (SdfTexture != null) SdfTexture.Release();
        //         if (WarpedSdfTexture != null) WarpedSdfTexture.Release();
        //
        //         fieldPreprocessingPipeline?.Dispose(); // Updated name
        //         sdfDomainWarpPingPong?.Dispose();
        //
        //         jumpFlooder1?.Dispose();
        //     }
        // }

        #region Debug

        public void DebugDrawMarchingSquaresBuffer()
        {
            if (!enableDebugDraw || computePipeline?.SegmentsBuffer == null ||
                computePipeline?.SegmentCountBuffer == null)
                return;

            // Get the actual number of segments generated
            int[] segmentCount = new int[1];
            computePipeline.SegmentCountBuffer.GetData(segmentCount);
            int actualSegmentCount = Mathf.Min(segmentCount[0], maxDebugSegments);

            if (actualSegmentCount <= 0)
            {
                Debug.Log("No segments to draw");
                return;
            }

            // Each segment is 4 floats: start.x, start.y, end.x, end.y
            Vector4[] segments = new Vector4[actualSegmentCount];
            computePipeline.SegmentsBuffer.GetData(segments, 0, 0, actualSegmentCount);

            // Convert from texture space [0,1] to world space and draw
            for (int i = 0; i < actualSegmentCount; i++)
            {
                Vector4 segment = segments[i];

                // Convert from normalized texture coordinates to world positions
                // Vector3 start = new Vector3(
                //     (segment.x - 0.5f),
                //     (segment.y - 0.5f),
                //     0f
                // );    

                Vector3 start = new Vector3(
                    (segment.x),
                    (segment.y),
                    0f
                );

                Vector3 end = new Vector3(
                    (segment.z),
                    (segment.w),
                    0f
                );

                // Transform to world space relative to this object
                start = transform.TransformPoint(start);
                end = transform.TransformPoint(end);
                Debug.DrawLine(start, end, debugLineColor, debugLineDuration);
            }

            Debug.Log($"Drew {actualSegmentCount} marching squares segments");
        }

        #endregion
    }
}