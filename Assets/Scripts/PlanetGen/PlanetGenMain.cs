using System;
using PlanetGen.Compute;
using PlanetGen.MarchingSquares;
// using PlanetGen.Polylines;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
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


        [Header("Field Processing")] [TriggerComputeRegen]
        public float domainWarp = 0f;

        [TriggerComputeRegen] public float domainWarpScale;
        [TriggerComputeRegen] public int domainWarpIterations = 1;
        [TriggerComputeRegen] public float blur1 = 0.1f;

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

        #endregion

        #region Members

        // Core systems
        private FieldGen.FieldGen fieldGen;
        private ComputePipeline computePipeline;
        private ParameterWatcher paramWatcher;
        private CPUMarchingSquares cpuMarchingSquares;


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
            // var stopwatch = Stopwatch.StartNew();
            
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
            
            // stopwatch.Stop(); 
            // print($"update took {stopwatch.ElapsedMilliseconds}");
        }

        public void OnDestroy()
        {
            computePipeline?.Dispose();
            fieldGen?.Dispose();
            cpuMarchingSquares?.Dispose();


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

            // Initialize CPU marching squares and polyline generator
            cpuMarchingSquares = new CPUMarchingSquares();
        }

        void RegenField()
        {
            // Generate the field data
            fieldGen.GetTex(ref field_textures, 0, radius, amplitude, frequency, scalarFieldWidth, blur);

            // Run CPU marching squares immediately after field generation
            // if (enableCPUMarchingSquares && field_textures.IsDataValid)
            // {
            //     GenerateCPUMarchingSquares();
            // }

            // Continue with existing pipeline
            computePipeline.Init(scalarFieldWidth, textureRes, gridResolution, maxSegmentsPerCell);
            fieldRenderer.material.SetTexture("_FieldTex", field_textures.ScalarFieldTexture);
            fieldRenderer.material.SetTexture("_ColorTex", field_textures.Colors);
            fieldRenderer.material.SetInt("_Mode", fieldDisplayMode);
            fieldRenderer.material.SetFloat("_Alpha", fieldDisplayOpacity);
            fieldRenderer.enabled = enableFieldPreview;

            RegenCompute();
        }

        void GenerateCPUMarchingSquares()
        {
            // Get direct reference to the scalar field data
            var scalarData = fieldGen.GetScalarDataReference(field_textures);

            // Enhanced timing with microsecond precision
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            long startTicks = stopwatch.ElapsedTicks;

            // Add some validation
            int totalCells = (scalarFieldWidth - 1) * (scalarFieldWidth - 1);

            // Sample a few values to ensure the data is valid
            // if (scalarData.Length > 0)
            // {
            //     Debug.Log(
            //         $"Sample scalar values: [{scalarData[0]:F3}, {scalarData[scalarData.Length / 2]:F3}, {scalarData[scalarData.Length - 1]:F3}]");
            // }

            if (useFastMethod)
            {
                cpuMarchingSquares.GenerateContoursByCompaction(scalarData, scalarFieldWidth, marchingSquaresThreshold);
            }
            else
            {
                cpuMarchingSquares.GenerateContoursByPrecount(scalarData, scalarFieldWidth, marchingSquaresThreshold,
                    batchSize);
            }

            stopwatch.Stop();
            long endTicks = stopwatch.ElapsedTicks;

            // Calculate precise timing
            double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
            double elapsedMicroseconds =
                (double)(endTicks - startTicks) / System.Diagnostics.Stopwatch.Frequency * 1_000_000;

            // Store performance data
            lastCPUMarchingSquaresTime = stopwatch.ElapsedMilliseconds;
            lastCPUSegmentCount = cpuMarchingSquares.SegmentCount;

            // if (showPerformanceStats)
            // {
            //     Debug.Log(
            //         $"CPU Marching Squares: Generated {lastCPUSegmentCount:N0} segments in {elapsedMs:F3}ms ({elapsedMicroseconds:F1}μs) " +
            //         $"(Method: {(useFastMethod ? "Optimized" : "Batched")}, Field: {scalarFieldWidth}x{scalarFieldWidth})");
            //
            //     if (lastCPUSegmentCount > 0)
            //     {
            //         double segmentsPerMs = lastCPUSegmentCount / Math.Max(elapsedMs, 0.001);
            //         Debug.Log(
            //             $"Performance: {segmentsPerMs:F0} segments/ms, {elapsedMicroseconds / totalCells:F3}μs per cell");
            //     }
            //     else
            //     {
            //         Debug.LogWarning("No segments generated! Check if all scalar values are above/below threshold.");
            //     }
            //
            //     // Performance expectation check
            //     if (elapsedMs < 0.1 && totalCells > 100000)
            //     {
            //         Debug.LogWarning($"Suspiciously fast timing for {totalCells:N0} cells. " +
            //                          "This might indicate the job isn't actually running or data issues.");
            //     }
            // }

            
        }

        void RegenCompute()
        {
            
            if (enableCPUMarchingSquares && field_textures.IsDataValid)
            {
                GenerateCPUMarchingSquares();
            }
            
            
            // var sw = System.Diagnostics.Stopwatch.StartNew();
            computePipeline.Dispatch(field_textures, gridResolution);
            // sw.Stop();
            // print($"Dispatch took {sw.ElapsedMilliseconds}ms");
            //
            // sw.Restart();
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
            // sw.Stop();
            // print($"setting textures took {sw.ElapsedMilliseconds}ms");
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

            if (showCPUSegments && enableCPUMarchingSquares && cpuMarchingSquares != null &&
                cpuMarchingSquares.SegmentCount > 0)
            {
                DrawCPUMarchingSquares();
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

        private void DrawCPUMarchingSquares()
        {
            var segments = cpuMarchingSquares.Segments;
            int segmentsToDraw = Mathf.Min(segments.Length, maxDebugSegments);

            for (int i = 0; i < segmentsToDraw; i++)
            {
                var segment = segments[i];
                Vector3 start = new Vector3(segment.start.x, segment.start.y, 0.05f);
                Vector3 end = new Vector3(segment.end.x, segment.end.y, 0.05f);

                start = transform.TransformPoint(start);
                end = transform.TransformPoint(end);
                Debug.DrawLine(start, end, cpuDebugLineColor, debugLineDuration);
            }
        }

        #endregion
    }
}