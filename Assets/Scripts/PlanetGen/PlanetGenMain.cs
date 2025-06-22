using System;
using System.Collections.Generic;
using UnityEditor.UI;
using UnityEngine;

namespace PlanetGen
{
    public class PlanetGenMain : MonoBehaviour
    {
        [Header("Field")] public int fieldWidth = 1024;
        [Range(0, 0.5f)] public float radius = 0.5f;
        [Range(0, 10f)] public float amplitude = 0.5f;
        [Range(0, 10f)] public float frequency = 0.5f;
        [Range(0, 5)] public int blur;

        [Header("Compute Shaders")] public float iso = 0.5f;

        [Header("SDF")] public int textureRes = 1024;
        [Range(0, 1f)] public float lineWidth;
        public float bandSpacing = 0.05f; // Distance between band centers
        public float maxDistance = 0.2f; // How far from contour to show bands
        public float domainWarp = 0f;
        public float domainWarpScale;
        public int domainWarpIterations = 1;

        [Header("Debug")] public bool enableDebugDraw = false;
        public Color debugLineColor = Color.red;
        public float debugLineDuration = 0.1f;
        public int maxDebugSegments = 20000; // Limit to avoid performance issues
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
            public float Iso, LineWidth;
            public int TextureRes;
            public float DomainWarp, DomainWarpScale; // Add these
            public int DomainWarpIterations; // Add this

            public bool HasChanged(PlanetGenMain genMain)
            {
                return
                    !Mathf.Approximately(Iso, genMain.iso) ||
                    !Mathf.Approximately(LineWidth, genMain.lineWidth) ||
                    !Mathf.Approximately(DomainWarp, genMain.domainWarp) || // Add
                    !Mathf.Approximately(DomainWarpScale, genMain.domainWarpScale) || // Add  
                    TextureRes != genMain.textureRes ||
                    DomainWarpIterations != genMain.domainWarpIterations; // Add
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
                // print("regen field");
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
            computePipeline.Dispatch(field_textures, iso, lineWidth);


            resultRenderer.material.SetTexture("_SDFTexture", computePipeline.SdfTexture);
            resultRenderer.material.SetTexture("_ColorTexture", field_textures.Colors); // Add this line
            resultRenderer.material.SetFloat("_LineWidth", lineWidth * 0.1f);
            resultRenderer.material.SetFloat("_ShowBands", 1.0f);
            resultRenderer.material.SetFloat("_BandSpacing", bandSpacing * 0.1f); // Distance between band centers
            resultRenderer.material.SetFloat("_MaxDistance", maxDistance); // How far from contour to show bands
        }

        public void Update()
        {
            if (cachedFieldParams.HasChanged(this))
            {
                print("cachedFieldParams has changed");
                computePipeline.Init(fieldWidth, textureRes);
                RegenField();
            }
            else if (cachedComputeParams.HasChanged(this))
            {
                print("cachedComputeParams has changed");
                RegenCompute();
            }

            if (enableDebugDraw)
            {
                DebugDrawMarchingSquaresBuffer();
            }

            UpdateCachedParams();
        }


        public void OnDestroy()
        {
            computePipeline?.Dispose();
            fieldGen?.Dispose();
            // todo implement dispose on fieldGen
        }

        void UpdateCachedParams()
        {
            cachedFieldParams = new CachedFieldParams
            {
                FieldWidth = this.fieldWidth,
                Radius = this.radius,
                Amplitude = this.amplitude,
                Frequency = this.frequency,
                Blur = this.blur, TextureRes = this.textureRes
            };

            cachedComputeParams = new CachedComputeParams()
            {
                Iso = this.iso,
                LineWidth = this.lineWidth,
                TextureRes = this.textureRes,
                DomainWarp = this.domainWarp, // Add
                DomainWarpScale = this.domainWarpScale, // Add
                DomainWarpIterations = this.domainWarpIterations // Add
            };
        }

        private class ComputePipeline : System.IDisposable
        {
            private PlanetGenMain parent;
            private int _marchingSquaresKernel;
            private int _sdfKernel;
            private int _sdfWarperKernel;

            private ComputeShader MarchingSquaresShader;
            private ComputeShader SdfGeneratorShader;
            private ComputeShader SdfDomainWarpShader;

            private PingPongPipeline domainWarpPingPong;
            private PingPongPipeline jumpFloodPingPong;

            public ComputeBuffer SegmentsBuffer { get; private set; }

            public ComputeBuffer
                SegmentColorsBuffer { get; private set; } // Note the struct for this is 8 floats (32 bytes)

            public ComputeBuffer SegmentCountBuffer { get; private set; } // To hold the segment count
            public ComputeBuffer DrawArgsBuffer { get; private set; }
            public RenderTexture SdfTexture { get; private set; }
            public RenderTexture SdfTextureWarped { get; private set; }

            public ComputePipeline(PlanetGenMain parent)
            {
                this.parent = parent;
                MarchingSquaresShader = Resources.Load<ComputeShader>("Compute/MarchingSquares");
                if (MarchingSquaresShader == null) Debug.LogWarning("shader is null");

                // SdfGeneratorShader = Resources.Load<ComputeShader>("Compute/GenerateSDF_raycast");
                SdfGeneratorShader = Resources.Load<ComputeShader>("Compute/GenerateSDF_scalarSample");
                if (SdfGeneratorShader == null) Debug.LogWarning("shader is null");

                SdfDomainWarpShader = Resources.Load<ComputeShader>("Compute/DomainWarpSDF");
                if (SdfGeneratorShader == null) Debug.LogWarning("shader is null");

                _marchingSquaresKernel = MarchingSquaresShader.FindKernel("MarchingSquares");
                _sdfKernel = SdfGeneratorShader.FindKernel("GenerateSDF");
                _sdfWarperKernel = SdfDomainWarpShader.FindKernel("Warp");
            }

            /// <summary>
            /// Initializes or re-initializes all compute buffers.
            /// Called when the field width changes or at the start.
            /// </summary>
            public void Init(int newFieldWidth, int newTextureRes)
            {
                InitBuffers(newFieldWidth, newTextureRes);
                InitPingPongPipelines();
                BindStaticResources();
                
            }

            void InitBuffers(int fieldWidth, int textureRes)
            {
                DisposeBuffers();

                int maxSegments = fieldWidth * fieldWidth * 2;

                SegmentsBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 4, ComputeBufferType.Append);
                SegmentColorsBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 8, ComputeBufferType.Append);
                SegmentCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

                DrawArgsBuffer = new ComputeBuffer(1, sizeof(int) * 4, ComputeBufferType.IndirectArguments);
                DrawArgsBuffer.SetData(new int[] { 0, 1, 0, 0 });

                if (SdfTexture != null) SdfTexture.Release();
                SdfTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBFloat);
                SdfTexture.enableRandomWrite = true;
                SdfTexture.Create();

                if (SdfTextureWarped != null) SdfTextureWarped.Release();
                SdfTextureWarped = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBFloat);
                SdfTextureWarped.enableRandomWrite = true;
                SdfTextureWarped.Create();
            }

            private ComputeResources domainWarpOutput;
            void InitPingPongPipelines()
            {
                domainWarpPingPong = new PingPongPipeline()
                    .WithResources(spec => spec.AddTexture("field", RenderTextureFormat.ARGBFloat))
                    .AddStep(Resources.Load<ComputeShader>("Compute/pingPong1/domainWarp"), "Warp", conf => conf
                        .WithIterations(() => parent.domainWarpIterations)
                        .WithFloatParam("amplitude", () => parent.domainWarp)
                        .WithFloatParam("frequency", () => parent.domainWarpScale)
                    );
                domainWarpPingPong.Init(parent.textureRes);

                jumpFloodPingPong = new PingPongPipeline()
                    .WithResources(spec =>
                        spec.AddTexture("distanceField", RenderTextureFormat.ARGBFloat)
                            .AddTexture("edges", RenderTextureFormat.ARGB32))
                    .AddStep(Resources.Load<ComputeShader>("Compute/pingPong1/jumpFlood"), "JumpFlood", conf => conf
                        .WithIterations(() => 1)
                    );
                jumpFloodPingPong.Init(parent.textureRes);
            }

            void BindStaticResources()
            {
                var msShader = MarchingSquaresShader;
                msShader.SetBuffer(_marchingSquaresKernel, "SegmentsBuffer", SegmentsBuffer);
                msShader.SetBuffer(_marchingSquaresKernel, "SegmentColorsBuffer", SegmentColorsBuffer);
                
                var sdfShader = SdfGeneratorShader;
                sdfShader.SetBuffer(_sdfKernel, "_Segments", SegmentsBuffer);
                sdfShader.SetTexture(_sdfKernel, "_SDFTexture", SdfTexture);
                
                sdfShader.SetBuffer(_sdfKernel, "_SegmentCount", SegmentCountBuffer);

            }
            
            /// <summary>
            /// Sets shader parameters and executes the Marching Squares compute shader.
            /// </summary>
            public void Dispatch(FieldGen.TextureRegistry textures, float iso, float lineWidth)
            {
                if (SegmentsBuffer == null) return;

                if (domainWarpPingPong == null)
                {
                    Debug.LogError("testPingPong is null - make sure InitPingPong() was called");
                    return;
                }


                try
                {
                    var input = new ComputeResources();
                    input.Textures["field"] = textures.ScalarField;

                    // Execute ping-pong pipeline
                    var domainWarpResults = domainWarpPingPong.Dispatch(input);

                    domainWarpPingPong.Dispatch(input);

                    // Reset buffer counters
                    SegmentsBuffer.SetCounterValue(0);
                    SegmentColorsBuffer.SetCounterValue(0);

                    // // Setup and dispatch Marching Squares shader
                    var msShader = MarchingSquaresShader;
                    msShader.SetTexture(_marchingSquaresKernel, "ScalarFieldTexture", domainWarpResults.Textures["field"]);
                    msShader.SetTexture(_marchingSquaresKernel, "ColorFieldTexture", textures.Colors);
                    msShader.SetFloat("IsoValue", iso);
                    msShader.SetInt("TextureWidth", parent.fieldWidth);
                    msShader.SetInt("TextureHeight", parent.fieldWidth);

                    int threadGroups = Mathf.CeilToInt(parent.fieldWidth / 8.0f);
                    msShader.Dispatch(_marchingSquaresKernel, threadGroups, threadGroups, 1);

                    // Copy segment count for later use
                    ComputeBuffer.CopyCount(SegmentsBuffer, SegmentCountBuffer, 0);

                    // Setup and dispatch SDF Generator shader
                    var sdfShader = SdfGeneratorShader;
                    sdfShader.SetTexture(_sdfKernel, "_ScalarField", domainWarpResults.Textures["field"]);
                    sdfShader.SetFloat("_IsoValue", iso);
                    sdfShader.SetInt("_FieldResolution", parent.fieldWidth);
                    sdfShader.SetInt("_TextureResolution", parent.textureRes);

                    int sdfThreadGroups = Mathf.CeilToInt(parent.textureRes / 8.0f);
                    sdfShader.Dispatch(_sdfKernel, sdfThreadGroups, sdfThreadGroups, 1);
                }
                finally
                {
                }
            }

            void DisposeBuffers()
            {
                SegmentsBuffer?.Dispose();
                SegmentColorsBuffer?.Dispose();
                DrawArgsBuffer?.Dispose();
                SegmentCountBuffer?.Dispose();
                if (SdfTexture != null) SdfTexture.Release();
            }

            public void Dispose()
            {
                DisposeBuffers();
            }
        }

        #region Debug

        /// <summary>
        /// Debug draws the marching squares segments as lines in the scene view
        /// </summary>
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
                // Assuming your field spans from -fieldWidth/2 to +fieldWidth/2 in world units
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