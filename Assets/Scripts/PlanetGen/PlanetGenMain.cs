using System;
using Unity.Mathematics;
using UnityEngine;

public class PlanetGen : MonoBehaviour
{
    [Header("Field")] public int fieldWidth = 512;
    [Range(0, 0.5f)] public float radius = 0.5f;
    [Range(0, 2f)] public float amplitude = 0.5f;
    [Range(0, 10f)] public float frequency = 0.5f;
    public float iso = 0.5f;
    [Range(0, 1f)] public float lineWidth;
    [Range(0, 5)] public int blur;


    public ComputeShader MarchingSqaresToQuadsShader;
    public ComputeShader PrepareArgsShader;

    public Material material;


    // private int vertexCount;

    private FieldGen fieldGen;

    private ComputePipeline computePipeline;
    private RenderPipeline renderPipeline;

    public new Renderer renderer;


    private FieldGen.TextureRegistry textures;

    private struct CachedFieldParams
    {
        public int FieldWidth, Blur;
        public float Radius, Amplitude, Frequency;

        public bool HasChanged(PlanetGen gen)
        {
            return FieldWidth != gen.fieldWidth
                   || Blur != gen.blur
                   || !Mathf.Approximately(Radius, gen.radius) ||
                   !Mathf.Approximately(Amplitude, gen.amplitude) ||
                   !Mathf.Approximately(Frequency, gen.frequency);
        }
    }

    private struct CachedComputeParams
    {
        public float Iso, LineWidth;

        public bool HasChanged(PlanetGen gen)
        {
            return
                !Mathf.Approximately(Iso, gen.iso) ||
                !Mathf.Approximately(LineWidth, gen.lineWidth);
        }
    }


    private CachedFieldParams cachedFieldParams;
    private CachedComputeParams cachedComputeParams;


    public void Start()
    {
        print("start");
        Init();
        // Regen(fieldWidth);
        RegenField();
    }

    void Init()
    {
        textures = new FieldGen.TextureRegistry(fieldWidth);
        fieldGen = new FieldGen();
        computePipeline = new ComputePipeline(this);
        computePipeline.InitBuffers(fieldWidth);
        AssignMaterialBuffers();
        renderPipeline = new RenderPipeline(this);
    }

    void RegenField()
    {
        if (cachedFieldParams.HasChanged(this))
        {
            // print("regen field");
            fieldGen.GetTex(ref textures, 0, radius, amplitude, frequency, fieldWidth, blur);
            computePipeline.InitBuffers(fieldWidth);
            AssignMaterialBuffers();
            UpdateCachedParams();
        }

        RegenCompute();
    }

    void RegenCompute()
    {
        computePipeline.Dispatch(textures, iso, lineWidth);
        renderer.material.SetTexture("_FieldTex", textures.fields);
        renderer.material.SetTexture("_ColorTex", textures.colors);
    }


    public void Update()
    {
        if (cachedFieldParams.HasChanged(this))
        {
            computePipeline.InitBuffers(fieldWidth);
            AssignMaterialBuffers();
            RegenField();
        }
        else if (cachedComputeParams.HasChanged(this))
        {
            RegenCompute();
        }

        if (renderPipeline != null)
        {
            renderPipeline.Render(computePipeline.DrawArgsBuffer);
        }

        UpdateCachedParams();
    }

    private void AssignMaterialBuffers()
    {
        material.SetBuffer("TriangleBuffer", computePipeline.TriangleBuffer);
    }

    public void OnDestroy()
    {
        computePipeline?.Dispose();
        // todo implement dispose on fieldGen
    }

    void UpdateCachedParams()
    {
        cachedFieldParams = new CachedFieldParams
        {
            FieldWidth = this.fieldWidth,
            Radius = this.radius,
            Amplitude = this.amplitude,
            Frequency = this.frequency,
            Blur = this.blur
        };

        cachedComputeParams = new CachedComputeParams()
        {
            Iso = this.iso,
            LineWidth = this.lineWidth
        };
    }

    private class ComputePipeline : System.IDisposable
    {
        private PlanetGen parent;


        public ComputeBuffer ContourVerticesBuffer { get; private set; }
        public ComputeBuffer TriangleBuffer { get; private set; }
        public ComputeBuffer TriangleCountBuffer { get; private set; }
        public ComputeBuffer DrawArgsBuffer { get; private set; }
        public ComputeBuffer VertexCountBuffer { get; private set; }

        private int fieldWidth;

        public ComputePipeline(PlanetGen parent)
        {
            this.parent = parent;
        }

        public void InitBuffers(int newFieldWidth)
        {
            DisposeBuffers();

            this.fieldWidth = newFieldWidth;
            int numCells = (newFieldWidth - 1) * (newFieldWidth - 1);
            int maxVertices = numCells * 12;
            ContourVerticesBuffer = new ComputeBuffer(maxVertices, 40, ComputeBufferType.Append);
            TriangleBuffer = new ComputeBuffer(maxVertices, sizeof(float) * 30, ComputeBufferType.Append);
            TriangleCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            VertexCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            DrawArgsBuffer = new ComputeBuffer(5, sizeof(int), ComputeBufferType.IndirectArguments);
            DrawArgsBuffer.SetData(new int[] { 0, 1, 0, 0, 0 });
        }

        // public void Dispatch(FieldGen.TextureRegistry textures, float iso, float lineWidth)
        // {
        //     TriangleBuffer.SetCounterValue(0);
        //
        //     var marchingKernel = parent.MarchingSqaresToQuadsShader.FindKernel("MarchingSquaresContour");
        //
        //     parent.MarchingSqaresToQuadsShader.SetTexture(marchingKernel, "ScalarFieldTexture", textures.fields);
        //     parent.MarchingSqaresToQuadsShader.SetTexture(marchingKernel, "ColorFieldTexture", textures.colors);
        //     parent.MarchingSqaresToQuadsShader.SetFloat("IsoValue", iso);
        //     parent.MarchingSqaresToQuadsShader.SetInt("TextureWidth", fieldWidth);
        //     parent.MarchingSqaresToQuadsShader.SetInt("TextureHeight", fieldWidth);
        //     parent.MarchingSqaresToQuadsShader.SetFloat("LineWidth", lineWidth);
        //     parent.MarchingSqaresToQuadsShader.SetBuffer(marchingKernel, "TriangleBuffer", TriangleBuffer);
        //
        //     // 1. Generate triangles
        //     int threadGroups = Mathf.CeilToInt((fieldWidth - 1) / 8f);
        //     parent.MarchingSqaresToQuadsShader.Dispatch(marchingKernel, threadGroups, threadGroups, 1);
        //
        //     // 2. Copy triangle count from TriangleBuffer to TriangleCountBuffer
        //     ComputeBuffer.CopyCount(TriangleBuffer, TriangleCountBuffer, 0);
        //
        //     // 3. Convert triangle count to vertex count
        //     var triangleMultKernel = parent.PrepareArgsShader.FindKernel("ConvertTriangleCountToVertexCount");
        //     parent.PrepareArgsShader.SetBuffer(triangleMultKernel, "TriangleCountBuffer", TriangleCountBuffer);
        //     parent.PrepareArgsShader.SetBuffer(triangleMultKernel, "VertexCountBuffer", VertexCountBuffer);
        //     parent.PrepareArgsShader.Dispatch(triangleMultKernel, 1, 1, 1);
        //
        //     // 4. Copy vertex count to draw args
        //     ComputeBuffer.CopyCount(VertexCountBuffer, DrawArgsBuffer, 0);
        // }

        public void Dispatch(FieldGen.TextureRegistry textures, float iso, float lineWidth)
        {
            TriangleBuffer.SetCounterValue(0);

            var marchingKernel = parent.MarchingSqaresToQuadsShader.FindKernel("MarchingSquaresContour");

            parent.MarchingSqaresToQuadsShader.SetTexture(marchingKernel, "ScalarFieldTexture", textures.fields);
            parent.MarchingSqaresToQuadsShader.SetTexture(marchingKernel, "ColorFieldTexture", textures.colors);
            parent.MarchingSqaresToQuadsShader.SetFloat("IsoValue", iso);
            parent.MarchingSqaresToQuadsShader.SetInt("TextureWidth", fieldWidth);
            parent.MarchingSqaresToQuadsShader.SetInt("TextureHeight", fieldWidth);
            parent.MarchingSqaresToQuadsShader.SetFloat("LineWidth", lineWidth);
            parent.MarchingSqaresToQuadsShader.SetBuffer(marchingKernel, "TriangleBuffer", TriangleBuffer);

            Debug.Log($"=== COMPUTE DISPATCH DEBUG ===");
            Debug.Log($"Field size: {fieldWidth}x{fieldWidth}, Iso: {iso}, LineWidth: {lineWidth}");

            // 1. Generate triangles
            int threadGroups = Mathf.CeilToInt((fieldWidth - 1) / 8f);
            Debug.Log($"Dispatching {threadGroups}x{threadGroups} thread groups");
            parent.MarchingSqaresToQuadsShader.Dispatch(marchingKernel, threadGroups, threadGroups, 1);

            // 2. Copy triangle count from TriangleBuffer to TriangleCountBuffer
            ComputeBuffer.CopyCount(TriangleBuffer, TriangleCountBuffer, 0);

            // DEBUG: Read triangle count
            int[] triangleCountData = new int[1];
            TriangleCountBuffer.GetData(triangleCountData);
            Debug.Log($"Triangles generated: {triangleCountData[0]}");

            // 3. Convert triangle count to vertex count
            var triangleMultKernel = parent.PrepareArgsShader.FindKernel("ConvertTriangleCountToVertexCount");
            parent.PrepareArgsShader.SetBuffer(triangleMultKernel, "TriangleCountBuffer", TriangleCountBuffer);
            parent.PrepareArgsShader.SetBuffer(triangleMultKernel, "VertexCountBuffer", VertexCountBuffer);
            parent.PrepareArgsShader.Dispatch(triangleMultKernel, 1, 1, 1);

            // DEBUG: Read vertex count
            int[] vertexCountData = new int[1];
            VertexCountBuffer.GetData(vertexCountData);
            Debug.Log($"Vertex count: {vertexCountData[0]}");

            // 4. Copy vertex count to draw args
            var copyKernel = parent.PrepareArgsShader.FindKernel("CopyVertexCountToDrawArgs");
            parent.PrepareArgsShader.SetBuffer(copyKernel, "VertexCountBuffer", VertexCountBuffer);
            parent.PrepareArgsShader.SetBuffer(copyKernel, "DrawArgsBuffer", DrawArgsBuffer);
            parent.PrepareArgsShader.Dispatch(copyKernel, 1, 1, 1);

            // DEBUG: Read draw args
            int[] drawArgsData = new int[5];
            DrawArgsBuffer.GetData(drawArgsData);
            Debug.Log(
                $"Draw args: [{drawArgsData[0]}, {drawArgsData[1]}, {drawArgsData[2]}, {drawArgsData[3]}, {drawArgsData[4]}]");

            // DEBUG: Sample a few triangles if any exist
            if (triangleCountData[0] > 0)
            {
                // Create a temp buffer to read triangle data (only read first few triangles)
                int trianglesToRead = Mathf.Min(3, triangleCountData[0]);
                var tempTriangleData = new float[trianglesToRead * 30]; // 30 floats per triangle

                // This is a bit hacky since we can't directly read from AppendStructuredBuffer
                // But we can try to read the underlying data
                try
                {
                    // Note: This might not work directly with AppendStructuredBuffer
                    // TriangleBuffer.GetData(tempTriangleData, 0, 0, trianglesToRead * 30);
                    Debug.Log($"First triangle position data would be here (can't easily read from AppendBuffer)");
                }
                catch (System.Exception e)
                {
                    Debug.Log($"Couldn't read triangle data: {e.Message}");
                }
            }

            Debug.Log($"=== END DEBUG ===\n");
        }

        void DisposeBuffers()
        {
            ContourVerticesBuffer?.Release();
            DrawArgsBuffer?.Release();
        }

        public void Dispose()
        {
            DisposeBuffers();
        }
    }


    private class RenderPipeline
    {
        private PlanetGen parent;
        private Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 50f);

        public RenderPipeline(PlanetGen parent)
        {
            this.parent = parent;
        }

        public void Render(ComputeBuffer drawArgsBuffer)
        {
            if (drawArgsBuffer == null) return;

            Graphics.DrawProceduralIndirect(parent.material, bounds, MeshTopology.Triangles,
                drawArgsBuffer, 0);
        }
    }

    public Texture2D CreateSolidColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(fieldWidth, fieldWidth, TextureFormat.RGBAFloat, false);


        Color32 color32 = color;
        Color32[] pixels = new Color32[fieldWidth * fieldWidth];


        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color32;
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return texture;
    }
}