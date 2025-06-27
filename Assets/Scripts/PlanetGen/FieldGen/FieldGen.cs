using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanetGen.FieldGen
{
    public class FieldGen: IDisposable
    {
        public struct  FieldData: IDisposable
        {
            public RenderTexture ScalarField;
            public RenderTexture Colors;

            public FieldData(int texture_width)
            {
                ScalarField = new RenderTexture(texture_width, texture_width, 0, RenderTextureFormat.RFloat) // Use RFloat for single-channel data
                {
                    filterMode = FilterMode.Bilinear, // Use Bilinear for smoother results
                    enableRandomWrite = true
                };
                ScalarField.Create();
                Colors = new RenderTexture(texture_width, texture_width, 0, RenderTextureFormat.ARGBFloat)
                {
                    filterMode = FilterMode.Bilinear,
                    enableRandomWrite = true
                };
                Colors.Create();
            }
            
            public void Dispose()
            {
                // Safely destroy textures when disposing
                if (ScalarField != null)
                {
                    if(ScalarField.IsCreated())
                    {
                        ScalarField.Release();
                    }
                    Object.Destroy(ScalarField);
                    ScalarField = null;
                }
                if (Colors != null)
                {
                    if(Colors.IsCreated())
                    {
                        Colors.Release();
                    }
                    Object.Destroy(Colors);
                    Colors = null;
                }
            }
        }

        /// <summary>
        /// Generates noise and color textures, with an optional multi-pass blur for the noise data.
        /// </summary>
        /// <param name="inTEX">The texture registry to populate.</param>
        /// <param name="seed">Seed for the noise generation.</param>
        /// <param name="radius">Base radius of the circular shape.</param>
        /// <param name="amplitude">Amplitude of the noise displacement.</param>
        /// <param name="frequency">Frequency of the noise.</param>
        /// <param name="tex_width">The width and height of the textures to generate.</param>
        /// <param name="blurIterations">The number of times to apply the blur filter. 0 means no blur.</param>
        /// <param name="cullIslands">Whether to remove floating islands after generation.</param>
        public void GetTex(ref FieldData inTEX, float seed, float radius, float amplitude, float frequency, int tex_width, int blurIterations = 0, bool cullIslands = true)
        {
            // Reinitialize textures only if the width has changed or they don't exist
            if (inTEX.ScalarField == null || tex_width != inTEX.ScalarField.width)
            {
                inTEX.Dispose(); // Dispose of existing textures if they exist
                inTEX = new FieldData(tex_width); // Create new textures
            }

            // Allocate native arrays for texture data
            // Note: We only need a single float (R channel) for the scalar field.
            NativeArray<float> textureData = new NativeArray<float>(tex_width * tex_width, Allocator.TempJob);
            NativeArray<float4> colorData = new NativeArray<float4>(tex_width * tex_width, Allocator.TempJob);

            // --- STAGE 1: GENERATE BASE TEXTURES ---
            var generationJob = new TextureGenerationJob
            {
                fieldData = textureData,
                colorData = colorData,
                texWidth = tex_width,
                radius = radius,
                centerX = tex_width / 2f,
                centerY = tex_width / 2f,
                normalizer = 1f / (tex_width * tex_width),
                frequency = frequency,
                amplitude = amplitude,
                seed = seed
            };

            JobHandle jobHandle = generationJob.Schedule(tex_width * tex_width, 64);
            jobHandle.Complete();

            // --- STAGE 2: APPLY BLUR (if requested) ---
            if (blurIterations > 0)
            {
                // We need a second buffer to ping-pong data between for blurring
                NativeArray<float> blurBuffer = new NativeArray<float>(tex_width * tex_width, Allocator.TempJob);

                NativeArray<float> readBuffer = textureData;
                NativeArray<float> writeBuffer = blurBuffer;

                for (int i = 0; i < blurIterations; i++)
                {
                    var blurJob = new BlurJob
                    {
                        InputData = readBuffer,
                        OutputData = writeBuffer,
                        TexWidth = tex_width
                    };
                    
                    jobHandle = blurJob.Schedule(tex_width * tex_width, 64);
                    jobHandle.Complete();
                    
                    // Swap buffers for the next iteration
                    (readBuffer, writeBuffer) = (writeBuffer, readBuffer);
                }

                // Ensure the final result is in textureData. If blurIterations is odd,
                // the final data is in blurBuffer, so we copy it back.
                if (readBuffer != textureData)
                {
                    textureData.CopyFrom(readBuffer);
                }
                
                blurBuffer.Dispose();
            }

            // --- STAGE 2.5: CULL FLOATING ISLANDS ---
            if (cullIslands && amplitude > 0) // Only cull if there's noise that could create islands
            {
                if (tex_width <= 512)
                {
                    // Use serial flood fill for smaller textures
                    // IslandCuller.FloodFillSerial(textureData, tex_width);
                    IslandCuller.FloodFillParallel(textureData, tex_width);

                }
                else
                {
                    // Use parallel approach for larger textures
                    IslandCuller.FloodFillParallel(textureData, tex_width);
                }
            }

            // --- STAGE 3: UPLOAD DATA TO GPU TEXTURES ---
            Texture2D tempTexture = new Texture2D(tex_width, tex_width, TextureFormat.RFloat, false);
            Texture2D tempColorTexture = new Texture2D(tex_width, tex_width, TextureFormat.RGBAFloat, false);
    
            // Set pixel data on Texture2D
            tempTexture.SetPixelData(textureData, 0);
            tempTexture.Apply();
    
            tempColorTexture.SetPixelData(colorData, 0);
            tempColorTexture.Apply();
    
            // Blit to the final RenderTextures
            Graphics.Blit(tempTexture, inTEX.ScalarField);
            Graphics.Blit(tempColorTexture, inTEX.Colors);
    
            // Clean up temporary objects
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
            [WriteOnly] public NativeArray<float> fieldData; // Changed to float
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

                // Convert pixel coordinates to UV space (0-1 range) for resolution independence
                float2 uv = new float2((float)x / texWidth, (float)y / texWidth);
                float2 pos = uv * frequency + new float2(seed, seed);
                float nze = noise.snoise(pos);

                // Keep the original amplitude scaling but make it resolution-independent
                // The original code used amplitude * 0.01f, so we maintain that relative scale
                float scaledAmplitude = amplitude * 0.01f;

                // Determine pixel value based on radius
                float val = (normalizedDistanceSquared < (radius * radius) + (nze * scaledAmplitude) ? 1.0f : 0f);

                fieldData[index] = val;

                var mult = 0.02f;
                float h = noise.snoise(pos * mult) / 2f + 0.5f;

                var color = Color.HSVToRGB(h, 1.0f, 1.0f);
                
                colorData[index] = new float4(color.r, color.g, color.b, 1.0f);
            }
        }
        
        /// <summary>
        /// A Burst-compiled job that performs a 3x3 box blur on a float array.
        /// </summary>
        [BurstCompile(CompileSynchronously = true)]
        private struct BlurJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> InputData;
            [WriteOnly] public NativeArray<float> OutputData;
            [ReadOnly] public int TexWidth;

            public void Execute(int index)
            {
                int x = index % TexWidth;
                int y = index / TexWidth;

                // A simple way to handle edges is to not blur them.
                // For a small blur radius, this is often visually acceptable.
                if (x == 0 || x == TexWidth - 1 || y == 0 || y == TexWidth - 1)
                {
                    OutputData[index] = InputData[index];
                    return;
                }
                
                // Sum the values in a 3x3 kernel around the current pixel.
                float total = 0f;
                for (int j = -1; j <= 1; j++)
                {
                    for (int k = -1; k <= 1; k++)
                    {
                        // Calculate the 1D index of the neighboring pixel
                        int neighborIndex = (y + j) * TexWidth + (x + k);
                        total += InputData[neighborIndex];
                    }
                }
                
                // The output is the average of the 9 pixels.
                OutputData[index] = total / 9.0f;
            }
        }

        public void Dispose()
        {
            // Nothing to dispose here as textures are managed by the caller
        }
    }
}