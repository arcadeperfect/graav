using System;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Types;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen.FieldGen2.Types
{
    public class FieldData2
    {

        public RasterData RasterData;
        public VectorData VectorData;
        public RenderTexture ScalarTex;
        public RenderTexture ColorTex;
        public int Size { get; private set; }
        
        public FieldData2(int size, RasterData rasterData, VectorData vectorData)
        {
            Size = size;
            this.RasterData = rasterData;
            this.VectorData = vectorData;

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
    }
}
