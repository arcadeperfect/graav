using UnityEngine;


namespace PlanetGen
{
    public class PlanetGenMain : MonoBehaviour
    {
        [Header("Field Generation")] [TriggerFieldRegen] [Min(2)]
        public int scalarFieldWidth;

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
                computePipeline.Init(scalarFieldWidth, textureRes);
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
            field_textures = new FieldGen.FieldData(scalarFieldWidth);
            fieldGen = new FieldGen();
            computePipeline = new ComputePipeline(this);
            computePipeline.Init(scalarFieldWidth, textureRes);
        }


        void RegenField()
        {
            fieldGen.GetTex(ref field_textures, 0, radius, amplitude, frequency, scalarFieldWidth, blur);
            computePipeline.Init(scalarFieldWidth, textureRes);
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
            resultRenderer.material.SetTexture("_SDFTexture", computePipeline.JumpFloodSdfTexture);
            resultRenderer.material.SetTexture("_WarpedSDFTexture", computePipeline.WarpedSdfTexture);
            resultRenderer.material.SetTexture("_UDFTexture", computePipeline.SurfaceUdfTexture);
            
            // resultRenderer.material.SetTexture("_PreciseDistanceTexture", computePipeline.PreciseDistanceTexture);

            // Pass all parameters needed for procedural rendering to the shader
            resultRenderer.material.SetFloat("_LineWidth", lineWidth);
            resultRenderer.material.SetFloat("_BandLineWidth", lineWidth * 0.5f);
            resultRenderer.material.SetInt("_NumberOfBands", numberOfBands);
            resultRenderer.material.SetFloat("_BandStartOffset", bandStartOffset);
            resultRenderer.material.SetFloat("_BandInterval", bandInterval);

            resultRenderer.enabled = renderActive;


            // Set texture for SDF preview renderer
            sdfRenderer.material.SetTexture("_SDFTex", computePipeline.JumpFloodSdfTexture);
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