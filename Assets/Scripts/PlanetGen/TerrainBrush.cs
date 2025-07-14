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
        
        // Private members
        private PlanetGenMain planetGenMain;
        private Camera mainCamera;
        private LineRenderer brushCursor;
        private Vector2 lastBrushPosition;
        private float lastUpdateTime;
        private bool isDirty;
        
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
            
            // Update terrain if we made changes
            if (isDirty)
            {
                planetGenMain.UpdateFieldFromBrush();
                isDirty = false;
            }
        }
        
        void HandleInput()
        {
            bool isDigging = digAction != null && digAction.action.IsPressed();
            bool isBuilding = buildAction != null && buildAction.action.IsPressed();
            
            if (!isDigging && !isBuilding)
                return;
            
            // Throttle updates for performance
            float timeSinceUpdate = Time.time - lastUpdateTime;
            if (timeSinceUpdate < 1f / updatesPerSecond)
                return;
            
            Vector2 mouseWorldPos = GetMouseWorldPosition();
            
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
            var fieldData = planetGenMain.GetFieldData();
            if (!fieldData.IsValid)
                return;
            
            // Convert world position (-1 to 1) to texture coordinates (0 to width)
            Vector2 normalizedPos = (worldPosition + Vector2.one) * 0.5f;
            Vector2 texturePos = normalizedPos * fieldData.Size;
            
            // Create brush job
            var brushJob = new SimpleBrushJob
            {
                // FieldData = fieldData.ScalarFieldArray,
                FieldData = fieldData.BaseRasterData.Scalar,
                Width = fieldData.Size,
                BrushCenter = texturePos,
                BrushRadius = brushRadius * fieldData.Size * 0.5f, // Convert to texture space
                BrushStrength = brushStrength,
                IsBuilding = isBuilding,
                DeltaTime = Time.deltaTime
            };
            
            // Execute the job
            JobHandle handle = brushJob.Schedule(fieldData.Size * fieldData.Size, 128);
            handle.Complete();
            
            isDirty = true;
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
        
        [BurstCompile(CompileSynchronously = true)]
        struct SimpleBrushJob : IJobParallelFor
        {
            public NativeArray<float> FieldData;
            [ReadOnly] public int Width;
            [ReadOnly] public float2 BrushCenter;
            [ReadOnly] public float BrushRadius;
            [ReadOnly] public float BrushStrength;
            [ReadOnly] public bool IsBuilding;
            [ReadOnly] public float DeltaTime;
            
            public void Execute(int index)
            {
                int x = index % Width;
                int y = index / Width;
                
                float2 pixelPos = new float2(x, y);
                float distance = math.distance(pixelPos, BrushCenter);
                
                // Skip if outside brush radius
                if (distance > BrushRadius)
                    return;
                
                // Calculate smooth falloff
                float normalizedDistance = distance / BrushRadius;
                float falloff = 1f - normalizedDistance;
                falloff = falloff * falloff; // Smooth curve
                
                // Calculate change amount
                float changeAmount = BrushStrength * falloff * DeltaTime;
                
                float currentValue = FieldData[index];
                float newValue;
                
                if (IsBuilding)
                {
                    // Add material (build up)
                    newValue = math.min(currentValue + changeAmount, 1f);
                }
                else
                {
                    // Remove material (dig)
                    newValue = math.max(currentValue - changeAmount, 0f);
                }
                
                FieldData[index] = newValue;
            }
        }
    }
}