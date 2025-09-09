using System.Collections.Generic;
using PlanetGen.Compute;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Shapes;

namespace PlanetGen
{
    /// <summary>
    /// Renders marching squares polylines using the Shapes plugin
    /// Uses the same data that would be used for colliders
    /// </summary>
    public class ShapesPolylineRenderer : MonoBehaviour
    {
        private PlanetGenMain _planetGenMain;


        [Header("Rendering Settings")] [Range(0.001f, 0.1f)]
        public float lineThickness = 0.005f;

        public Color lineColor = Color.white;
        public Color fillColor = new Color(1f, 1f, 1f, 0.1f);

        [Header("Fill Settings")] public bool enableFill = false;
        public bool closedLoops = true;

        // [Header("Performance")]
        // public bool enableCulling = true;
        [Range(1, 100)] public int maxPolylinesRendered = 50;

        [Header("Debug")] public bool showDebugInfo = false;
        public bool logPerformanceStats = false;

        // Private members
        private List<Polyline> polylineComponents = new List<Polyline>();
        private List<Polygon> polygonComponents = new List<Polygon>();
        private Camera mainCamera;
        private Bounds renderBounds;
        private int lastPolylineCount = 0;

        void Start()
        {
            // mainCamera = Camera.main ?? FindObjectOfType<Camera>();
            _planetGenMain = GetComponentInParent<PlanetGenMain>();
            _planetGenMain.OnCPUPolylinesGenerated += HandleRegen;
        }

        void Update()
        {
            // if (enableCulling && mainCamera != null)
            // {
            //     UpdateRenderBounds();
            // }
            // else
            // {
            //     Debug.Log("didn't update bounds");
            // }
        }

        private void HandleRegen(
            NativeList<float4> segments,
            MarchingSquaresCPU.PolylineData polylines)
        {
            print("event received");git 
            
            UpdatePolylines(polylines);
        }

        

        /// <summary>
        /// Update polylines using the raw polyline data (alternative method)
        /// </summary>
        public void UpdatePolylines(MarchingSquaresCPU.PolylineData polylineData)
        {
            if (!polylineData.AllPoints.IsCreated || !polylineData.PolylineRanges.IsCreated)
            {
                ClearPolylines();
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Clear existing data
            ClearPolylines();

            // Process each polyline using ranges
            int processedCount = 0;
            for (int i = 0; i < polylineData.PolylineRanges.Length && processedCount < maxPolylinesRendered; i++)
            {
                var range = polylineData.PolylineRanges[i];
                int startIdx = range.x;
                int pointCount = range.y;

                // Skip if not enough points
                if (pointCount < 2)
                    continue;

                // Extract points for this polyline
                var points = new List<Vector3>();
                for (int j = 0; j < pointCount; j++)
                {
                    var point = polylineData.AllPoints[startIdx + j];
                    points.Add(transform.TransformPoint(new Vector3(point.x, point.y, 0f)));
                }

                // Skip if culling is enabled and polyline is outside view
                // if (enableCulling && !IsPolylineVisible(points))
                //     continue;

                // Create polyline component
                CreatePolylineComponent(points, i);

                // Create polygon fill if enabled
                if (enableFill && closedLoops && pointCount >= 3)
                {
                    CreatePolygonComponent(points, i);
                }

                processedCount++;
            }

            stopwatch.Stop();
            lastPolylineCount = processedCount;

            if (logPerformanceStats)
            {
                Debug.Log(
                    $"[ShapesPolylineRenderer] Updated {processedCount} polylines in {stopwatch.ElapsedMilliseconds}ms");
            }
        }

        private void CreatePolylineComponent(List<Vector3> points, int index)
        {
            // Create a new GameObject for this polyline
            GameObject polylineObj = new GameObject($"Polyline_{index}");
            polylineObj.transform.SetParent(transform);

            // Add Polyline component
            Polyline polyline = polylineObj.AddComponent<Polyline>();

            // Set polyline properties
            polyline.Color = lineColor;
            polyline.Thickness = lineThickness * 0.2f;
            polyline.Closed = closedLoops;

            // Convert points to local space and set them directly
            var localPoints = new List<Vector3>();
            foreach (var point in points)
            {
                // Convert to local space relative to the polyline object
                Vector3 localPoint = polylineObj.transform.InverseTransformPoint(point);
                localPoints.Add(localPoint);
            }

            // Set points directly on the polyline
            polyline.SetPoints(localPoints);
            polylineComponents.Add(polyline);
        }

        private void CreatePolygonComponent(List<Vector3> points, int index)
        {
            // Create a new GameObject for this polygon
            GameObject polygonObj = new GameObject($"Polygon_{index}");
            polygonObj.transform.SetParent(transform);

            // Add Polygon component
            Polygon polygon = polygonObj.AddComponent<Polygon>();

            // Set polygon properties
            polygon.Color = fillColor;

            // Convert points to local space Vector2
            var localPoints = new List<Vector2>();
            foreach (var point in points)
            {
                // Convert to local space relative to the polygon object
                Vector3 localPoint = polygonObj.transform.InverseTransformPoint(point);
                localPoints.Add(new Vector2(localPoint.x, localPoint.y));
            }

            // Set points directly on the polygon
            polygon.SetPoints(localPoints);
            polygonComponents.Add(polygon);
        }

        private bool IsPolylineVisible(List<Vector3> points)
        {
            if (mainCamera == null)
                return true;

            // Simple bounds check - could be optimized further
            foreach (var point in points)
            {
                Vector3 screenPoint = mainCamera.WorldToViewportPoint(point);
                if (screenPoint.x >= -0.1f && screenPoint.x <= 1.1f &&
                    screenPoint.y >= -0.1f && screenPoint.y <= 1.1f &&
                    screenPoint.z > 0)
                {
                    return true; // At least one point is visible
                }
            }

            return false;
        }

        private void UpdateRenderBounds()
        {
            if (mainCamera == null)
                return;

            // Update bounds based on camera view
            var frustumHeight = 2.0f * Mathf.Abs(mainCamera.transform.position.z) *
                                Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var frustumWidth = frustumHeight * mainCamera.aspect;

            Vector3 center = new Vector3(mainCamera.transform.position.x, mainCamera.transform.position.y, 0);
            Vector3 size = new Vector3(frustumWidth * 1.2f, frustumHeight * 1.2f, 1f); // 20% margin

            renderBounds = new Bounds(center, size);
        }

        void OnDrawGizmosSelected()
        {
            if (showDebugInfo)
            {
                // Draw render bounds
                // if (enableCulling)
                // {
                //     Gizmos.color = Color.yellow;
                //     Gizmos.DrawWireCube(renderBounds.center, renderBounds.size);
                // }

                // Draw polyline count info
#if UNITY_EDITOR
                UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
                    $"Polylines: {lastPolylineCount}");
#endif
            }
        }

        public void ClearPolylines()
        {
            // Destroy existing polyline GameObjects
            foreach (var polyline in polylineComponents)
            {
                if (polyline != null && polyline.gameObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(polyline.gameObject);
                    else
                        DestroyImmediate(polyline.gameObject);
                }
            }

            // Destroy existing polygon GameObjects
            foreach (var polygon in polygonComponents)
            {
                if (polygon != null && polygon.gameObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(polygon.gameObject);
                    else
                        DestroyImmediate(polygon.gameObject);
                }
            }

            polylineComponents.Clear();
            polygonComponents.Clear();
            lastPolylineCount = 0;
        }

        void OnDestroy()
        {
            ClearPolylines();
        }
    }
}