using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlanetGen
{
    /// <summary>
    /// Simple, performant terrain brush for continuous digging and building.
    /// Astroneer-style terrain deformation with just radius and strength controls.
    /// Updated to work with the new DeformableFieldData architecture.
    /// </summary>
    public class TerrainBrush : MonoBehaviour
    {
        [Header("Brush Settings")]
        [Range(0.01f, 0.3f)]
        public float brushRadius = 0.05f;
        
        [Range(0.1f, 10f)]
        public float brushStrength = 1f;
        
        [Header("Controls")]
        public InputActionReference digAction;
        public InputActionReference buildAction;
        public InputActionReference mousePositionAction;
        
        [Header("Visual")]
        public bool showBrushCursor = true;
        public Material brushCursorMaterial;
        
        [Header("Performance")]
        [Range(10, 120)]
        public int updatesPerSecond = 60;
        
        [Header("Debug")]
        public bool enableDebugLogging = true;
        
        // Private members
        private PlanetGenMain planetGenMain;
        private Camera mainCamera;
        private LineRenderer brushCursor;
        private Vector2 lastBrushPosition;
        private float lastUpdateTime;
        
        // Cursor circle
        private const int CIRCLE_SEGMENTS = 24;
        private Vector3[] circlePoints = new Vector3[CIRCLE_SEGMENTS + 1];
        
        void Start()
        {
            planetGenMain = GetComponent<PlanetGenMain>();
            mainCamera = Camera.main ?? FindObjectOfType<Camera>();
            
            if (planetGenMain == null)
            {
                Debug.LogError("TerrainBrush needs PlanetGenMain component!");
                enabled = false;
                return;
            }
            
            Debug.Log("[TerrainBrush] Initialized successfully");
            
            // Enable input actions
            if (digAction != null) digAction.action.Enable();
            if (buildAction != null) buildAction.action.Enable();
            if (mousePositionAction != null) mousePositionAction.action.Enable();
            
            SetupBrushCursor();
        }
        
        void OnDestroy()
        {
            // Disable input actions
            if (digAction != null) digAction.action.Disable();
            if (buildAction != null) buildAction.action.Disable();
            if (mousePositionAction != null) mousePositionAction.action.Disable();
        }
        
        void Update()
        {
            HandleInput();
            UpdateBrushCursor();
        }
        
        void HandleInput()
        {
            bool isDigging = digAction != null && digAction.action.IsPressed();
            bool isBuilding = buildAction != null && buildAction.action.IsPressed();
            
            if (!isDigging && !isBuilding)
                return;
            
            if (enableDebugLogging)
            {
                Debug.Log($"[TerrainBrush] Input detected - Digging: {isDigging}, Building: {isBuilding}");
            }
            
            // Throttle updates for performance
            float timeSinceUpdate = Time.time - lastUpdateTime;
            if (timeSinceUpdate < 1f / updatesPerSecond)
                return;
            
            Vector2 mouseWorldPos = GetMouseWorldPosition();
            
            if (enableDebugLogging)
            {
                Debug.Log($"[TerrainBrush] Mouse world position: {mouseWorldPos}");
            }
            
            // Apply continuous brushing
            if (isDigging)
            {
                ApplyBrush(mouseWorldPos, false); // false = dig (subtract)
            }
            else if (isBuilding)
            {
                ApplyBrush(mouseWorldPos, true);  // true = build (add)
            }
            
            lastBrushPosition = mouseWorldPos;
            lastUpdateTime = Time.time;
        }
        
        Vector2 GetMouseWorldPosition()
        {
            Vector2 mouseScreenPos = mousePositionAction != null ? 
                mousePositionAction.action.ReadValue<Vector2>() : 
                Mouse.current.position.ReadValue();
            
            // Use camera's transform.position.z as the distance for proper screen-to-world conversion
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, -mainCamera.transform.position.z));
            return transform.InverseTransformPoint(mouseWorldPos);
        }
        
        void ApplyBrush(Vector2 worldPosition, bool isBuilding)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[TerrainBrush] ApplyBrush called - Position: {worldPosition}, Building: {isBuilding}");
            }
            
            // Get the working field data (mutable version)
            var workingFieldData = planetGenMain.GetWorkingFieldData();
            if (workingFieldData?.IsValid != true)
            {
                if (enableDebugLogging)
                {
                    Debug.LogWarning($"[TerrainBrush] No valid working field data available. WorkingFieldData is null: {workingFieldData == null}");
                    if (workingFieldData != null)
                    {
                        Debug.LogWarning($"[TerrainBrush] Working field data IsValid: {workingFieldData.IsValid}");
                    }
                }
                return;
            }
            
            if (enableDebugLogging)
            {
                Debug.Log($"[TerrainBrush] Working field data obtained - Size: {workingFieldData.Size}");
            }
            
            // Convert world position (-1 to 1) to texture coordinates (0 to width)
            Vector2 normalizedPos = (worldPosition + Vector2.one) * 0.5f;
            Vector2 texturePos = normalizedPos * workingFieldData.Size;
            int2 center = new int2((int)texturePos.x, (int)texturePos.y);
            
            // Convert brush radius to texture space
            float radiusInTexels = brushRadius * workingFieldData.Size * 0.5f;
            
            // Calculate strength based on delta time for frame-rate independent brushing
            float frameStrength = brushStrength * Time.deltaTime;
            
            if (enableDebugLogging)
            {
                Debug.Log($"[TerrainBrush] Deformation params - Center: {center}, Radius: {radiusInTexels}, Strength: {frameStrength}");
                
                // Check if center is within bounds
                bool inBounds = center.x >= 0 && center.x < workingFieldData.Size && 
                               center.y >= 0 && center.y < workingFieldData.Size;
                Debug.Log($"[TerrainBrush] Center in bounds: {inBounds}");
            }
            
            // Sample multiple values before deformation to see the area
            float valueBefore = 0f;
            float[] sampleValuesBefore = new float[9];
            int sampleIndex = 0;
            
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int sampleX = center.x + dx;
                    int sampleY = center.y + dy;
                    
                    if (sampleX >= 0 && sampleX < workingFieldData.Size && 
                        sampleY >= 0 && sampleY < workingFieldData.Size)
                    {
                        int index = sampleY * workingFieldData.Size + sampleX;
                        if (index >= 0 && index < workingFieldData.ModifiedScalarField.Length)
                        {
                            sampleValuesBefore[sampleIndex] = workingFieldData.ModifiedScalarField[index];
                            if (dx == 0 && dy == 0) valueBefore = sampleValuesBefore[sampleIndex];
                        }
                    }
                    sampleIndex++;
                }
            }
            
            // Use the built-in deformation method
            workingFieldData.DeformTerrain(center, radiusInTexels, frameStrength, isBuilding);
            
            // Sample values after deformation
            float valueAfter = 0f;
            float[] sampleValuesAfter = new float[9];
            sampleIndex = 0;
            bool anyChange = false;
            
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int sampleX = center.x + dx;
                    int sampleY = center.y + dy;
                    
                    if (sampleX >= 0 && sampleX < workingFieldData.Size && 
                        sampleY >= 0 && sampleY < workingFieldData.Size)
                    {
                        int index = sampleY * workingFieldData.Size + sampleX;
                        if (index >= 0 && index < workingFieldData.ModifiedScalarField.Length)
                        {
                            sampleValuesAfter[sampleIndex] = workingFieldData.ModifiedScalarField[index];
                            if (dx == 0 && dy == 0) valueAfter = sampleValuesAfter[sampleIndex];
                            
                            if (Mathf.Abs(sampleValuesAfter[sampleIndex] - sampleValuesBefore[sampleIndex]) > 0.001f)
                            {
                                anyChange = true;
                            }
                        }
                    }
                    sampleIndex++;
                }
            }
            
            if (enableDebugLogging)
            {
                Debug.Log($"[TerrainBrush] Center value change - Before: {valueBefore:F3}, After: {valueAfter:F3}, Delta: {valueAfter - valueBefore:F3}");
                Debug.Log($"[TerrainBrush] Any change detected in 3x3 area: {anyChange}");
                
                if (!anyChange)
                {
                    Debug.LogWarning($"[TerrainBrush] NO CHANGES DETECTED! This might indicate an issue with the deformation logic.");
                    Debug.LogWarning($"[TerrainBrush] Brush params were - Radius: {radiusInTexels:F1}, Strength: {frameStrength:F3}, Building: {isBuilding}");
                }
            }
            
            // Force an immediate update to see if the system responds
            planetGenMain.UpdateFieldFromBrush();
            
            if (enableDebugLogging)
            {
                Debug.Log("[TerrainBrush] Called UpdateFieldFromBrush()");
            }
        }
        
        void SetupBrushCursor()
        {
            if (!showBrushCursor)
                return;
            
            GameObject cursorObj = new GameObject("BrushCursor");
            cursorObj.transform.SetParent(transform);
            brushCursor = cursorObj.AddComponent<LineRenderer>();
            
            // Setup line renderer
            if (brushCursorMaterial != null)
                brushCursor.material = brushCursorMaterial;
            else
                brushCursor.material = new Material(Shader.Find("Sprites/Default"));
            
            brushCursor.startWidth = 0.01f;
            brushCursor.endWidth = 0.01f;
            brushCursor.positionCount = CIRCLE_SEGMENTS + 1;
            brushCursor.useWorldSpace = false;
            brushCursor.loop = true;
        }
        
        void UpdateBrushCursor()
        {
            if (!showBrushCursor || brushCursor == null)
                return;
            
            Vector2 mouseWorldPos = GetMouseWorldPosition();
            
            // Generate circle points
            for (int i = 0; i <= CIRCLE_SEGMENTS; i++)
            {
                float angle = (float)i / CIRCLE_SEGMENTS * Mathf.PI * 2f;
                circlePoints[i] = new Vector3(
                    mouseWorldPos.x + Mathf.Cos(angle) * brushRadius,
                    mouseWorldPos.y + Mathf.Sin(angle) * brushRadius,
                    0f
                );
            }
            
            brushCursor.SetPositions(circlePoints);
            
            // Change color based on what we're doing
            bool isDigging = digAction != null && digAction.action.IsPressed();
            bool isBuilding = buildAction != null && buildAction.action.IsPressed();
            
            if (isDigging)
                brushCursor.material.color = Color.red;
            else if (isBuilding)
                brushCursor.material.color = Color.green;
            else
                brushCursor.material.color = Color.white;
        }
    }
}