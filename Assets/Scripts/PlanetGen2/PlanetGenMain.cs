using UnityEngine;
using UnityEngine.Serialization;

namespace PlanetGen2
{
    public class PlanetGenMain2 : MonoBehaviour
    {
        [Header("Field")] public int fieldWidth = 512;
        [Range(0, 0.5f)] public float radius = 0.5f;
        [Range(0, 2f)] public float amplitude = 0.5f;
        [Range(0, 10f)] public float frequency = 0.5f;
        [Range(0, 5)] public int blur;

        [Header("Compute Shaders")] public float iso = 0.5f;

        [Header("SDF")] public int textureRes = 1024;
        [Range(0, 1f)] public float lineWidth;
        public float bandSpacing = 0.05f; // Distance between band centers
        public float maxDistance = 0.2f; // How far from contour to show bands
        [Header("Debug")] public bool enableDebugDraw = false;
        public Color debugLineColor = Color.red;
        public float debugLineDuration = 0.1f;
        public int maxDebugSegments = 1000; // Limit to avoid performance issues

        public ComputeShader MarchingSqaresShader;
        public ComputeShader SdfGeneratorShader;


        private FieldGen fieldGen;

        private ComputePipeline computePipeline;

        public Renderer sdfRenderer;
        public Renderer fieldRenderer;
        public Renderer resultRenderer;

        // private global::PlanetGen.FieldGen.TextureRegistry field_textures;
        private FieldGen.TextureRegistry field_textures;

        private struct CachedFieldParams
        {
            public int FieldWidth, Blur, TextureRes;
            public float Radius, Amplitude, Frequency;

            public bool HasChanged(PlanetGenMain2 genMain2)
            {
                return FieldWidth != genMain2.fieldWidth
                       || Blur != genMain2.blur
                       || TextureRes != genMain2.textureRes
                       || !Mathf.Approximately(Radius, genMain2.radius) ||
                       !Mathf.Approximately(Amplitude, genMain2.amplitude) ||
                       !Mathf.Approximately(Frequency, genMain2.frequency);
            }
        }

        private struct CachedComputeParams
        {
            public float Iso, LineWidth;
            public int TextureRes;

            public bool HasChanged(PlanetGenMain2 genMain2)
            {
                return
                    !Mathf.Approximately(Iso, genMain2.iso) ||
                    !Mathf.Approximately(LineWidth, genMain2.lineWidth) ||
                    TextureRes != genMain2.textureRes;
            }
        }

        private CachedFieldParams cachedFieldParams;
        private CachedComputeParams cachedComputeParams;

        public void Start()
        {
            // print("start");
            Init();
            RegenField();
        }

        void Init()
        {
            field_textures = new FieldGen.TextureRegistry(fieldWidth);
            fieldGen = new FieldGen();
            computePipeline = new ComputePipeline(this);
            computePipeline.InitBuffers(fieldWidth, textureRes);
        }

        void RegenField()
        {
            if (cachedFieldParams.HasChanged(this))
            {
                // print("regen field");
                fieldGen.GetTex(ref field_textures, 0, radius, amplitude, frequency, fieldWidth, blur);
                computePipeline.InitBuffers(fieldWidth, textureRes);
                fieldRenderer.material.SetTexture("_FieldTex", field_textures.fields);
                fieldRenderer.material.SetTexture("_ColorTex", field_textures.colors);
                UpdateCachedParams();
            }

            RegenCompute();
        }


        void RegenCompute()
        {
            computePipeline.Dispatch(field_textures, iso, lineWidth);

            // debug tex
            sdfRenderer.material.SetTexture("_MainTex", computePipeline.SdfTexture);

            resultRenderer.material.SetTexture("_SDFTexture", computePipeline.SdfTexture);
            resultRenderer.material.SetTexture("_ColorTexture", field_textures.colors); // Add this line
            resultRenderer.material.SetFloat("_LineWidth", lineWidth * 0.1f);
            resultRenderer.material.SetFloat("_ShowBands", 1.0f);
            resultRenderer.material.SetFloat("_BandSpacing", bandSpacing);  // Distance between band centers
            resultRenderer.material.SetFloat("_MaxDistance", maxDistance);   // How far from contour to show bands
        }

        public void Update()
        {
            if (cachedFieldParams.HasChanged(this))
            {
                computePipeline.InitBuffers(fieldWidth, textureRes);
                RegenField();
            }
            else if (cachedComputeParams.HasChanged(this))
            {
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
                LineWidth = this.lineWidth
            };
        }

        private class ComputePipeline : System.IDisposable
        {
            private PlanetGenMain2 parent;
            private int _marchingSquaresKernel;
            private int _sdfKernel; // Kernel for the new shader

            // Buffers generated by the Marching Squares shader
            public ComputeBuffer SegmentsBuffer { get; private set; }

            public ComputeBuffer
                SegmentColorsBuffer { get; private set; } // Note the struct for this is 8 floats (32 bytes)

            public ComputeBuffer SegmentCountBuffer { get; private set; } // To hold the segment count

            // Buffer for indirect drawing arguments
            public ComputeBuffer DrawArgsBuffer { get; private set; }

            // The final output texture from the SDF pass
            public RenderTexture SdfTexture { get; private set; }

            public ComputePipeline(PlanetGenMain2 parent)
            {
                this.parent = parent;
                _marchingSquaresKernel = parent.MarchingSqaresShader.FindKernel("MarchingSquares");
                _sdfKernel = parent.SdfGeneratorShader.FindKernel("GenerateSDF");
            }

            /// <summary>
            /// Initializes or re-initializes all compute buffers.
            /// Called when the field width changes or at the start.
            /// </summary>
            public void InitBuffers(int newFieldWidth, int newTextureRes)
            {
                DisposeBuffers();

                int maxSegments = newFieldWidth * newFieldWidth * 2;

                SegmentsBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 4, ComputeBufferType.Append);

                // Your SegmentColor struct is two float4s, which is 8 floats total (32 bytes).
                SegmentColorsBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 8, ComputeBufferType.Append);

                // This buffer is used to get the count back from the Append Buffer.
                SegmentCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

                DrawArgsBuffer = new ComputeBuffer(1, sizeof(int) * 4, ComputeBufferType.IndirectArguments);
                DrawArgsBuffer.SetData(new int[] { 0, 1, 0, 0 });

                // NEW: Initialize the SDF RenderTexture
                // Format is ARGBFloat to hold distance, index, and interp factor.
                if (SdfTexture != null) SdfTexture.Release();
                SdfTexture = new RenderTexture(newTextureRes, newTextureRes, 0, RenderTextureFormat.ARGBFloat);
                SdfTexture.enableRandomWrite = true; // IMPORTANT
                SdfTexture.Create();
            }

            /// <summary>
            /// Sets shader parameters and executes the Marching Squares compute shader.
            /// </summary>
            public void Dispatch(FieldGen.TextureRegistry textures, float iso, float lineWidth)
            {
                if (SegmentsBuffer == null) return;

                // --- PASS 1: MARCHING SQUARES ---
                SegmentsBuffer.SetCounterValue(0);
                SegmentColorsBuffer.SetCounterValue(0);

                var msShader = parent.MarchingSqaresShader;
                msShader.SetBuffer(_marchingSquaresKernel, "SegmentsBuffer", SegmentsBuffer);
                msShader.SetBuffer(_marchingSquaresKernel, "SegmentColorsBuffer", SegmentColorsBuffer);
                msShader.SetTexture(_marchingSquaresKernel, "ScalarFieldTexture", textures.fields);
                msShader.SetTexture(_marchingSquaresKernel, "ColorFieldTexture", textures.colors);
                msShader.SetFloat("IsoValue", iso);
                msShader.SetInt("TextureWidth", parent.fieldWidth);
                msShader.SetInt("TextureHeight", parent.fieldWidth);

                int threadGroups = Mathf.CeilToInt(parent.fieldWidth / 8.0f);
                msShader.Dispatch(_marchingSquaresKernel, threadGroups, threadGroups, 1);

                // Get the number of segments that were actually generated.
                ComputeBuffer.CopyCount(SegmentsBuffer, SegmentCountBuffer, 0);

                // --- PASS 2: SDF GENERATION ---
                var sdfGeneratorShader = parent.SdfGeneratorShader;
                sdfGeneratorShader.SetBuffer(_sdfKernel, "_Segments", SegmentsBuffer);
                sdfGeneratorShader.SetTexture(_sdfKernel, "_SDFTexture", SdfTexture);
                sdfGeneratorShader.SetInt("_FieldResolution", parent.fieldWidth);
                sdfGeneratorShader.SetInt("_TextureResolution", parent.textureRes);
                
                // Pass the segment count to the SDF shader.
                sdfGeneratorShader.SetBuffer(_sdfKernel, "_SegmentCount", SegmentCountBuffer);


                int sdfThreadGroups = Mathf.CeilToInt(parent.textureRes / 8.0f);
                sdfGeneratorShader.Dispatch(_sdfKernel, sdfThreadGroups, sdfThreadGroups, 1);
                parent.sdfRenderer.material.mainTexture = SdfTexture;
            }

            void DisposeBuffers()
            {
                SegmentsBuffer?.Dispose();
                SegmentColorsBuffer?.Dispose();
                DrawArgsBuffer?.Dispose();
                SegmentCountBuffer?.Dispose();
                // NEW: Dispose the texture
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