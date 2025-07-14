using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using PlanetGen.Compute;
using PlanetGen.Core;
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

        [Header("Debug")] public bool showPerformanceStats = true;
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
        private FieldGen2.FieldGenMain _fieldGenMain;
        private ComputePipeline computePipeline;
        private ParameterWatcher paramWatcher;

        public Renderer fieldRenderer;
        public Renderer sdfRenderer;
        public Renderer resultRenderer;
        public GameObject collidersObject;

        // Data management - proper ownership pattern
        private FieldData _baseFieldData;
        private DeformableFieldData _workingFieldData;
        private readonly object _dataLock = new object(); // Note: was Object, should be object

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
        }

        void Update()
        {
            var changes = paramWatcher.CheckForChanges();

            if (changes.HasFieldRegen())
            {
                Debug.Log("[DEBUG] Field regen triggered");
                // Use working data size if available, otherwise use a default
                var size = _workingFieldData?.Size ?? 512;
                var initResult = computePipeline.Init(size, textureRes, gridResolution, maxSegmentsPerCell);

                initResult.OnFailure(error =>
                    ErrorHandler.LogError("PlanetGenMain.Update", $"Failed to reinitialize pipeline: {error}"));

                RegenField();
            }
            else if (changes.HasComputeRegen())
            {
                Debug.Log("[DEBUG] Compute regen triggered");
                RegenCompute();
            }
            else if (computeConstantly)
            {
                // Comment this out temporarily to reduce log spam
                // Debug.Log("[DEBUG] Compute constantly triggered");
                RegenCompute();
            }

            if (changes.HasBufferReInit())
            {
                Debug.Log("[DEBUG] Buffer reinit triggered");
                var size = _workingFieldData?.Size ?? 512;
                var initResult = computePipeline.Init(size, textureRes, gridResolution, maxSegmentsPerCell);

                initResult.OnFailure(error =>
                    ErrorHandler.LogError("PlanetGenMain.Update", $"Failed to reinitialize buffers: {error}"));

                RegenCompute();
            }

            if (enableDebugDraw)
            {
                DebugDraw();
            }
        }

        public void OnDestroy()
        {
            lock (_dataLock)
            {
                // Dispose working data (we own this)
                _workingFieldData?.Dispose();
                _workingFieldData = null;

                // Don't dispose base data (FieldGenMain owns it)
                _baseFieldData = null;
            }

            // Dispose native collections
            if (cpuSegments.IsCreated) cpuSegments.Dispose();
            if (cpuPolylines.AllPoints.IsCreated) cpuPolylines.Dispose();
            if (colliderData.IsCreated) colliderData.Dispose();

            computePipeline?.Dispose();
        }

        #endregion

        #region Pipeline

        void Init()
        {
            paramWatcher = new ParameterWatcher(this);
            _fieldGenMain = GetComponent<FieldGen2.FieldGenMain>();

            _fieldGenMain.OnDataReady += ProcessFieldData;
            computePipeline = new ComputePipeline(this);

            cpuSegments = new NativeList<float4>(Allocator.Persistent);
            cpuPolylines = new MarchingSquaresCPU.PolylineData(Allocator.Persistent);
            colliderData = new NativeList<MarchingSquaresCPU.ColliderData>(Allocator.Persistent);
        }

        void RegenField()
        {
            _fieldGenMain.ExternalProcess(0); //TODO implement seed
        }

        private void SetupRenderers()
        {
            // Use working data textures (which can be modified)
            fieldRenderer.material.SetTexture("_FieldTex", _workingFieldData.ModifiedScalarTexture);
            fieldRenderer.material.SetTexture("_ColorTex", _workingFieldData.ColorTexture);
            fieldRenderer.material.SetInt("_Mode", fieldDisplayMode);
            fieldRenderer.material.SetFloat("_Alpha", fieldDisplayOpacity);
            fieldRenderer.enabled = enableFieldPreview;
        }

        private void DisableRendering()
        {
            fieldRenderer.enabled = false;
            resultRenderer.enabled = false;
            sdfRenderer.enabled = false;
        }

        void ProcessFieldData(FieldData fieldData)
        {
            Debug.Log($"[DEBUG] ProcessFieldData called with fieldData.IsValid = {fieldData.IsValid}");

            lock (_dataLock)
            {
                // Store reference to immutable base data (we don't own this)
                _baseFieldData = fieldData;
                Debug.Log($"[DEBUG] Stored base field data, size = {fieldData.Size}");

                // Dispose old working copy and create new one from base data
                _workingFieldData?.Dispose();
                _workingFieldData = fieldData.IsValid ? fieldData.CreateDeformableVersion() : null;
                Debug.Log($"[DEBUG] Created working field data, valid = {_workingFieldData?.IsValid}");
            }

            if (_workingFieldData?.IsValid != true)
            {
                ErrorHandler.LogError("PlanetGenMain.ProcessFieldData",
                    "Failed to create working field data from base field data");
                DisableRendering();
                return;
            }

            Debug.Log($"[DEBUG] About to initialize compute pipeline with size {_workingFieldData.Size}");

            // Initialize compute pipeline with the working data
            var initResult =
                computePipeline.Init(_workingFieldData.Size, textureRes, gridResolution, maxSegmentsPerCell);

            Debug.Log($"[DEBUG] Compute pipeline init result: {initResult.IsSuccess}");
            if (!initResult.IsSuccess)
            {
                Debug.LogError($"[DEBUG] Init failed: {initResult.ErrorMessage}");
            }
            
            Debug.Log("[DEBUG] About to call OnSuccess chain");


            initResult
                .OnSuccess(() =>
                {
                    Debug.Log("[DEBUG] Pipeline init succeeded, setting up renderers");
                    // Set up renderers only if initialization succeeded
                    SetupRenderers();
                    Debug.Log("[DEBUG] About to call RegenCompute");
                    RegenCompute();
                })
                .OnFailure(error =>
                {
                    Debug.LogError($"[DEBUG] Pipeline init failed: {error}");
                    ErrorHandler.LogError("PlanetGenMain.ProcessFieldData",
                        $"Failed to initialize compute pipeline: {error}");
                    DisableRendering();
                });
        }

        void RegenCompute()
        {
            Debug.Log("[DEBUG] RegenCompute called");

            if (_workingFieldData?.IsValid != true)
            {
                Debug.LogWarning("[DEBUG] RegenCompute: No valid working field data");
                ErrorHandler.LogWarning("PlanetGenMain.RegenCompute",
                    "Cannot regenerate compute - no valid working field data");
                return;
            }

            Debug.Log("[DEBUG] Working field data is valid, continuing...");

            // Sync any pending terrain modifications to GPU texture
            bool wasModified = _workingFieldData.SyncTextureIfDirty();
            if (wasModified)
            {
                Debug.Log("[DEBUG] Terrain modifications synced to GPU");
            }

            // Skip CPU marching squares for now to isolate the issue
            Debug.Log("[DEBUG] About to dispatch compute pipeline");

            // GPU Compute Pipeline - dispatch with working data (which includes any modifications)
            var dispatchResult = computePipeline.Dispatch(_workingFieldData, gridResolution);

            Debug.Log($"[DEBUG] Dispatch result: {dispatchResult.IsSuccess}");
            if (!dispatchResult.IsSuccess)
            {
                Debug.LogError($"[DEBUG] Dispatch failed: {dispatchResult.ErrorMessage}");
            }

            dispatchResult
                .OnSuccess(() =>
                {
                    Debug.Log("[DEBUG] Dispatch succeeded, updating material properties");
                    // Update material properties only if dispatch succeeded
                    UpdateMaterialProperties();
                    Debug.Log("[DEBUG] Material properties updated");
                })
                .OnFailure(error =>
                {
                    Debug.LogError($"[DEBUG] Dispatch failed in OnFailure: {error}");
                    ErrorHandler.LogError("PlanetGenMain.RegenCompute", $"Compute pipeline dispatch failed: {error}");
                });
        }

        private void UpdateMaterialProperties()
        {
            Debug.Log("[DEBUG] UpdateMaterialProperties called");

            // Check if textures are valid before setting them
            if (computePipeline.JumpFloodSdfTexture == null)
            {
                Debug.LogError("[DEBUG] JumpFloodSdfTexture is null!");
            }

            if (computePipeline.WarpedSdfTexture == null)
            {
                Debug.LogError("[DEBUG] WarpedSdfTexture is null!");
            }

            if (computePipeline.SurfaceUdfTexture == null)
            {
                Debug.LogError("[DEBUG] SurfaceUdfTexture is null!");
            }

            if (_workingFieldData.ColorTexture == null)
            {
                Debug.LogError("[DEBUG] WorkingFieldData.ColorTexture is null!");
            }

            // Use working data color texture (shared immutable)
            resultRenderer.material.SetTexture("_ColorTexture", _workingFieldData.ColorTexture);
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

            Debug.Log($"[DEBUG] About to enable resultRenderer, renderActive = {renderActive}");
            resultRenderer.enabled = renderActive;

            sdfRenderer.material.SetTexture("_SDFTex", computePipeline.SurfaceUdfTexture);
            sdfRenderer.material.SetInt("_Mode", sdfDisplayMode);
            sdfRenderer.material.SetFloat("_Alpha", sdfDisplayOpacity);
            sdfRenderer.material.SetFloat("_Mult", sdfDisplayMult);
            Debug.Log($"[DEBUG] About to enable sdfRenderer, enableSdfPreview = {enableSdfPreview}");
            sdfRenderer.enabled = enableSdfPreview;

            Debug.Log("[DEBUG] UpdateMaterialProperties completed");
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

        #region Terrain Deformation API

        /// <summary>
        /// Public API for brush operations - returns the mutable working copy
        /// </summary>
        public DeformableFieldData GetWorkingFieldData()
        {
            lock (_dataLock)
            {
                if (_workingFieldData?.IsValid != true)
                {
                    ErrorHandler.LogWarning("PlanetGenMain.GetWorkingFieldData",
                        "No valid working field data available for terrain deformation");
                    return null;
                }

                return _workingFieldData;
            }
        }

        /// <summary>
        /// Reset terrain modifications back to original base state
        /// </summary>
        public void ResetTerrain()
        {
            lock (_dataLock)
            {
                _workingFieldData?.ResetToBase();
            }

            // Trigger compute pipeline update to reflect the reset
            RegenCompute();
        }

        /// <summary>
        /// Legacy API for compatibility - use GetWorkingFieldData() instead
        /// </summary>
        public FieldData GetFieldData()
        {
            return _baseFieldData;
        }

        /// <summary>
        /// Legacy method - should use terrain deformation API instead
        /// </summary>
        public void UpdateFieldFromBrush()
        {
            if (_workingFieldData?.IsValid != true)
                return;

            // Update the field display
            fieldRenderer.material.SetTexture("_FieldTex", _workingFieldData.ModifiedScalarTexture);
            fieldRenderer.material.SetTexture("_ColorTex", _workingFieldData.ColorTexture);

            // Regenerate compute pipeline and marching squares
            RegenCompute();
        }

        #endregion

        
    }
}