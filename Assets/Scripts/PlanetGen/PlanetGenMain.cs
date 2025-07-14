using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using PlanetGen.Compute;
using PlanetGen.FieldGen2.Types;
using Unity.Collections;
using Unity.Mathematics;
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

        // [Range(0, 0.5f)] [TriggerFieldRegen] public float radius = 0.5f;
        // [Range(0, 50f)] [TriggerFieldRegen] public float amplitude = 0.5f;
        // [Range(0, 50f)] [TriggerFieldRegen] public float frequency = 0.5f;
        // [Range(0, 5)] [TriggerFieldRegen] public int blur;

        [Header("Field Preview")] [Range(0, 2)] [TriggerFieldRegen]
        public int fieldDisplayMode;

        [Range(0f, 1f)] [TriggerFieldRegen] public float fieldDisplayOpacity = 1f;

        [Header("CPU Marching Squares")] [TriggerFieldRegen]
        public bool enableCPUMarchingSquares = true;

        [TriggerFieldRegen] public bool enablePolylineGeneration = false;

        [TriggerFieldRegen] public bool createColliders = false;
        [TriggerFieldRegen] public PhysicsMaterial2D colliderMaterial;
        [TriggerFieldRegen] public float colliderThickness = 0.1f;

        [TriggerFieldRegen] [Range(0.1f, 0.9f)]
        public float marchingSquaresThreshold = 0.5f;

        [TriggerFieldRegen] public bool useFastMethod = true;
        [TriggerFieldRegen] public int batchSize = 4;
        


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

        [Header("Debug")] 
        public bool showPerformanceStats = true;
        public bool showCPUSegments = true;
        public bool showGPUSegments = true;
        public bool showCPUPolylines;

        public Color debugLineColor = Color.red;
        public Color cpuDebugLineColor = Color.blue;
        public Color polylineDebugColor = Color.green;
        public float debugLineDuration = 0.1f;
        public int maxDebugSegments = 20000;
        public bool computeConstantly;

        #endregion

        #region Members

        // Core systems
        // private FieldGen.FieldGen fieldGen;
        private FieldGen2.FieldGen2 fieldGen;
        private ComputePipeline computePipeline;
        private ParameterWatcher paramWatcher;


        public Renderer fieldRenderer;
        public Renderer sdfRenderer;
        public Renderer resultRenderer;
        public GameObject collidersObject;

        // private FieldGen.FieldGen.FieldData field_textures;
        private FieldData2 _fieldData;
        
        private NativeList<float4> cpuSegments;
        private MarchingSquaresCPU.PolylineData cpuPolylines;
        private NativeList<MarchingSquaresCPU.ColliderData> colliderData;
        private List<EdgeCollider2D> cachedColliders = new List<EdgeCollider2D>();


        // Performance tracking
        private float lastCPUMarchingSquaresTime;
        private int lastCPUSegmentCount;
        private float lastPolylineGenerationTime;
        private int lastPolylineCount;

        #endregion

        
        
        #region Unity Lifecycle

        public void Start()
        {
            
            Init();
            // ProcessFieldData();
        }

        public void Update()
        {
            var changes = paramWatcher.CheckForChanges();
            
            if (changes.HasFieldRegen())
            {
                computePipeline.Init(scalarFieldWidth, textureRes, gridResolution, maxSegmentsPerCell);
                // ProcessFieldData();
                RegenField();
            }
            else if (changes.HasComputeRegen())
            {
                RegenCompute();
            }
            else if (computeConstantly)
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
            // fieldGen?.Dispose();
            // field_textures.Dispose();
        }

        #endregion

        #region Pipeline

        void Init()
        {
            paramWatcher = new ParameterWatcher(this);
            fieldGen = GetComponent<FieldGen2.FieldGen2>();
            
            fieldGen.OnDataReady += ProcessFieldData;
            computePipeline = new ComputePipeline(this);
            computePipeline.Init(scalarFieldWidth, textureRes, gridResolution, maxSegmentsPerCell);

            cpuSegments = new NativeList<float4>(Allocator.Persistent);
            cpuPolylines = new MarchingSquaresCPU.PolylineData(Allocator.Persistent);
            colliderData = new NativeList<MarchingSquaresCPU.ColliderData>(Allocator.Persistent);
        }

        void RegenField()
        {
            fieldGen.ExternalProcess(0); //TODO implement seed
        }

        void ProcessFieldData(FieldData2 fieldData)
        {
            
            _fieldData = fieldData;
            
            if(_fieldData == null)
            {
                Debug.LogError("Field data is not initialized. Please ensure FieldGen2 is set up correctly.");
                return;
            }
            
            if (!_fieldData.IsDataValid)
            {
                print("Field data is not valid, cannot regenerate field.");
                return;
            }
            
            
            computePipeline.Init(scalarFieldWidth, textureRes, gridResolution, maxSegmentsPerCell);
            fieldRenderer.material.SetTexture("_FieldTex", _fieldData.ScalarFieldTexture);
            fieldRenderer.material.SetTexture("_ColorTex", _fieldData.Colors);
            fieldRenderer.material.SetInt("_Mode", fieldDisplayMode);
            fieldRenderer.material.SetFloat("_Alpha", fieldDisplayOpacity);
            fieldRenderer.enabled = enableFieldPreview;

            RegenCompute();
        }


        void RegenCompute()
        {
            
            
            if (enableCPUMarchingSquares && _fieldData.IsDataValid)
            {
                var stopwatch = new Stopwatch();
                // --- Step 1: Generate Segments ---
                if (showPerformanceStats) stopwatch.Start();

                if (cpuSegments.IsCreated) cpuSegments.Dispose();
                cpuSegments = MarchingSquaresCPU.GenerateSegmentsBurst(_fieldData, marchingSquaresThreshold,
                    Allocator.Persistent);

                if (showPerformanceStats)
                {
                    stopwatch.Stop();
                    lastCPUMarchingSquaresTime = stopwatch.ElapsedMilliseconds;
                    lastCPUSegmentCount = cpuSegments.Length;
                    stopwatch.Reset();
                }

                // --- Step 2: Extract Polylines ---
                if (enablePolylineGeneration)
                {
                    if (showPerformanceStats) stopwatch.Start();
                
                    if (cpuPolylines.AllPoints.IsCreated) cpuPolylines.Dispose();
                    cpuPolylines = MarchingSquaresCPU.ExtractPolylinesBurst(cpuSegments, Allocator.Persistent);
                
                
                
                    if (showPerformanceStats)
                    {
                        stopwatch.Stop();
                        lastPolylineGenerationTime = stopwatch.ElapsedMilliseconds;
                        lastPolylineCount = cpuPolylines.PolylineRanges.Length;
                
                        print($"{lastPolylineCount} polylines took {lastPolylineGenerationTime} ms");
                    }
                }

                if (enablePolylineGeneration)
                {
                    if (showPerformanceStats) stopwatch.Start();
                    if (cpuPolylines.AllPoints.IsCreated) cpuPolylines.Dispose();
                    if (colliderData.IsCreated) colliderData.Dispose();

                    if (createColliders)
                    {
                        cpuPolylines = MarchingSquaresCPU.ExtractPolylinesWithColliders(cpuSegments, out colliderData,
                            3, Allocator.Persistent);
                        UpdateCollidersFromData();
                    }
                }
            }

            computePipeline.Dispatch(_fieldData, gridResolution);

            resultRenderer.material.SetTexture("_ColorTexture", _fieldData.Colors);
            resultRenderer.material.SetTexture("_SDFTexture", computePipeline.JumpFloodSdfTexture);
            resultRenderer.material.SetTexture("_WarpedSDFTexture", computePipeline.WarpedSdfTexture);
            resultRenderer.material.SetTexture("_UDFTexture", computePipeline.SurfaceUdfTexture);

            resultRenderer.material.SetFloat("_LineWidth", lineWidth * 0.01f);
            resultRenderer.material.SetFloat("_BandLineWidth", lineWidth * 0.01f);
            resultRenderer.material.SetInt("_NumberOfBands", numberOfBands);
            resultRenderer.material.SetFloat("_BandStartOffset", bandStartOffset);
            resultRenderer.material.SetFloat("_BandInterval", bandInterval);
            resultRenderer.material.SetFloat("_SurfaceBrightness", 1);
            resultRenderer.material.SetFloat("_BandBrightness", 0.5f);

            resultRenderer.enabled = renderActive;

            sdfRenderer.material.SetTexture("_SDFTex", computePipeline.SurfaceUdfTexture);
            sdfRenderer.material.SetInt("_Mode", sdfDisplayMode);
            sdfRenderer.material.SetFloat("_Alpha", sdfDisplayOpacity);
            sdfRenderer.material.SetFloat("_Mult", sdfDisplayMult);
            sdfRenderer.enabled = enableSdfPreview;
        }
        
        private void UpdateCollidersFromData()
        {
            // Disable excess colliders
            for (int i = colliderData.Length; i < cachedColliders.Count; i++)
            {
                if (cachedColliders[i] != null) cachedColliders[i].enabled = false;
            }

            // Update/create colliders from data
            for (int i = 0; i < colliderData.Length; i++)
            {
                var data = colliderData[i];
        
                EdgeCollider2D collider;
                if (i < cachedColliders.Count)
                {
                    collider = cachedColliders[i];
                    collider.enabled = true;
                }
                else
                {
                    var go = new GameObject($"Collider_{i}");
                    go.transform.SetParent(collidersObject.transform);
                    collider = go.AddComponent<EdgeCollider2D>();
                    cachedColliders.Add(collider);
                }

                // Set properties
                collider.sharedMaterial = colliderMaterial;
                collider.edgeRadius = colliderThickness * 0.01f;

                // Convert points
                var points = new Vector2[data.PointCount];
                for (int j = 0; j < data.PointCount; j++)
                {
                    var point = cpuPolylines.AllPoints[data.StartIndex + j];
                    points[j] = new Vector2(point.x, point.y);
                }
                collider.points = points;
            }
        }

        #endregion


        #region Debug

        public void DebugDraw()
        {
            if (showCPUSegments && cpuSegments.IsCreated)
            {
                DrawCPUSegments();
            }

            // --- NEW ---: Draw polylines if enabled
            if (showCPUPolylines && cpuPolylines.AllPoints.IsCreated)
            {
                DrawCPUPolylines();
            }

            if (showGPUSegments && computePipeline?.SegmentsBuffer != null &&
                computePipeline?.SegmentCountBuffer != null)
            {
                DrawGPUMarchingSquares();
            }
        }

        private void DrawCPUPolylines()
        {
            for (int i = 0; i < cpuPolylines.PolylineRanges.Length; i++)
            {
                var range = cpuPolylines.PolylineRanges[i];
                int startIdx = range.x;
                int pointCount = range.y;

                for (int j = 0; j < pointCount - 1; j++)
                {
                    float2 p1_f2 = cpuPolylines.AllPoints[startIdx + j];
                    float2 p2_f2 = cpuPolylines.AllPoints[startIdx + j + 1];

                    Vector3 p1 = transform.TransformPoint(new Vector3(p1_f2.x, p1_f2.y, 0));
                    Vector3 p2 = transform.TransformPoint(new Vector3(p2_f2.x, p2_f2.y, 0));

                    Debug.DrawLine(p1, p2, polylineDebugColor, debugLineDuration);
                }
            }

            // for (int polylineIndex = 0; polylineIndex < cpuPolylines.PolylineRanges.Length; polylineIndex++)
            // {
            //     var range = cpuPolylines.PolylineRanges[polylineIndex];
            //     int startIndex = range.x;
            //     int pointCount = range.y;
            //
            //     Debug.Log($"Polyline {polylineIndex}: {pointCount} points");
            //
            //     // // Print all points in this polyline
            //     // for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            //     // {
            //     //     int globalIndex = startIndex + pointIndex;
            //     //     var point = cpuPolylines.AllPoints[globalIndex];
            //     //     Debug.Log($"  Point {pointIndex}: ({point.x:F3}, {point.y:F3})");
            //     // }
            // }
        }

        private void DrawCPUSegments()
        {
            int actualSegmentCount = Mathf.Min(cpuSegments.Length, maxDebugSegments);
            for (int i = 0; i < actualSegmentCount; i++)
            {
                float4 segment = cpuSegments[i];
                Vector3 start = transform.TransformPoint(new Vector3(segment.x, segment.y, 0f));
                Vector3 end = transform.TransformPoint(new Vector3(segment.z, segment.w, 0f));
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
        #region Brush Support Methods

        /// <summary>
        /// Gets a reference to the current field data for brush operations.
        /// </summary>
        public FieldData2 GetFieldData()
        {
            return _fieldData;
        }

        /// <summary>
        /// Updates the GPU textures after brush modifications and triggers regeneration.
        /// Call this after modifying the scalar field data with the brush.
        /// </summary>
        public void UpdateFieldFromBrush()
        {
            if (!_fieldData.IsDataValid)
                return;
        
            // Update GPU textures from the modified native arrays
            // fieldGen.UpdateGPUTextures(ref field_textures);
        
            // Update the field display
            fieldRenderer.material.SetTexture("_FieldTex", _fieldData.ScalarFieldTexture);
            fieldRenderer.material.SetTexture("_ColorTex", _fieldData.Colors);
        
            // Regenerate compute pipeline and marching squares
            RegenCompute();
        }

        #endregion
    }
}