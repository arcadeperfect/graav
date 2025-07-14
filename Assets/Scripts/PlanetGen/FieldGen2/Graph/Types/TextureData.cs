using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen.FieldGen2.Graph.Types
{
    public class TextureData: IDisposable
    {
        public RenderTexture ScalarTex;
        public RenderTexture ColorTex;

        public TextureData(
            int size,
            // NativeArray<float> scalar,
            // NativeArray<float4> color
            RasterData rasterData
            )
        {
            ScalarTex = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            ScalarTex.enableRandomWrite = true;
            ScalarTex.Create();
            Texture2D scalarTemp = new Texture2D(size, size, TextureFormat.RFloat, false);
            scalarTemp.SetPixelData(rasterData.Scalar, 0);
            scalarTemp.Apply();
            Graphics.Blit(scalarTemp, ScalarTex);
            UnityEngine.Object.DestroyImmediate(scalarTemp);
            
            ColorTex = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            ColorTex.enableRandomWrite = true;
            ColorTex.Create();
            Texture2D colorTemp = new Texture2D(size, size, TextureFormat.RGBAFloat, false);
            colorTemp.SetPixelData(rasterData.Color, 0);
            colorTemp.Apply();
            Graphics.Blit(colorTemp, ColorTex);
            UnityEngine.Object.DestroyImmediate(colorTemp);
        }
        
        public void Dispose()
        {
            if (ScalarTex != null)
            {
                ScalarTex.Release();
                UnityEngine.Object.DestroyImmediate(ScalarTex);
                ScalarTex = null;
            }
            if (ColorTex != null)
            {
                ColorTex.Release();
                UnityEngine.Object.DestroyImmediate(ColorTex);
                ColorTex = null;
            }
        }
    }
}