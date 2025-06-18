using Unity.Mathematics;
using UnityEngine;
using System.Linq;

public class _PlanetGen : MonoBehaviour
{
    [Header("Field Parameters")]
    public int fieldWidth = 512;
    [Range(0, 0.5f)] public float radius = 0.5f;
    [Range(0, 2f)] public float amplitude = 0.5f;
    [Range(0, 10f)] public float frequency = 0.5f;

    [Header("Marching Squares")]
    public ComputeShader marchingSquaresShader;
    public ComputeShader segmentsToQuadsShader;
    public ComputeShader indirectArgsShader;
    public Material material;
    [Range(0.001f, 0.1f)] public float lineWidth = 0.005f;
    [Range(0f, 1f)] public float iso = 0.5f;

    [Header("Rendering")]
    public Renderer fieldRenderer;
    
    [Header("Debug")]
    public bool autoRegenerate = false;
    public bool showDebugInfo = true;

    // Pipeline Components
    private FieldGen fieldGen;
    private ComputePipeline computePipeline;
    private RenderPipeline renderPipeline;

    // Cached parameters for change detection
    private struct CachedParams
    {
        public int fieldWidth;
        public float radius, amplitude, frequency, iso, lineWidth;
        
        public bool HasChanged(_PlanetGen gen)
        {
            return fieldWidth != gen.fieldWidth || 
                   !Mathf.Approximately(radius, gen.radius) ||
                   !Mathf.Approximately(amplitude, gen.amplitude) ||
                   !Mathf.Approximately(frequency, gen.frequency) ||
                   !Mathf.Approximately(iso, gen.iso) ||
                   !Mathf.Approximately(lineWidth, gen.lineWidth);
        }
    }
    private CachedParams cachedParams;

    public struct SegmentColor
    {
        public Vector4 Color1;
        public Vector4 Color2;

        public SegmentColor(Vector4 color1, Vector4 color2)
        {
            Color1 = color1;
            Color2 = color2;
        }
    }

    #region Unity Lifecycle
    
    void Start()
    {
        InitializePipeline();
        RegenerateAll();
    }

    void Update()
    {
        if (autoRegenerate && cachedParams.HasChanged(this))
        {
            RegenerateAll();
        }
    }

    void OnRenderObject()
    {
        // Only render if we're in play mode and have valid data
        if (!Application.isPlaying) return;
        if (Camera.current == null) return;
        
        renderPipeline?.Render();
    }

    void OnDestroy()
    {
        CleanupPipeline();
    }

    void OnValidate()
    {
        // Clamp values to prevent issues
        fieldWidth = Mathf.Max(32, fieldWidth);
        lineWidth = Mathf.Max(0.001f, lineWidth);
    }

    #endregion

    #region Public Interface

    [ContextMenu("Regenerate All")]
    public void RegenerateAll()
    {
        try
        {
            GenerateField();
            RunComputePipeline();
            UpdateCachedParams();
            
            if (showDebugInfo)
                LogPipelineStats();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Pipeline execution failed: {e.Message}");
        }
    }

    [ContextMenu("Regenerate Field Only")]
    public void RegenerateFieldOnly()
    {
        GenerateField();
        UpdateCachedParams();
    }

    [ContextMenu("Run Compute Pipeline Only")]
    public void RunComputePipelineOnly()
    {
        RunComputePipeline();
    }

    #endregion

    #region Pipeline Management

    void InitializePipeline()
    {
        fieldGen = new FieldGen();
        computePipeline = new ComputePipeline(this);
        renderPipeline = new RenderPipeline(this);
        
        ValidateShaders();
    }

    void CleanupPipeline()
    {
        computePipeline?.Dispose();
        renderPipeline?.Dispose();
    }

    void ValidateShaders()
    {
        if (!marchingSquaresShader) Debug.LogError("Marching Squares Shader not assigned!");
        if (!segmentsToQuadsShader) Debug.LogError("Segments to Quads Shader not assigned!");
        if (!indirectArgsShader) Debug.LogError("Indirect Args Shader not assigned!");
        if (!material) Debug.LogError("Material not assigned!");
    }

    void UpdateCachedParams()
    {
        cachedParams = new CachedParams
        {
            fieldWidth = this.fieldWidth,
            radius = this.radius,
            amplitude = this.amplitude,
            frequency = this.frequency,
            iso = this.iso,
            lineWidth = this.lineWidth
        };
    }

    #endregion

    #region Field Generation

    void GenerateField()
    {
        var textures = new FieldGen.TextureRegistry(fieldWidth);
        fieldGen.GetTex(textures, 0, radius, amplitude, frequency, fieldWidth);

        if (fieldRenderer)
        {
            fieldRenderer.material.SetTexture("_FieldTex", textures.fields);
            fieldRenderer.material.SetTexture("_ColorTex", textures.colors);
        }

        computePipeline.SetFieldTextures(textures.fields, textures.colors);
    }

    #endregion

    #region Compute Pipeline

    void RunComputePipeline()
    {
        computePipeline.Execute(fieldWidth, iso, lineWidth);
        renderPipeline.UpdateBuffers(computePipeline.GetVertexBuffer(), computePipeline.GetVertexColorBuffer());
    }

    void LogPipelineStats()
    {
        int segmentCount = computePipeline.GetSegmentCount();
        int vertexCount = computePipeline.GetVertexCount();
        Debug.Log($"Pipeline Stats - Segments: {segmentCount}, Vertices: {vertexCount}");
    }

    #endregion

    #region Nested Classes

    private class ComputePipeline : System.IDisposable
    {
        private readonly _PlanetGen parent;
        private ComputeBuffer segmentBuffer;
        private ComputeBuffer segmentColorBuffer;
        private ComputeBuffer vertexBuffer;
        private ComputeBuffer vertexColorBuffer;
        private ComputeBuffer segmentCountBuffer;
        private ComputeBuffer indirectArgsBuffer;
        
        private Texture2D fieldTexture;
        private Texture2D colorTexture;
        private int maxSegments;
        private bool isInitialized;

        public ComputePipeline(_PlanetGen parent)
        {
            this.parent = parent;
        }

        public void Execute(int fieldWidth, float isoValue, float lineWidth)
        {
            InitializeBuffers(fieldWidth);
            RunMarchingSquares(fieldWidth, isoValue);
            PrepareIndirectArgs();
            ConvertSegmentsToQuads(lineWidth);
        }

        void InitializeBuffers(int fieldWidth)
        {
            int newMaxSegments = (fieldWidth - 1) * (fieldWidth - 1) * 4;
            
            if (isInitialized && newMaxSegments == maxSegments) return;

            DisposeBuffers();
            maxSegments = newMaxSegments;

            try
            {
                segmentBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 4);
                segmentColorBuffer = new ComputeBuffer(maxSegments, sizeof(float) * 8);
                segmentCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
                vertexBuffer = new ComputeBuffer(maxSegments * 6, sizeof(float) * 3);
                vertexColorBuffer = new ComputeBuffer(maxSegments * 6, sizeof(float) * 4);
                indirectArgsBuffer = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);

                // Initialize with empty data
                segmentBuffer.SetData(new Vector4[maxSegments]);
                var emptyColors = new SegmentColor[maxSegments];
                for (int i = 0; i < maxSegments; i++)
                    emptyColors[i] = new SegmentColor(Vector4.zero, Vector4.zero);
                segmentColorBuffer.SetData(emptyColors);
                segmentCountBuffer.SetData(new int[] { 0 });
                vertexBuffer.SetData(new Vector3[maxSegments * 6]);
                vertexColorBuffer.SetData(new Vector4[maxSegments * 6]);
                indirectArgsBuffer.SetData(new int[] { 0, 1, 1 });

                isInitialized = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize compute buffers: {e.Message}");
                DisposeBuffers();
                throw;
            }
        }

        void RunMarchingSquares(int fieldWidth, float isoValue)
        {
            if (!parent.marchingSquaresShader || fieldTexture == null) return;

            int kernel = parent.marchingSquaresShader.FindKernel("MarchingSquares");
            
            parent.marchingSquaresShader.SetBuffer(kernel, "SegmentsBuffer", segmentBuffer);
            parent.marchingSquaresShader.SetBuffer(kernel, "SegmentCountBuffer", segmentCountBuffer);
            parent.marchingSquaresShader.SetBuffer(kernel, "SegmentColorsBuffer", segmentColorBuffer);
            parent.marchingSquaresShader.SetFloat("IsoValue", isoValue);
            parent.marchingSquaresShader.SetInt("TextureHeight", fieldWidth);
            parent.marchingSquaresShader.SetInt("TextureWidth", fieldWidth);
            parent.marchingSquaresShader.SetTexture(kernel, "ScalarFieldTexture", fieldTexture);
            parent.marchingSquaresShader.SetTexture(kernel, "ColorFieldTexture", colorTexture);
            
            int threadGroups = Mathf.CeilToInt(fieldWidth / 8f);
            parent.marchingSquaresShader.Dispatch(kernel, threadGroups, threadGroups, 1);
        }

        void PrepareIndirectArgs()
        {
            if (!parent.indirectArgsShader) return;

            int kernel = parent.indirectArgsShader.FindKernel("PrepareIndirectArgs");
            parent.indirectArgsShader.SetInt("ThreadGroupSize", 64);
            parent.indirectArgsShader.SetBuffer(kernel, "SegmentCountBuffer", segmentCountBuffer);
            parent.indirectArgsShader.SetBuffer(kernel, "IndirectArgsBuffer", indirectArgsBuffer);
            parent.indirectArgsShader.Dispatch(kernel, 1, 1, 1);
        }

        void ConvertSegmentsToQuads(float lineWidth)
        {
            if (!parent.segmentsToQuadsShader) return;

            int kernel = parent.segmentsToQuadsShader.FindKernel("CSMain");
            parent.segmentsToQuadsShader.SetBuffer(kernel, "segmentBuffer", segmentBuffer);
            parent.segmentsToQuadsShader.SetBuffer(kernel, "segmentColorBuffer", segmentColorBuffer);
            parent.segmentsToQuadsShader.SetBuffer(kernel, "vertexBuffer", vertexBuffer);
            parent.segmentsToQuadsShader.SetBuffer(kernel, "vertexColorBuffer", vertexColorBuffer);
            parent.segmentsToQuadsShader.SetBuffer(kernel, "segmentCountBuffer", segmentCountBuffer);
            parent.segmentsToQuadsShader.SetFloat("lineWidth", lineWidth);
            parent.segmentsToQuadsShader.DispatchIndirect(kernel, indirectArgsBuffer);
        }

        public void SetFieldTextures(Texture2D field, Texture2D color)
        {
            fieldTexture = field;
            colorTexture = color;
        }

        public ComputeBuffer GetVertexBuffer() => vertexBuffer;
        public ComputeBuffer GetVertexColorBuffer() => vertexColorBuffer;
        
        public int GetSegmentCount()
        {
            if (segmentCountBuffer == null) return 0;
            int[] data = new int[1];
            segmentCountBuffer.GetData(data);
            return data[0];
        }

        public int GetVertexCount() => vertexBuffer?.count ?? 0;

        void DisposeBuffers()
        {
            segmentBuffer?.Release();
            segmentColorBuffer?.Release();
            vertexBuffer?.Release();
            vertexColorBuffer?.Release();
            segmentCountBuffer?.Release();
            indirectArgsBuffer?.Release();
        }

        public void Dispose()
        {
            DisposeBuffers();
            isInitialized = false;
        }
    }

    private class RenderPipeline : System.IDisposable
    {
        private readonly _PlanetGen parent;
        private ComputeBuffer currentVertexBuffer;
        private ComputeBuffer currentVertexColorBuffer;

        public RenderPipeline(_PlanetGen parent)
        {
            this.parent = parent;
        }

        public void UpdateBuffers(ComputeBuffer vertexBuffer, ComputeBuffer vertexColorBuffer)
        {
            currentVertexBuffer = vertexBuffer;
            currentVertexColorBuffer = vertexColorBuffer;

            if (parent.material != null)
            {
                parent.material.SetBuffer("VertexBuffer", vertexBuffer);
                parent.material.SetBuffer("VertexColorBuffer", vertexColorBuffer);
            }
        }

        public void Render()
        {
            if (currentVertexBuffer == null || parent.material == null) return;
            if (currentVertexBuffer.count == 0) return;

            // Ensure we're not rendering during inappropriate times
            if (Camera.current == null) return;

            try
            {
                Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 50f);
                Graphics.DrawProcedural(parent.material, bounds, MeshTopology.Triangles, currentVertexBuffer.count);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Render failed: {e.Message}");
            }
        }

        public void Dispose()
        {
            // Buffers are owned by ComputePipeline, so we don't dispose them here
            currentVertexBuffer = null;
            currentVertexColorBuffer = null;
        }
    }

    #endregion

    #region Utility Methods

    public static Color RandomColor()
    {
        return new Color(
            UnityEngine.Random.Range(0f, 1f),
            UnityEngine.Random.Range(0f, 1f),
            UnityEngine.Random.Range(0f, 1f),
            1f
        );
    }

    #endregion
}