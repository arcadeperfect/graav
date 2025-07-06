using PlanetGen.FieldGen2.Graph;
using Sirenix.OdinInspector;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen.FieldGen2
{
    // First, add these properties to FieldGen2 class:
    // public PlanetData CurrentPlanetData => currentPlanetData;
    // public bool HasPlanetData => hasPlanetData;
    // public int CurrentRasterSize => rasterSize;

    public class PlanetDataVisualizer : MonoBehaviour
    {
        [Header("Source")]
        public FieldGen2 fieldGen;
        
        [Header("Visualization")]
        public VisualizationMode visualizationMode = VisualizationMode.Color;
        
        [Header("Display")]
        public Renderer targetRenderer;
        
        private Texture2D currentTexture;

        void Start()
        {
            // Auto-find FieldGen2 if not assigned
            if (fieldGen == null)
            {
                fieldGen = GetComponent<FieldGen2>();
                if (fieldGen == null)
                {
                    fieldGen = FindObjectOfType<FieldGen2>();
                }
            }
            
            // Subscribe to events
            FieldGen2.OnPlanetDataGenerated += OnPlanetDataGenerated;
            
            // Try to visualize existing data
            if (fieldGen != null && fieldGen.HasRasterData)
            {
                UpdateVisualization();
            }
        }
        
        void OnDestroy()
        {
            // Unsubscribe from events
            FieldGen2.OnPlanetDataGenerated -= OnPlanetDataGenerated;
            
            if (currentTexture != null)
            {
                if (Application.isPlaying)
                    Destroy(currentTexture);
                else
                    DestroyImmediate(currentTexture);
            }
        }
        
        private void OnPlanetDataGenerated(FieldGen2 source)
        {
            // Only respond to our specific FieldGen2 (in case there are multiple)
            if (source == fieldGen)
            {
                UpdateVisualization();
            }
        }

        [Button("Force Update")]
        public void ForceUpdate()
        {
            UpdateVisualization();
        }

        public void UpdateVisualization()
        {
            if (fieldGen == null || !fieldGen.HasRasterData || targetRenderer == null) return;

            // Clean up old texture
            if (currentTexture != null)
            {
                if (Application.isPlaying)
                    Destroy(currentTexture);
                else
                    DestroyImmediate(currentTexture);
            }

            // Create new visualization
            currentTexture = PlanetDataVisualizerUtils.CreateTexture(
                fieldGen.CurrentRaster, 
                fieldGen.CurrentRasterSize, 
                visualizationMode
            );

            // Apply to renderer
            if (targetRenderer.material != null)
            {
                targetRenderer.material.mainTexture = currentTexture;
            }
        }

        void OnValidate()
        {
            // Update visualization when inspector values change
            if (Application.isPlaying)
            {
                UpdateVisualization();
            }
        }
    }

    // Keep the existing utility class unchanged
    public static class PlanetDataVisualizerUtils
    {
        public static Texture2D CreateTexture(RasterData rasterData, int textureSize, 
            VisualizationMode mode = VisualizationMode.Color)
        {
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[textureSize * textureSize];

            for (int i = 0; i < textureSize * textureSize; i++)
            {
                Color32 pixelColor = GetPixelColor(rasterData, i, mode);
                pixels[i] = pixelColor;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Color32 GetPixelColor(RasterData rasterData, int index, VisualizationMode mode)
        {
            switch (mode)
            {
                case VisualizationMode.Color:
                    float4 color = rasterData.Color[index];
                    return new Color32(
                        (byte)(color.x * 255),
                        (byte)(color.y * 255), 
                        (byte)(color.z * 255),
                        (byte)(color.w * 255)
                    );

                case VisualizationMode.Scalar:
                    float scalar = rasterData.Scalar[index];
                    byte scalarByte = (byte)(scalar * 255);
                    return new Color32(scalarByte, scalarByte, scalarByte, 255);

                case VisualizationMode.Altitude:
                    float altitude = rasterData.Altitude[index];
                    byte altitudeByte = (byte)(math.saturate(altitude) * 255);
                    return new Color32(altitudeByte, altitudeByte, altitudeByte, 255);

                case VisualizationMode.Angle:
                    float angle = rasterData.Angle[index];
                    float hue = (angle + math.PI) / (2f * math.PI);
                    Color hsvColor = Color.HSVToRGB(hue, 1f, 1f);
                    return hsvColor;

                default:
                    return Color.magenta;
            }
        }
    }

    public enum VisualizationMode
    {
        Color,
        Scalar,
        Altitude,
        Angle
    }
}