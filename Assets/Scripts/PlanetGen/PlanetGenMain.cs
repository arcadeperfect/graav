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
    [Range(0, 0.2f)] public float lineWidth;
    
    public ComputeShader marchingSquaresShader;
    public ComputeShader segmentsToQuadsShader;
    public ComputeShader indirectArgsShader;
    public Material material;
    


    // private int vertexCount;

    private FieldGen fieldGen;

    private ComputePipeline computePipeline;
    private RenderPipeline renderPipeline;

    public new Renderer renderer;


    private FieldGen.TextureRegistry textures;

    private struct CachedParams
    {
        public int FieldWidth;
        public float Radius, Amplitude, Frequency, Iso, LineWidth;
        
        public bool HasChanged(PlanetGen gen)
        {
            return FieldWidth != gen.fieldWidth || 
                   !Mathf.Approximately(Radius, gen.radius) ||
                   !Mathf.Approximately(Amplitude, gen.amplitude) ||
                   !Mathf.Approximately(Frequency, gen.frequency) ||
                   !Mathf.Approximately(Iso, gen.iso) ||
                   !Mathf.Approximately(LineWidth, gen.lineWidth);
        }
    }

    private CachedParams cachedParams;


    public void Start()
    {
        print("start");
        Init();
        // Regen(fieldWidth);
        
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
    
    void Regen(int fieldWidth)
    {
        fieldGen.GetTex(textures, 0, radius, amplitude, frequency, fieldWidth);
        computePipeline.Dispatch(textures, iso, lineWidth);
        renderer.material.SetTexture("_FieldTex", textures.fields);
        renderer.material.SetTexture("_ColorTex", textures.colors);
    }


    public void Update()
    {
        if(cachedParams.HasChanged(this))
        {
            if (cachedParams.FieldWidth != this.fieldWidth)
            {
                computePipeline.InitBuffers(fieldWidth);
                AssignMaterialBuffers();
            }
            
            Regen(fieldWidth);
            UpdateCachedParams();
        }
        
        if (renderPipeline != null)
        {
            renderPipeline.Render(computePipeline.DrawArgsBuffer);
        }
        
    }

    private void AssignMaterialBuffers()
    {
        material.SetBuffer("VertexBuffer", computePipeline.VertexBuffer);
        material.SetBuffer("VertexColorBuffer", computePipeline.VertexColorBuffer);
    }
    
    public void OnDestroy()
    {
        computePipeline?.Dispose();
        // todo implement dispose on fieldGen
    }

    void UpdateCachedParams()
    {
        cachedParams = new CachedParams
        {
            FieldWidth = this.fieldWidth,
            Radius = this.radius,
            Amplitude = this.amplitude,
            Frequency = this.frequency,
            Iso = this.iso,
            LineWidth = this.lineWidth
        };
    }

    private class ComputePipeline : System.IDisposable
    {
        private PlanetGen parent;

        private ComputeBuffer segmentColorBuffer;
        private ComputeBuffer segmentBuffer;
        private ComputeBuffer segmentCountBuffer;
        private ComputeBuffer indirectArgsBuffer;
        private ComputeBuffer vertexCountBuffer;
        public ComputeBuffer VertexBuffer { get; private set; }
        public ComputeBuffer VertexColorBuffer { get; private set; }
        public ComputeBuffer DrawArgsBuffer { get; private set; }
        private readonly int[] counterReset = { 0 };
        private int fieldWidth;

        public ComputePipeline(PlanetGen parent)
        {
            this.parent = parent;
        }
        
        public void InitBuffers(int newFieldWidth)
        {
            DisposeBuffers();
            
            this.fieldWidth = newFieldWidth;
            int maxSegments = (newFieldWidth - 1) * (newFieldWidth - 1) * 4;
            
            segmentBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 4, ComputeBufferType.Default);
            segmentColorBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 8, ComputeBufferType.Default);
            segmentCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            VertexColorBuffer = new ComputeBuffer(maxSegments * 6, sizeof(float) * 4);
            VertexBuffer = new ComputeBuffer(maxSegments * 6, sizeof(float) * 3);
            indirectArgsBuffer = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
            vertexCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            DrawArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            DrawArgsBuffer.SetData(new int[4] { 0, 1, 1, 0});
            
            
            // segmentBuffer.SetData(new Vector4[maxSegments]);
            // segmentColorBuffer.SetData(new Vector4[maxSegments * 2]);
            // segmentCountBuffer.SetData(new int[1] { 0 });
            // float4[] warningColors = new float4[maxSegments * 6];
            // for (int i = 0; i < warningColors.Length; i++)
            // {
            //     warningColors[i] = new float4(1f, 0f, 1f, 0f);
            // }
            //
            // vertexColorBuffer.SetData(warningColors);
            // vertexBuffer.SetData(new Vector3[maxSegments * 6]);
            // indirectArgsBuffer.SetData(new int[3] { 0, 1, 1 });
            
            // parent.material.SetBuffer("VertexBuffer", VertexBuffer);
            // parent.material.SetBuffer("VertexColorBuffer", VertexColorBuffer);
        }

        public void Dispatch(FieldGen.TextureRegistry textures, float iso, float lineWidth)
        {

            
            segmentCountBuffer.SetData(counterReset);
            
            // Marching Squares
            int marchingKernel = parent.marchingSquaresShader.FindKernel("MarchingSquares");

            parent.marchingSquaresShader.SetBuffer(marchingKernel, "SegmentsBuffer", segmentBuffer);
            parent.marchingSquaresShader.SetBuffer(marchingKernel, "SegmentCountBuffer", segmentCountBuffer);
            parent.marchingSquaresShader.SetBuffer(marchingKernel, "SegmentColorsBuffer", segmentColorBuffer);
            parent.marchingSquaresShader.SetFloat("IsoValue", iso);
            parent.marchingSquaresShader.SetInt("TextureHeight", fieldWidth);
            parent.marchingSquaresShader.SetInt("TextureWidth", fieldWidth);
            parent.marchingSquaresShader.SetTexture(marchingKernel, "ScalarFieldTexture", textures.fields);
            parent.marchingSquaresShader.SetTexture(marchingKernel, "ColorFieldTexture", textures.colors);

            int threadGroups = Mathf.CeilToInt(fieldWidth / 8f);
            parent.marchingSquaresShader.Dispatch(marchingKernel, threadGroups, threadGroups, 1);

            //Segments to Quads
            
            int quadKernel = parent.segmentsToQuadsShader.FindKernel("CSMain");
            int prepareArgsKernel = parent.indirectArgsShader.FindKernel("PrepareQuadGenArgs");
            int threadGroupSize = 64;
            

            
            parent.indirectArgsShader.SetInt("ThreadGroupSize", threadGroupSize);
            parent.indirectArgsShader.SetBuffer(prepareArgsKernel, "SegmentCountBuffer", segmentCountBuffer);
            parent.indirectArgsShader.SetBuffer(prepareArgsKernel, "IndirectArgsBuffer", indirectArgsBuffer);
            parent.indirectArgsShader.Dispatch(prepareArgsKernel, 1, 1, 1);

            parent.segmentsToQuadsShader.SetBuffer(quadKernel, "segmentBuffer", segmentBuffer);
            parent.segmentsToQuadsShader.SetBuffer(quadKernel, "segmentColorBuffer", segmentColorBuffer);
            parent.segmentsToQuadsShader.SetBuffer(quadKernel, "vertexBuffer", VertexBuffer);
            parent.segmentsToQuadsShader.SetBuffer(quadKernel, "vertexColorBuffer", VertexColorBuffer);
            parent.segmentsToQuadsShader.SetBuffer(quadKernel, "segmentCountBuffer", segmentCountBuffer);
            parent.segmentsToQuadsShader.SetFloat("lineWidth", lineWidth);
            
            vertexCountBuffer.SetData(counterReset);
            parent.segmentsToQuadsShader.SetBuffer(quadKernel, "vertexCountBuffer", vertexCountBuffer);
            
            parent.segmentsToQuadsShader.DispatchIndirect(quadKernel, indirectArgsBuffer);
            
            int prepareDrawArgsKernel = parent.indirectArgsShader.FindKernel("PrepareDrawArgs");
            parent.indirectArgsShader.SetBuffer(prepareDrawArgsKernel, "DrawArgsBuffer", DrawArgsBuffer);
            parent.indirectArgsShader.SetBuffer(prepareDrawArgsKernel, "VertexCountBuffer", vertexCountBuffer);

            parent.indirectArgsShader.Dispatch(prepareDrawArgsKernel, 1, 1, 1);
          
        }
        
        void DisposeBuffers()
        {
            segmentBuffer?.Release();
            segmentColorBuffer?.Release();
            VertexBuffer?.Release();
            VertexColorBuffer?.Release();
            segmentCountBuffer?.Release();
            indirectArgsBuffer?.Release();
            vertexCountBuffer?.Release();
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
            // parent.material.SetBuffer("VertexBuffer", parent.computePipeline.VertexBuffer);
            // parent.material.SetBuffer("VertexColorBuffer", parent.computePipeline.VertexColorBuffer);
        }

        public void Render(ComputeBuffer drawArgsBuffer)
        {
            if (drawArgsBuffer == null) return;
            int[] actualVertexCount = new int[4];
            drawArgsBuffer.GetData(actualVertexCount);
            print(actualVertexCount[0]);
            Graphics.DrawProceduralIndirect(parent.material, bounds, MeshTopology.Triangles, parent.computePipeline.DrawArgsBuffer);
        }


    }

}