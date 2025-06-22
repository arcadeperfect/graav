using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Object = UnityEngine.Object;

namespace PlanetGen
{
   
    // public static class Texture2DExtensions
    // {
    //     public static void ApplyBlur(this Texture2D texture, int blurRadius)
    //     {
    //         if (blurRadius <= 0) return;
    //
    //         // Using ReadPixels is often faster than GetPixels
    //         RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height);
    //         Graphics.Blit(texture, rt);
    //         RenderTexture previous = RenderTexture.active;
    //         RenderTexture.active = rt;
    //     
    //         Texture2D tempTex = new Texture2D(texture.width, texture.height, texture.format, false);
    //         tempTex.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
    //         tempTex.Apply();
    //
    //         RenderTexture.active = previous;
    //         RenderTexture.ReleaseTemporary(rt);
    //
    //         Color[] pixels = tempTex.GetPixels();
    //         Object.Destroy(tempTex); // Clean up the temporary texture
    //
    //         int width = texture.width;
    //         int height = texture.height;
    //         Color[] blurredPixels = new Color[pixels.Length];
    //
    //         for (int y = 0; y < height; y++)
    //         {
    //             for (int x = 0; x < width; x++)
    //             {
    //                 Color sum = Color.clear;
    //                 int count = 0;
    //
    //                 for (int ky = -blurRadius; ky <= blurRadius; ky++)
    //                 {
    //                     for (int kx = -blurRadius; kx <= blurRadius; kx++)
    //                     {
    //                         int sampleX = x + kx;
    //                         int sampleY = y + ky;
    //
    //                         if (sampleX >= 0 && sampleX < width && sampleY >= 0 && sampleY < height)
    //                         {
    //                             sum += pixels[(sampleY * width) + sampleX];
    //                             count++;
    //                         }
    //                     }
    //                 }
    //                 blurredPixels[(y * width) + x] = sum / count;
    //             }
    //         }
    //         texture.SetPixels(blurredPixels);
    //         texture.Apply();
    //     }
    // }


    public class FieldGen: IDisposable
    {
        public struct  TextureRegistry: IDisposable
        {
            public RenderTexture ScalarField;
            public RenderTexture Colors;

            public TextureRegistry(int texture_width)
            {
                ScalarField = new RenderTexture(texture_width, texture_width, 0, RenderTextureFormat.ARGBFloat)
                {
                    filterMode = FilterMode.Point,
                    enableRandomWrite = true
                };
                ScalarField.Create();
                Colors = new RenderTexture(texture_width, texture_width, 0, RenderTextureFormat.ARGBFloat)
                {
                    filterMode = FilterMode.Point,
                    enableRandomWrite = true
                };
                Colors.Create();
            }

            // public void Initialize(int texture_width)
            // {
            //     // Safely destroy previous textures before creating new ones
            //     if (Fields != null) Object.Destroy(Fields);
            //     if (Colors != null) Object.Destroy(Colors);
            //
            //     Fields = new RenderTexture(texture_width, texture_width, 0, RenderTextureFormat.ARGBFloat)
            //     {
            //         filterMode = FilterMode.Point,
            //         enableRandomWrite = true
            //     };
            //     Fields.Create();
            //     Colors = new RenderTexture(texture_width, texture_width, 0, RenderTextureFormat.ARGBFloat)
            //     {
            //         filterMode = FilterMode.Point,
            //         enableRandomWrite = true
            //     };
            //     Colors.Create();
            // }
            public void Dispose()
            {
                // Safely destroy textures when disposing
                if (ScalarField != null)
                {
                    ScalarField.Release();
                    Object.Destroy(ScalarField);
                    ScalarField = null;
                }
                if (Colors != null)
                {
                    Colors.Release();
                    Object.Destroy(Colors);
                    Colors = null;
                }
            }
        }

        public void GetTex(ref TextureRegistry inTEX, float seed, float radius, float amplitude, float frequency, int tex_width, int blurRadius = 0)
        {
            // Reinitialize textures only if the width has changed or they don't exist
            if (inTEX.ScalarField == null || tex_width != inTEX.ScalarField.width)
            {
                // inTEX.Initialize(tex_width);
                inTEX.Dispose(); // Dispose of existing textures if they exist
                inTEX = new TextureRegistry(tex_width); // Create new textures
            }

            // Allocate native array for texture data
            NativeArray<float4> textureData = new NativeArray<float4>(tex_width * tex_width, Allocator.TempJob);
            NativeArray<float4> colorData = new NativeArray<float4>(tex_width * tex_width, Allocator.TempJob);

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

            Texture2D tempTexture = new Texture2D(tex_width, tex_width, TextureFormat.RGBAFloat, false);
            Texture2D tempColorTexture = new Texture2D(tex_width, tex_width, TextureFormat.RGBAFloat, false);
    
            // Set pixel data on Texture2D
            tempTexture.SetPixelData(textureData, 0);
            tempTexture.Apply();
    
            tempColorTexture.SetPixelData(colorData, 0);
            tempColorTexture.Apply();
    
            // Blit to RenderTexture
            Graphics.Blit(tempTexture, inTEX.ScalarField);
            Graphics.Blit(tempColorTexture, inTEX.Colors);
    
            // Clean up
            Object.Destroy(tempTexture);
            Object.Destroy(tempColorTexture);
            textureData.Dispose();
            colorData.Dispose();
        }
        
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
            [WriteOnly] public NativeArray<float4> fielddData;
            [WriteOnly] public NativeArray<float4> colorData;

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

                fielddData[index] = new float4(val, val, val, 1.0f);

                var mult = 0.02f;
                float h = noise.snoise(pos * mult) / 2f + 0.5f;

                var color = Color.HSVToRGB(h, 1.0f, 1.0f);
                
                colorData[index] = new float4(color.r, color.g, color.b, 1.0f);
            }
        }

        public void Dispose()
        {
            // Nothing to dispose currently because the textures are owned by the caller
        }
    }
}