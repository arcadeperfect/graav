using System;
using NUnit.Framework;
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
        public RenderTexture ScalarFieldTexture;
        public RenderTexture Colors;
        public int Width { get; private set; }

        public bool IsDataValid { get; } = false;

        public FieldData2(int size, RasterData rasterData, VectorData vectorData)
        {
            Width = size;
            this.RasterData = rasterData;
            this.VectorData = vectorData;

            ScalarFieldTexture = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            ScalarFieldTexture.enableRandomWrite = true;
            ScalarFieldTexture.Create();
            Texture2D scalarTemp = new Texture2D(size, size, TextureFormat.RFloat, false);
            scalarTemp.SetPixelData(rasterData.Scalar, 0);
            scalarTemp.Apply();
            Graphics.Blit(scalarTemp, ScalarFieldTexture);
            UnityEngine.Object.DestroyImmediate(scalarTemp);
            
            Colors = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Colors.enableRandomWrite = true;
            Colors.Create();
            Texture2D colorTemp = new Texture2D(size, size, TextureFormat.RGBAFloat, false);
            colorTemp.SetPixelData(rasterData.Color, 0);
            colorTemp.Apply();
            Graphics.Blit(colorTemp, Colors);
            UnityEngine.Object.DestroyImmediate(colorTemp);
            
            
            IsDataValid = true;
        }
        
        
    }
}
