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
        public Texture2D ScalarTex;
        public Texture3D ColorTex;
        public int Size { get; private set; }
        
        public FieldData2(int size, RasterData rasterData, VectorData vectorData)
        {
            Size = size;
            this.RasterData = rasterData;
            this.VectorData = vectorData;
            
            // Initialize textures
            ScalarTex = new Texture2D(size, size, TextureFormat.RFloat, false)
            {
                 filterMode = FilterMode.Point
                // wrapMode = TextureWrapMode.Clamp
            };
            // ScalarTex.

        }
    }
}
