using PlanetGen.FieldGen2.Graph.Types;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen.FieldGen2.Graph
{
    public static class GPUVectorRasterizer
    {
        private static ComputeShader rasterizeShader;
        private static int kernelIndex = -1;
        
        // Initialize the compute shader (call this once at startup)
        public static void Initialize()
        {
            if (rasterizeShader == null)
            {
                rasterizeShader = Resources.Load<ComputeShader>("VectorRasterizer");
                if (rasterizeShader == null)
                {
                    Debug.LogError("VectorRasterizer compute shader not found in Resources folder!");
                    return;
                }
                kernelIndex = rasterizeShader.FindKernel("CSMain");
            }
        }
        
        public static JobHandle RasterizeVector(VectorData vectorData, int textureSize, float worldSize,
            ref RasterData rasterData, JobHandle dependency = default)
        {
            if (rasterizeShader == null)
            {
                Initialize();
                if (rasterizeShader == null)
                {
                    Debug.LogError("Failed to initialize compute shader, falling back to CPU rasterizer");
                    return VectorRasterizer.RasterizeVector(vectorData, textureSize, worldSize, ref rasterData, dependency);
                }
            }
            
            // Wait for dependency (though GPU work is async)
            dependency.Complete();
            
            if (!vectorData.IsValid || vectorData.Count < 3)
            {
                FillDefaultRasterData(ref rasterData, textureSize);
                return new JobHandle(); // Return completed handle
            }
            
            // Create GPU buffers
            ComputeBuffer vertexBuffer = new ComputeBuffer(vectorData.Count, sizeof(float) * 2);
            
            // Copy vertex data to GPU
            float2[] vertices = new float2[vectorData.Count];
            for (int i = 0; i < vectorData.Count; i++)
            {
                vertices[i] = vectorData.Vertices[i];
            }
            vertexBuffer.SetData(vertices);
            
            // Create render textures for output
            RenderTexture scalarTexture = CreateRenderTexture(textureSize, RenderTextureFormat.RFloat);
            RenderTexture altitudeTexture = CreateRenderTexture(textureSize, RenderTextureFormat.RFloat);  
            RenderTexture colorTexture = CreateRenderTexture(textureSize, RenderTextureFormat.ARGBFloat);
            RenderTexture angleTexture = CreateRenderTexture(textureSize, RenderTextureFormat.RFloat);
            
            // Set compute shader parameters
            rasterizeShader.SetBuffer(kernelIndex, "Vertices", vertexBuffer);
            rasterizeShader.SetInt("VertexCount", vectorData.Count);
            rasterizeShader.SetFloat("WorldSize", worldSize);
            rasterizeShader.SetInt("TextureSize", textureSize);
            
            rasterizeShader.SetTexture(kernelIndex, "ScalarResult", scalarTexture);
            rasterizeShader.SetTexture(kernelIndex, "AltitudeResult", altitudeTexture);
            rasterizeShader.SetTexture(kernelIndex, "ColorResult", colorTexture);
            rasterizeShader.SetTexture(kernelIndex, "AngleResult", angleTexture);
            
            // Dispatch compute shader
            int threadGroups = Mathf.CeilToInt(textureSize / 8.0f);
            rasterizeShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);
            
            // Read back results to NativeArrays
            ReadRenderTextureToNativeArray(scalarTexture, rasterData.Scalar, textureSize);
            ReadRenderTextureToNativeArray(altitudeTexture, rasterData.Altitude, textureSize);  
            ReadRenderTextureToNativeArray(colorTexture, rasterData.Color, textureSize);
            ReadRenderTextureToNativeArray(angleTexture, rasterData.Angle, textureSize);
            
            // Cleanup GPU resources
            vertexBuffer.Release();
            scalarTexture.Release();
            altitudeTexture.Release();
            colorTexture.Release();
            angleTexture.Release();
            
            return new JobHandle(); // GPU work is async, return completed handle
        }
        
        private static RenderTexture CreateRenderTexture(int size, RenderTextureFormat format)
        {
            RenderTexture rt = new RenderTexture(size, size, 0, format);
            rt.enableRandomWrite = true;
            rt.Create();
            return rt;
        }
        
        private static void ReadRenderTextureToNativeArray(RenderTexture rt, NativeArray<float> output, int textureSize)
        {
            // Create temporary texture for readback
            Texture2D temp = new Texture2D(textureSize, textureSize, TextureFormat.RFloat, false);
            
            RenderTexture.active = rt;
            temp.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
            temp.Apply();
            RenderTexture.active = null;
            
            // Copy to NativeArray
            var pixels = temp.GetRawTextureData<float>();
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = pixels[i];
            }
            
            Object.DestroyImmediate(temp);
        }
        
        private static void ReadRenderTextureToNativeArray(RenderTexture rt, NativeArray<float4> output, int textureSize)
        {
            // Create temporary texture for readback
            Texture2D temp = new Texture2D(textureSize, textureSize, TextureFormat.RGBAFloat, false);
            
            RenderTexture.active = rt;
            temp.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
            temp.Apply();
            RenderTexture.active = null;
            
            // Copy to NativeArray
            var pixels = temp.GetRawTextureData<float4>();
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = pixels[i];
            }
            
            Object.DestroyImmediate(temp);
        }
        
        private static void FillDefaultRasterData(ref RasterData rasterData, int textureSize)
        {
            for (int i = 0; i < textureSize * textureSize; i++)
            {
                rasterData.Scalar[i] = 0f;
                rasterData.Altitude[i] = 0f;
                rasterData.Color[i] = new float4(0.1f, 0.3f, 0.8f, 1f);
                rasterData.Angle[i] = 0f;
            }
        }
    }
}