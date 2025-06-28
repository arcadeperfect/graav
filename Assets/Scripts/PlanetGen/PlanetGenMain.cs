using System.Collections.Generic;
using System.Diagnostics;
using PlanetGen.Compute;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PlanetGen
{
    public class PlanetGenMain : MonoBehaviour
    {
        #region Inspector

        [Header("Dispplays")] [TriggerFieldRegen]
        public bool enableFieldPreview = true;

        [TriggerComputeRegen] public bool enableSdfPreview = true;
        [TriggerComputeRegen] public bool renderActive;
        public bool enableDebugDraw = false;

        [Header("Field Generation")] [TriggerFieldRegen]
        public int scalarFieldWidth;

        [Range(0, 0.5f)] [TriggerFieldRegen] public float radius = 0.5f;
        [Range(0, 50f)] [TriggerFieldRegen] public float amplitude = 0.5f;
        [Range(0, 10f)] [TriggerFieldRegen] public float frequency = 0.5f;
        [Range(0, 5)] [TriggerFieldRegen] public int blur;

        [Header("Field Preview")] [Range(0, 2)] [TriggerFieldRegen]
        public int fieldDisplayMode;

        [Range(0f, 1f)] [TriggerFieldRegen] public float fieldDisplayOpacity = 1f;

        [Header("CPU Marching Squares")] [TriggerFieldRegen]
        public bool enableCPUMarchingSquares = true;

        [TriggerFieldRegen] [Range(0.1f, 0.9f)]
        public float marchingSquaresThreshold = 0.5f;

        [TriggerFieldRegen] public bool useFastMethod = true;
        [TriggerFieldRegen] public int batchSize = 4;
        [TriggerFieldRegen] public bool showPerformanceStats = true;


        // [Header("Field Processing")] [TriggerComputeRegen]
        // public float domainWarp = 0f;
        //
        // [TriggerComputeRegen] public float domainWarpScale;
        // [TriggerComputeRegen] public int domainWarpIterations = 1;
        // [TriggerComputeRegen] public float blur1 = 0.1f;

        [Header("SDF 1 - used to render the surface")] [Header("Preview")] [TriggerComputeRegen] [Range(0, 3)]
        public int sdfDisplayMode = 0;

        [TriggerComputeRegen] public float sdfDisplayOpacity = 1f;
        [TriggerComputeRegen] public float sdfDisplayMult = 1f;

        [Header("GPU Compute")] [TriggerBufferReInit]
        public int textureRes = 1024;

        public bool bruteForce = false;
        [TriggerBufferReInit] public int gridResolution;
        [TriggerBufferReInit] public int maxSegmentsPerCell;
        [Range(0, 1f)] [TriggerComputeRegen] public float lineWidth;

        [Header("SDF 2 - used to render bands")] [TriggerComputeRegen]
        public float domainWarp2 = 0f;

        [TriggerComputeRegen] public float domainWarp2Scale;
        [TriggerComputeRegen] public int domainWarp2Iterations = 1;
        [TriggerComputeRegen] public float blur2 = 0.1f;

        [Header("Bands")] [TriggerComputeRegen]
        public int numberOfBands = 5;

        [Range(-0.5f, 0.5f)] [TriggerComputeRegen]
        public float bandStartOffset = -0.05f;

        [Range(-0.5f, 0.5f)] [TriggerComputeRegen]
        public float bandInterval = 0.02f;

        [Header("Debug")] public bool showCPUSegments = true;
        public bool showGPUSegments = true;


        public Color debugLineColor = Color.red;
        public Color cpuDebugLineColor = Color.blue;
        public float debugLineDuration = 0.1f;
        public int maxDebugSegments = 20000;
        public bool computeConstantly;


        [Header("CPU Marching Squares Enhanced")] [TriggerFieldRegen]
        public bool enablePolylineGeneration = false;

        [TriggerFieldRegen] public bool createColliders = false;
        [TriggerFieldRegen] public PhysicsMaterial2D colliderMaterial;
        [TriggerFieldRegen] public float colliderThickness = 0.1f;

        #endregion

        #region Members

        // Core systems
        private FieldGen.FieldGen fieldGen;
        private ComputePipeline computePipeline;
        private ParameterWatcher paramWatcher;

        private List<Vector4> cpuSegments;

        public Renderer fieldRenderer;
        public Renderer sdfRenderer;
        public Renderer resultRenderer;
        public GameObject collidersObject;

        private FieldGen.FieldGen.FieldData field_textures;


        // Performance tracking
        private float lastCPUMarchingSquaresTime;
        private int lastCPUSegmentCount;
        private float lastPolylineGenerationTime;
        private int lastPolylineCount;

        #endregion

        #region Unity Lifecycle

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
                computePipeline.Init(scalarFieldWidth, textureRes, gridResolution, maxSegmentsPerCell);
                RegenField();
            }
            else if (changes.HasComputeRegen() || computeConstantly)
            {
                RegenCompute();
            }

            if (changes.HasBufferReInit())
            {
                computePipeline.Init(scalarFieldWidth, textureRes, gridResolution, maxSegmentsPerCell);
                RegenCompute();
            }

            if (enableDebugDraw)
            {
                DebugDraw();
            }
        }

        public void OnDestroy()
        {
            computePipeline?.Dispose();
            fieldGen?.Dispose();
            field_textures.Dispose();
        }

        #endregion

        #region Pipeline

        void Init()
        {
            field_textures = new FieldGen.FieldGen.FieldData(scalarFieldWidth);
            fieldGen = new FieldGen.FieldGen();
            computePipeline = new ComputePipeline(this);
            computePipeline.Init(scalarFieldWidth, textureRes, gridResolution, maxSegmentsPerCell);
            cpuSegments = new List<Vector4>();
        }

        void RegenField()
        {
            // Generate the field data
            fieldGen.GetTex(ref field_textures, 0, radius, amplitude, frequency, scalarFieldWidth, blur);

            computePipeline.Init(scalarFieldWidth, textureRes, gridResolution, maxSegmentsPerCell);
            fieldRenderer.material.SetTexture("_FieldTex", field_textures.ScalarFieldTexture);
            fieldRenderer.material.SetTexture("_ColorTex", field_textures.Colors);
            fieldRenderer.material.SetInt("_Mode", fieldDisplayMode);
            fieldRenderer.material.SetFloat("_Alpha", fieldDisplayOpacity);
            fieldRenderer.enabled = enableFieldPreview;

            RegenCompute();
        }


        void RegenCompute()
        {
            if (enableCPUMarchingSquares && field_textures.IsDataValid)
            {
                var stopwatch = new Stopwatch();
                if(showPerformanceStats) stopwatch.Start();

                // Execute the CPU Marching Squares algorithm
                (var segments, _) = Compute.MarchingSquaresCPU.GenerateSegments(field_textures, marchingSquaresThreshold);
                cpuSegments = segments;
                
                if(showPerformanceStats)
                {
                    stopwatch.Stop();
                    lastCPUMarchingSquaresTime = stopwatch.ElapsedMilliseconds;
                    lastCPUSegmentCount = cpuSegments.Count;
                }
            }

            computePipeline.Dispatch(field_textures, gridResolution);

            resultRenderer.material.SetTexture("_ColorTexture", field_textures.Colors);
            resultRenderer.material.SetTexture("_SDFTexture", computePipeline.JumpFloodSdfTexture);
            resultRenderer.material.SetTexture("_WarpedSDFTexture", computePipeline.WarpedSdfTexture);
            resultRenderer.material.SetTexture("_UDFTexture", computePipeline.SurfaceUdfTexture);

            resultRenderer.material.SetFloat("_LineWidth", lineWidth * 0.01f);
            resultRenderer.material.SetFloat("_BandLineWidth", lineWidth * 0.01f);
            resultRenderer.material.SetInt("_NumberOfBands", numberOfBands);
            resultRenderer.material.SetFloat("_BandStartOffset", bandStartOffset);
            resultRenderer.material.SetFloat("_BandInterval", bandInterval);
            resultRenderer.material.SetFloat("_SurfaceBrightness", 1);
            resultRenderer.material.SetFloat("_BandBrightness", 1);

            resultRenderer.enabled = renderActive;

            sdfRenderer.material.SetTexture("_SDFTex", computePipeline.SurfaceUdfTexture);
            sdfRenderer.material.SetInt("_Mode", sdfDisplayMode);
            sdfRenderer.material.SetFloat("_Alpha", sdfDisplayOpacity);
            sdfRenderer.material.SetFloat("_Mult", sdfDisplayMult);
            sdfRenderer.enabled = enableSdfPreview;
        }

        #endregion


        #region Debug

        public void DebugDraw()
        {
            if (showGPUSegments && computePipeline?.SegmentsBuffer != null &&
                computePipeline?.SegmentCountBuffer != null)
            {
                DrawGPUMarchingSquares();
            }
            if (showCPUSegments && cpuSegments != null)
            {
                DrawCPUMarchingSquares();
            }
        }
        private void DrawCPUMarchingSquares()
        {
            int actualSegmentCount = Mathf.Min(cpuSegments.Count, maxDebugSegments);

            for (int i = 0; i < actualSegmentCount; i++)
            {
                Vector4 segment = cpuSegments[i];
                Vector3 start = new Vector3(segment.x, segment.y, 0f);
                Vector3 end = new Vector3(segment.z, segment.w, 0f);

                start = transform.TransformPoint(start);
                end = transform.TransformPoint(end);
                Debug.DrawLine(start, end, cpuDebugLineColor, debugLineDuration);
            }
        }

        private void DrawGPUMarchingSquares()
        {
            int[] segmentCount = new int[1];
            computePipeline.SegmentCountBuffer.GetData(segmentCount);
            int actualSegmentCount = Mathf.Min(segmentCount[0], maxDebugSegments);

            if (actualSegmentCount <= 0) return;

            Vector4[] segments = new Vector4[actualSegmentCount];
            computePipeline.SegmentsBuffer.GetData(segments, 0, 0, actualSegmentCount);

            for (int i = 0; i < actualSegmentCount; i++)
            {
                Vector4 segment = segments[i];
                Vector3 start = new Vector3(segment.x, segment.y, 0f);
                Vector3 end = new Vector3(segment.z, segment.w, 0f);

                start = transform.TransformPoint(start);
                end = transform.TransformPoint(end);
                Debug.DrawLine(start, end, debugLineColor, debugLineDuration);
            }
        }

        #endregion
    }
}