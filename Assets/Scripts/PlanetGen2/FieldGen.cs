using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen2
{
   
    public static class Texture2DExtensions
    {
        public static void ApplyBlur(this Texture2D texture, int blurRadius)
        {
            if (blurRadius <= 0) return;

            // Using ReadPixels is often faster than GetPixels
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height);
            Graphics.Blit(texture, rt);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
        
            Texture2D tempTex = new Texture2D(texture.width, texture.height, texture.format, false);
            tempTex.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            tempTex.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color[] pixels = tempTex.GetPixels();
            Object.Destroy(tempTex); // Clean up the temporary texture

            int width = texture.width;
            int height = texture.height;
            Color[] blurredPixels = new Color[pixels.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color sum = Color.clear;
                    int count = 0;

                    for (int ky = -blurRadius; ky <= blurRadius; ky++)
                    {
                        for (int kx = -blurRadius; kx <= blurRadius; kx++)
                        {
                            int sampleX = x + kx;
                            int sampleY = y + ky;

                            if (sampleX >= 0 && sampleX < width && sampleY >= 0 && sampleY < height)
                            {
                                sum += pixels[(sampleY * width) + sampleX];
                                count++;
                            }
                        }
                    }
                    blurredPixels[(y * width) + x] = sum / count;
                }
            }
            texture.SetPixels(blurredPixels);
            texture.Apply();
        }
    }


    public class FieldGen
    {
        public struct  TextureRegistry
        {
            public Texture2D fields;
            public Texture2D colors;

            public TextureRegistry(int texture_width)
            {
                fields = new Texture2D(texture_width, texture_width, TextureFormat.RGBAFloat, false)
                {
                    filterMode = FilterMode.Point
                };
                colors = new Texture2D(texture_width, texture_width, TextureFormat.RGBAFloat, false)
                {
                    filterMode = FilterMode.Point
                };
            }

            public void Reinitialize(int new_texture_width)
            {
                // Safely destroy previous textures before creating new ones
                if (fields != null) Object.Destroy(fields);
                if (colors != null) Object.Destroy(colors);

                fields = new Texture2D(new_texture_width, new_texture_width, TextureFormat.RGBAFloat, false)
                {
                    filterMode = FilterMode.Point
                };
                colors = new Texture2D(new_texture_width, new_texture_width, TextureFormat.RGBAFloat, false)
                {
                    filterMode = FilterMode.Point
                };
            }
        }

        public void GetTex(ref TextureRegistry in_tex, float seed, float radius, float amplitude, float frequency, int tex_width, int blurRadius = 0)
        {
            // Reinitialize textures only if the width has changed or they don't exist
            if (in_tex.fields == null || tex_width != in_tex.fields.width)
            {
                in_tex.Reinitialize(tex_width);
            }

            // Allocate native array for texture data
            NativeArray<Color> textureData = new NativeArray<Color>(tex_width * tex_width, Allocator.TempJob);
            NativeArray<Color> colorData = new NativeArray<Color>(tex_width * tex_width, Allocator.TempJob);

            // Create and schedule the job
            var job = new TextureGenerationJob
            {
                fielddData = textureData,
                colorData = colorData,
                texWidth = tex_width,
                radius = radius,
                centerX = tex_width / 2f,
                centerY = tex_width / 2f,
                normalizer = 1f / (tex_width * tex_width),
                frequency = frequency * 0.01f,
                amplitude = amplitude * 0.01f,
                seed = seed
            };

            JobHandle jobHandle = job.Schedule(tex_width * tex_width, 64);
            jobHandle.Complete();

            // Apply the data to the texture
            in_tex.fields.SetPixelData(textureData, 0);
            in_tex.fields.Apply();

            in_tex.colors.SetPixelData(colorData, 0);
            in_tex.colors.Apply();

            // Apply blur if blurRadius is greater than 0
            if (blurRadius > 0)
            {
                // Now correctly calls the top-level extension method
                in_tex.fields.ApplyBlur(blurRadius);
                in_tex.colors.ApplyBlur(blurRadius);
            }

            // Clean up
            textureData.Dispose();
            colorData.Dispose();
        }

        // ... (PrintNativeArray and TextureGenerationJob structs remain the same) ...

        void PrintNativeArray<T>(NativeArray<T> textureData, int tex_width) where T : struct
        {
            string toPrint = "";
            for (int i = 0; i < tex_width; i++)
            {
                string thisLine = "";
                for (int j = 0; j < tex_width; j++)
                {
                    int this_index = i * tex_width + j;
                    thisLine += (textureData[this_index].ToString());
                    thisLine += ("   ");
                }
                toPrint += thisLine + "\n";
            }
            Debug.Log(toPrint);
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct TextureGenerationJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<Color> fielddData;
            [WriteOnly] public NativeArray<Color> colorData;

            [ReadOnly] public int texWidth;
            [ReadOnly] public float radius;
            [ReadOnly] public float centerX;
            [ReadOnly] public float centerY;
            [ReadOnly] public float normalizer;
            [ReadOnly] public float frequency;
            [ReadOnly] public float amplitude;
            [ReadOnly] public float seed;


            public void Execute(int index)
            {
                // Convert 1D index to 2D coordinates
                int x = index % texWidth;
                int y = index / texWidth;

                // Calculate squared distance from center
                float dx = x - centerX;
                float dy = y - centerY;
                float distanceSquared = dx * dx + dy * dy;

                // Normalize the distance
                float normalizedDistanceSquared = distanceSquared * normalizer;

                float2 pos = new float2(x * frequency + seed, y * frequency + seed);
                float nze = noise.snoise(pos);

                // Determine pixel value based on radius
                float val = (normalizedDistanceSquared < (radius * radius) + (nze * amplitude) ? 1.0f : 0);

                fielddData[index] = new Color(val, val, val, 1.0f);

                var mult = 0.02f;
                float h = noise.snoise(pos * mult) / 2f + 0.5f;

                colorData[index] = Color.HSVToRGB(h, 1.0f, 1.0f);
            }
        }
    }
}