using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations;
using static Unity.Mathematics.noise;

public class FieldGen
{
    // private FastNoiseLite simplex;
    // private Texture2D tex;

    public struct TextureRegistry
    {
        public Texture2D fields;
        public Texture2D colors;

        public TextureRegistry(int texture_width)
        {
            fields = new Texture2D(texture_width, texture_width, TextureFormat.RGBAFloat, false);
            fields.filterMode = FilterMode.Point;
            colors = new Texture2D(texture_width, texture_width, TextureFormat.RGBAFloat, false);
            colors.filterMode = FilterMode.Point;
        }
    }
    // public FieldGen()
    // {
    //     // simplex = new FastNoiseLite(base_seed);
    //     // simplex.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
    //     // this.tex = tex;
    //     // InitTexture(tex_dimension);
    // }


    // private void InitTexture(int tex_dimension)
    // {
    //     if (tex != null)
    //         Object.Destroy(tex);
    //     // tex = new Texture2D(tex_dimension, tex_dimension);
    //     tex = new Texture2D(tex_dimension, tex_dimension, TextureFormat.RGBAFloat, false);
    //     tex.filterMode = FilterMode.Point;
    // }

    public void GetTex(TextureRegistry in_tex, float seed, float radius, float amplitude, float frequency,
        int tex_width)
    {
        // if (tex_width != in_tex.width)
        //     InitTexture(tex_width);

        // Allocate native array for texture data
        // NativeArray<Color32> textureData = new NativeArray<Color32>(tex_width * tex_width, Allocator.TempJob);
        NativeArray<Color> textureData = new NativeArray<Color>(tex_width * tex_width, Allocator.TempJob);
        NativeArray<Color> colorData = new NativeArray<Color>(tex_width * tex_width, Allocator.TempJob);

        // Create and schedule the job
        TextureGenerationJob job = new TextureGenerationJob
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
            seed = 0
        };

        JobHandle jobHandle = job.Schedule(tex_width * tex_width, 64);
        jobHandle.Complete();

        // PrintNativeArray(textureData, tex_width);
        // PrintNativeArray(colorData, tex_width);

        // Apply the data to the texture
        in_tex.fields.SetPixelData(textureData, 0);
        in_tex.fields.Apply();

        in_tex.colors.SetPixelData(colorData, 0);
        in_tex.colors.Apply();

        // Clean up
        textureData.Dispose();
        colorData.Dispose();

        // return in_tex;
    }

    void PrintNativeArray<T>(NativeArray<T> textureData, int tex_width) where T : struct
    {
        string toPrint = "";
        int count = 0;
        for (int i = 0; i < tex_width; i++)
        {
            string thisLine = "";
            for (int j = 0; j < tex_width; j++)
            {
                int indx_x = count % tex_width;
                int indx_y = count / tex_width;
                int this_index = j * tex_width + indx_x;
                thisLine += (textureData[this_index]);
                thisLine += ("   ");
                count++;
            }

            toPrint += thisLine;
            toPrint += "\n";
        }

        Debug.Log(toPrint);
    }

    // public void UpdateTexture(Texture2D tex, float seed, float radius, float amplitude, float frequency, int tex_width)
    // {
    //     tex = GetTex(seed, radius, amplitude, frequency, tex_width);
    // }

    [BurstCompile(CompileSynchronously = true)]
    private struct TextureGenerationJob : IJobParallelFor
    {
        // [WriteOnly] public NativeArray<Color32> textureData;
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


            float2 pos = new float2(x * frequency, y * frequency);
            // float2 pos = new float2(x * frequency + seed, y * frequency + seed);
            // float nze = pnoise(pos, texWidth * texWidth);
            float nze = snoise(pos);


            // Determine pixel value based on radius
            byte val = (byte)(normalizedDistanceSquared < (radius * radius) + (nze * amplitude) ? 1.0f : 0);


            fielddData[index] = new Color(val, val, val, 1.0f);

            var mult = 0.02f;

            float h = snoise(pos * mult) / 2f + 0.5f;
            float s = snoise(pos * mult + 10.0f) / 2f + 0.5f;
            float v = snoise(pos * mult + 20.0f) / 2f + 0.5f;

            // var rgb_color = Color.HSVToRGB(h, s, v); 

            colorData[index] = Color.HSVToRGB(h, 1.0f, 1.0f);
        }
    }


    private float normalized_distance(Vector2 a, Vector2 b, float width)
    {
        float d = Vector2.Distance(a, b);
        return d / width;
    }

    private float distance_squared(float ax, float ay, float bx, float by)
    {
        return (ax - bx) * (ax - bx) + (ay - by) * (ay - by);
    }

    
}