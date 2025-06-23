using System;
using System.Collections.Generic;
using UnityEditor.UI;
using UnityEngine;

namespace PlanetGen
{
    public class PlanetGenMain : MonoBehaviour
    {
        [Header("Field Generation")] public int fieldWidth = 1024;
        [Range(0, 0.5f)] public float radius = 0.5f;
        [Range(0, 10f)] public float amplitude = 0.5f;
        [Range(0, 10f)] public float frequency = 0.5f;
        [Range(0, 5)] public int blur;

        [Header("Pre")] public float domainWarp = 0f;
        public float domainWarpScale;
        public int domainWarpIterations = 1;

        [Header("SDF 1")] public int textureRes = 1024;
        [Range(0, 1f)] public float lineWidth;
        public float bandSpacing = 0.05f;

        [Header("SDF 2")] public float domainWarp2 = 0f;
        public float domainWarpScale2;
        public int domainWarpIterations2 = 1;

        [Header("Bands")] public int numberOfBands = 5;
        public float bandStartOffset = -0.05f;
        public float bandInterval = 0.02f;

        [Header("Debug")] public bool enableDebugDraw = false;
        public Color debugLineColor = Color.red;
        public float debugLineDuration = 0.1f;
        public int maxDebugSegments = 20000;

        private FieldGen fieldGen;
        private ComputePipeline computePipeline;
        public Renderer fieldRenderer;
        public Renderer resultRenderer;

        private FieldGen.TextureRegistry field_textures;

        private struct CachedFieldParams
        {
            public int FieldWidth, Blur, TextureRes;
            public float Radius, Amplitude, Frequency;

            public bool HasChanged(PlanetGenMain genMain)
            {
                return FieldWidth != genMain.fieldWidth
                       || Blur != genMain.blur
                       || TextureRes != genMain.textureRes
                       || !Mathf.Approximately(Radius, genMain.radius) ||
                       !Mathf.Approximately(Amplitude, genMain.amplitude) ||
                       !Mathf.Approximately(Frequency, genMain.frequency);
            }
        }

        private struct CachedComputeParams
        {
            public float LineWidth;
            public int TextureRes;
            public float DomainWarp, DomainWarpScale;
            public int DomainWarpIterations;
            public float DomainWarp2, DomainWarpScale2;
            public int DomainWarpIterations2;
            public int NumberOfBands;
            public float BandStartOffset, BandInterval;

            public bool HasChanged(PlanetGenMain genMain)
            {

                var changed = 
                    !Mathf.Approximately(LineWidth, genMain.lineWidth) ||
                    !Mathf.Approximately(DomainWarp, genMain.domainWarp) ||
                    !Mathf.Approximately(DomainWarpScale, genMain.domainWarpScale) ||
                    TextureRes != genMain.textureRes ||
                    DomainWarpIterations != genMain.domainWarpIterations ||
                    NumberOfBands != genMain.numberOfBands ||
                    !Mathf.Approximately(BandStartOffset, genMain.bandStartOffset) ||
                    !Mathf.Approximately(BandInterval, genMain.bandInterval) ||
                    !Mathf.Approximately(DomainWarp2, genMain.domainWarp2) ||
                    !Mathf.Approximately(DomainWarpScale2, genMain.domainWarpScale2) ||
                    DomainWarpIterations2 != genMain.domainWarpIterations2;
                // if (changed)
                // {
                //     print("Compute params changed");
                // }
                
                return changed;
            }
        }

        private CachedFieldParams cachedFieldParams;
        private CachedComputeParams cachedComputeParams;

        public void Start() 
        {
            print("start");
            Init();
            RegenField();
        }

        void Init()
        {
            field_textures = new FieldGen.TextureRegistry(fieldWidth);
            fieldGen = new FieldGen();
            computePipeline = new ComputePipeline(this);
            computePipeline.Init(fieldWidth, textureRes);
        }

        void RegenField()
        {
            if (cachedFieldParams.HasChanged(this))
            {
                fieldGen.GetTex(ref field_textures, 0, radius, amplitude, frequency, fieldWidth, blur);
                computePipeline.Init(fieldWidth, textureRes);
                fieldRenderer.material.SetTexture("_FieldTex", field_textures.ScalarField);
                fieldRenderer.material.SetTexture("_ColorTex", field_textures.Colors);
                UpdateCachedParams();
            }

            RegenCompute();
        }

        void RegenCompute()
        {
            computePipeline.Dispatch(field_textures);

            // Set the SDF textures
            resultRenderer.material.SetTexture("_SDFTexture", computePipeline.SdfTexture);
            resultRenderer.material.SetTexture("_BandSDFTexture", computePipeline.BandSdfTexture);

            // Set the original field color texture - this is the key addition
            resultRenderer.material.SetTexture("_ColorTexture", field_textures.Colors);

            // Set line widths
            resultRenderer.material.SetFloat("_LineWidth", lineWidth * 0.1f);
            resultRenderer.material.SetFloat("_BandLineWidth", lineWidth * 0.05f);

            // Optional: Set band color and whether to use it
            // Set to 1 to use the fixed _BandColor, set to 0 to use field colors for bands
            resultRenderer.material.SetFloat("_UseBandColor", 1.0f); // You can make this a public parameter
        }

        public void Update()
        {
            if (cachedFieldParams.HasChanged(this))
            {
                computePipeline.Init(fieldWidth, textureRes);
                RegenField(); // This already calls UpdateCachedParams
            }
            else if (cachedComputeParams.HasChanged(this))
            {
                RegenCompute();
                UpdateCachedParams(); // Update cache only after compute
            }

            if (enableDebugDraw)
            {
                // DebugDrawMarchingSquaresBuffer();
                DebugBandGeneration();
            }
        }

        public void OnDestroy()
        {
            computePipeline?.Dispose();
            fieldGen?.Dispose();
        }

        void UpdateCachedParams()
        {
            cachedFieldParams = new CachedFieldParams
            {
                FieldWidth = this.fieldWidth,
                Radius = this.radius,
                Amplitude = this.amplitude,
                Frequency = this.frequency,
                Blur = this.blur,
                TextureRes = this.textureRes
            };

            cachedComputeParams = new CachedComputeParams()
            {
                LineWidth = this.lineWidth,
                TextureRes = this.textureRes,
                DomainWarp = this.domainWarp,
                DomainWarpScale = this.domainWarpScale,
                DomainWarpIterations = this.domainWarpIterations,
                NumberOfBands = this.numberOfBands,
                BandStartOffset = this.bandStartOffset,
                BandInterval = this.bandInterval,
                DomainWarp2 = this.domainWarp2,
                DomainWarpScale2 = this.domainWarpScale2,
                DomainWarpIterations2 = this.domainWarpIterations2
            };
        }

        private class ComputePipeline : IDisposable
        {
            private PlanetGenMain parent;
            private int _marchingSquaresKernel;

            // Separate kernels for the two different SDF generation methods
            // private int _sdfKernelUnsigned;
            private int _sdfKernel;

            private ComputeShader MarchingSquaresShader;
            // private ComputeShader SdfGeneratorShader_Unsigned;
            private ComputeShader SdfGeneratorShader_Signed;

            private PingPongPipeline fieldDomainWarpPingPong;
            private PingPongPipeline sdfDomainWarpPingPong;

            // Original surface buffers
            public ComputeBuffer SegmentsBuffer { get; private set; }
            public ComputeBuffer SegmentColorsBuffer { get; private set; }
            public ComputeBuffer SegmentCountBuffer { get; private set; }
            public ComputeBuffer DrawArgsBuffer { get; private set; }
            public RenderTexture SdfTexture { get; private set; }

            // Band buffers
            public ComputeBuffer BandSegmentsBuffer { get; private set; }
            public ComputeBuffer BandSegmentColorsBuffer { get; private set; }
            public ComputeBuffer BandSegmentCountBuffer { get; private set; }
            public RenderTexture BandSdfTexture { get; private set; }

            private int fieldResolution;
            private int textureResolution;

            public ComputePipeline(PlanetGenMain parent)
            {
                this.parent = parent;
                MarchingSquaresShader = Resources.Load<ComputeShader>("Compute/MarchingSquares");
                if (MarchingSquaresShader == null) Debug.LogWarning("MarchingSquares shader is null");

                // SdfGeneratorShader_Unsigned = Resources.Load<ComputeShader>("Compute/GenerateSDF_unsigned");
                // if (SdfGeneratorShader_Unsigned == null) Debug.LogWarning("Unsigned SDF shader is null");

                SdfGeneratorShader_Signed = Resources.Load<ComputeShader>("Compute/GenerateSDF_scalarSample");
                if (SdfGeneratorShader_Signed == null) Debug.LogWarning("Signed SDF shader is null");

                _marchingSquaresKernel = MarchingSquaresShader.FindKernel("MarchingSquares");

                // Find the kernel for each SDF generation type
                // _sdfKernelUnsigned = SdfGeneratorShader_Signed.FindKernel("GenerateSDF_Unsigned");
                _sdfKernel = SdfGeneratorShader_Signed.FindKernel("GenerateSDF");
            }

            public void Init(int newFieldWidth, int newTextureRes)
            {
                fieldResolution = newFieldWidth;
                textureResolution = newTextureRes;

                InitBuffers(newFieldWidth, newTextureRes);
                InitPingPongPipelines();
            }

            void InitBuffers(int fieldWidth, int textureRes)
            {
                DisposeBuffers();

                int maxSegments = fieldWidth * fieldWidth * 2;
                int maxBandSegments = maxSegments * parent.numberOfBands;

                SegmentsBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 4, ComputeBufferType.Append);
                SegmentColorsBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 8, ComputeBufferType.Append);
                SegmentCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
                DrawArgsBuffer = new ComputeBuffer(1, sizeof(int) * 4, ComputeBufferType.IndirectArguments);
                DrawArgsBuffer.SetData(new int[] { 0, 1, 0, 0 });

                BandSegmentsBuffer = new ComputeBuffer(maxBandSegments, sizeof(float) * 4, ComputeBufferType.Append);
                BandSegmentColorsBuffer =
                    new ComputeBuffer(maxBandSegments, sizeof(float) * 8, ComputeBufferType.Append);
                BandSegmentCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

                if (SdfTexture != null) SdfTexture.Release();
                SdfTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBFloat)
                {
                    enableRandomWrite = true,
                    filterMode = FilterMode.Bilinear
                };
                SdfTexture.Create();

                if (BandSdfTexture != null) BandSdfTexture.Release();
                BandSdfTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBFloat)
                {
                    enableRandomWrite = true,
                    filterMode = FilterMode.Bilinear
                };
                BandSdfTexture.Create();
            }

            void InitPingPongPipelines()
            {
                fieldDomainWarpPingPong = new PingPongPipeline()
                    .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
                    .AddStep(Resources.Load<ComputeShader>("Compute/pingPong1/domainWarp"), "Warp", conf => conf
                        .WithIterations(() => parent.domainWarpIterations)
                        .WithFloatParam("amplitude", () => parent.domainWarp)
                        .WithFloatParam("frequency", () => parent.domainWarpScale)
                    );
                fieldDomainWarpPingPong.Init(fieldResolution);

                // This pipeline is not strictly necessary in the corrected code but is kept for consistency
                sdfDomainWarpPingPong = new PingPongPipeline()
                    .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
                    .AddStep(Resources.Load<ComputeShader>("Compute/pingPong1/domainWarp"), "Warp", conf => conf
                        .WithIterations(() => parent.domainWarpIterations2)
                        .WithFloatParam("amplitude", () => parent.domainWarp2)
                        .WithFloatParam("frequency", () => parent.domainWarpScale2)
                    );
                sdfDomainWarpPingPong.Init(textureResolution);
            }

            public void Dispatch(FieldGen.TextureRegistry textures)
            {
                if (SegmentsBuffer == null) return;

                var input = new ComputeResources();
                input.Textures["field"] = textures.ScalarField;
                var domainWarpResults = fieldDomainWarpPingPong.Dispatch(input);

                // STEP 1: Generate the main surface and its SIGNED Distance Field.
                GenerateSurfaceAndSDF(domainWarpResults, textures);


                GenerateInteriorBandSegments(SdfTexture, domainWarpResults, textures);


                // Reset for next test
                BandSegmentsBuffer.SetCounterValue(0);
                BandSegmentColorsBuffer.SetCounterValue(0);


                var sdfInput = new ComputeResources();
                sdfInput.Textures["field"] = SdfTexture;

                // Check if input texture is valid
                if (SdfTexture == null)
                {
                    Debug.LogError("SdfTexture is null before ping-pong!");
                    return;
                }

                var warpedSdfResults = sdfDomainWarpPingPong.Dispatch(sdfInput);

                // Check if output is valid
                if (warpedSdfResults == null || warpedSdfResults.Textures == null)
                {
                    Debug.LogError("warpedSdfResults is null or missing textures!");
                    return;
                }

                if (!warpedSdfResults.Textures.ContainsKey("field"))
                {
                    Debug.Log($"Available keys: {string.Join(", ", warpedSdfResults.Textures.Keys)}");
                    return;
                }

                var warpedTexture = warpedSdfResults.Textures["field"];
                if (warpedTexture == null)
                {
                    Debug.LogError("Warped SDF texture is null!");
                    return;
                }


                GenerateInteriorBandSegments(warpedTexture, domainWarpResults, textures);
                int[] countWithWarp = new int[1];
                BandSegmentCountBuffer.GetData(countWithWarp);


                // STEP 3: Generate an UNSIGNED SDF from the band segments for rendering.
                GenerateBandSDF();
            }

            void GenerateSurfaceAndSDF(ComputeResources domainWarpResults, FieldGen.TextureRegistry textures)
            {
                // Reset buffer counters
                SegmentsBuffer.SetCounterValue(0);
                SegmentColorsBuffer.SetCounterValue(0);

                // Setup and dispatch Marching Squares shader for the main surface
                var msShader = MarchingSquaresShader;
                msShader.SetBuffer(_marchingSquaresKernel, "SegmentsBuffer", SegmentsBuffer);
                msShader.SetBuffer(_marchingSquaresKernel, "SegmentColorsBuffer", SegmentColorsBuffer);
                msShader.SetTexture(_marchingSquaresKernel, "ScalarFieldTexture", domainWarpResults.Textures["field"]);
                msShader.SetTexture(_marchingSquaresKernel, "ColorFieldTexture", textures.Colors);
                msShader.SetFloat("IsoValue", 0.5f);
                msShader.SetInt("TextureWidth", fieldResolution);
                msShader.SetInt("TextureHeight", fieldResolution);

                int fieldThreadGroups = Mathf.CeilToInt(fieldResolution / 8.0f);
                msShader.Dispatch(_marchingSquaresKernel, fieldThreadGroups, fieldThreadGroups, 1);

                ComputeBuffer.CopyCount(SegmentsBuffer, SegmentCountBuffer, 0);

                // **CRITICAL CHANGE**: Use the SIGNED SDF generator.
                var sdfShader = SdfGeneratorShader_Signed;
                sdfShader.SetBuffer(_sdfKernel, "_Segments", SegmentsBuffer);
                sdfShader.SetTexture(_sdfKernel, "_SDFTexture", SdfTexture);
                sdfShader.SetBuffer(_sdfKernel, "_SegmentCount", SegmentCountBuffer);

                // The signed SDF shader needs the original field and isovalue to determine inside/outside.
                sdfShader.SetTexture(_sdfKernel, "_ScalarField", domainWarpResults.Textures["field"]);
                sdfShader.SetFloat("_IsoValue", 0.5f);
                sdfShader.SetInt("_TextureResolution", textureResolution);

                int textureThreadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
                sdfShader.Dispatch(_sdfKernel, textureThreadGroups, textureThreadGroups, 1);
            }

            void GenerateInteriorBandSegments(RenderTexture signedSdf, ComputeResources domainWarpResults,
                FieldGen.TextureRegistry textures)
            {
                BandSegmentsBuffer.SetCounterValue(0);
                BandSegmentColorsBuffer.SetCounterValue(0);

                var msShader = MarchingSquaresShader;

                for (int i = 0; i < parent.numberOfBands; i++)
                {
                    // Use negative iso values to trace contours INSIDE the main shape.
                    // Ensure bandStartOffset and bandInterval are negative in the Inspector.
                    float isoValue = parent.bandStartOffset + (i * parent.bandInterval);

                    msShader.SetBuffer(_marchingSquaresKernel, "SegmentsBuffer", BandSegmentsBuffer);
                    msShader.SetBuffer(_marchingSquaresKernel, "SegmentColorsBuffer", BandSegmentColorsBuffer);

                    // Use the SIGNED SDF as the scalar field to find the iso-surfaces (the bands).
                    msShader.SetTexture(_marchingSquaresKernel, "ScalarFieldTexture", signedSdf);
                    // Use the original color field so bands get colored correctly.
                    msShader.SetTexture(_marchingSquaresKernel, "ColorFieldTexture", textures.Colors);

                    msShader.SetFloat("IsoValue", isoValue);
                    msShader.SetInt("TextureWidth", textureResolution); // Marching squares now runs on the SDF texture
                    msShader.SetInt("TextureHeight", textureResolution);

                    int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
                    msShader.Dispatch(_marchingSquaresKernel, threadGroups, threadGroups, 1);
                }

                ComputeBuffer.CopyCount(BandSegmentsBuffer, BandSegmentCountBuffer, 0);
            }

            void GenerateBandSDF()
            {
                var sdfShader = SdfGeneratorShader_Signed;
                sdfShader.SetBuffer(_sdfKernel, "_Segments", BandSegmentsBuffer);
                sdfShader.SetTexture(_sdfKernel, "_SDFTexture", BandSdfTexture);
                sdfShader.SetBuffer(_sdfKernel, "_SegmentCount", BandSegmentCountBuffer);
                sdfShader.SetInt("_TextureResolution", textureResolution);

                int textureThreadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
                sdfShader.Dispatch(_sdfKernel, textureThreadGroups, textureThreadGroups, 1);
            }

            void DisposeBuffers()
            {
                SegmentsBuffer?.Dispose();
                SegmentColorsBuffer?.Dispose();
                DrawArgsBuffer?.Dispose();
                SegmentCountBuffer?.Dispose();
                if (SdfTexture != null) SdfTexture.Release();

                BandSegmentsBuffer?.Dispose();
                BandSegmentColorsBuffer?.Dispose();
                BandSegmentCountBuffer?.Dispose();
                if (BandSdfTexture != null) BandSdfTexture.Release();
            }

            public void Dispose()
            {
                DisposeBuffers();
                fieldDomainWarpPingPong?.Dispose();
                sdfDomainWarpPingPong?.Dispose();
            }
        }

        #region Debug

        public void DebugBandGeneration()
        {
            if (computePipeline?.BandSegmentCountBuffer == null)
            {
                Debug.Log("Band segment count buffer is null");
                return;
            }

            // Get the actual number of band segments generated
            int[] bandSegmentCount = new int[1];
            computePipeline.BandSegmentCountBuffer.GetData(bandSegmentCount);

            Debug.Log($"Generated {bandSegmentCount[0]} band segments with {numberOfBands} bands");
            Debug.Log($"Band start offset: {bandStartOffset}, interval: {bandInterval}");

            // Check if we have any band segments to draw
            if (bandSegmentCount[0] > 0)
            {
                Debug.Log("Band segments exist - check shader parameters");
            }
            else
            {
                Debug.Log("No band segments generated - check iso values or SDF");
            }
        }

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
                Vector3 start = new Vector3(
                    (segment.x - 0.5f),
                    (segment.y - 0.5f),
                    0f
                );

                Vector3 end = new Vector3(
                    (segment.z - 0.5f),
                    (segment.w - 0.5f),
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