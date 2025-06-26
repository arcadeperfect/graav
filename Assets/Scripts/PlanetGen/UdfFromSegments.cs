using UnityEngine;
using UnityEngine.Rendering;

namespace PlanetGen
{
    public class UdfFromSegments
    {
        private ComputeShader _udfShader;
        private int _buildGridKernel;
        private int _generateUdfKernel;

        private ComputeBuffer _gridIndicesBuffer;
        private ComputeBuffer _gridCellsBuffer;

        private int _gridResolution;
        private int _maxSegmentsPerCell;

        public UdfFromSegments(int gridResolution = 64, int maxSegmentsPerCell = 32)
        {
            _udfShader = CSP.UdfFromSegments.GetShader(); // Assumes a provider like in your example
            if (_udfShader == null)
            {
                Debug.LogError("UdfFromSegments shader not found!");
                return;
            }
            
            _buildGridKernel = _udfShader.FindKernel("BuildGrid");
            _generateUdfKernel = _udfShader.FindKernel("GenerateUDFFromGrid");

            this._gridResolution = gridResolution;
            this._maxSegmentsPerCell = maxSegmentsPerCell;
        }

        public void Init()
        {
            // Each cell needs a uint2 (startIndex, count)
            _gridIndicesBuffer?.Dispose();
            _gridIndicesBuffer = new ComputeBuffer(_gridResolution * _gridResolution, sizeof(uint) * 2, ComputeBufferType.Default);

            // Each cell can store up to _maxSegmentsPerCell segment indices (uint)
            _gridCellsBuffer?.Dispose();
            _gridCellsBuffer = new ComputeBuffer(_gridResolution * _gridResolution * _maxSegmentsPerCell, sizeof(uint), ComputeBufferType.Default);
        }
        
        public void GenerateUdf(ComputeBuffer segmentsBuffer, ComputeBuffer segmentCountBuffer, RenderTexture outputUdfTexture)
        {

            if (_udfShader == null || segmentsBuffer == null || segmentCountBuffer == null || outputUdfTexture == null)
            {
                Debug.LogError("Cannot generate UDF, missing resources.");
                return;
            }

            int textureRes = outputUdfTexture.width;
            
            // --- Clear grid buffers from previous frame ---
            // Set all cell counts to zero. We don't need to clear the _gridCellsBuffer.
            _gridIndicesBuffer.SetData(new uint[_gridResolution * _gridResolution * 2]);
            
            // --- PASS 1: Build the spatial grid ---
            _udfShader.SetInt("_GridResolution", _gridResolution);
            _udfShader.SetInt("_MaxSegmentsPerCell", _maxSegmentsPerCell);
            _udfShader.SetInt("_TextureResolution", textureRes);

            _udfShader.SetBuffer(_buildGridKernel, "_SegmentsBuffer", segmentsBuffer);
            _udfShader.SetBuffer(_buildGridKernel, "_SegmentCountBuffer", segmentCountBuffer);
            _udfShader.SetBuffer(_buildGridKernel, "_GridIndicesBuffer", _gridIndicesBuffer);
            _udfShader.SetBuffer(_buildGridKernel, "_GridCellsBuffer", _gridCellsBuffer);

            // Get segment count to dispatch correct number of threads
            // NOTE: No readback! We use an intermediate buffer to get the count.
            int[] segmentCountData = new int[1];
            segmentCountBuffer.GetData(segmentCountData); // This is a tiny, fast readback. A better way would be to use DispatchIndirect if the count is needed on GPU only. For dispatching from CPU, this is required.
            int segmentCount = segmentCountData[0];
            int buildThreads = Mathf.CeilToInt(segmentCount / 64.0f);
            if(buildThreads > 0)
                _udfShader.Dispatch(_buildGridKernel, buildThreads, 1, 1);

            // --- PASS 2: Generate the UDF from the grid ---
            _udfShader.SetTexture(_generateUdfKernel, "_UDFTexture", outputUdfTexture);
            // Re-set all buffers and params for the second kernel
            _udfShader.SetBuffer(_generateUdfKernel, "_SegmentsBuffer", segmentsBuffer);
            _udfShader.SetBuffer(_generateUdfKernel, "_GridIndicesBuffer", _gridIndicesBuffer);
            _udfShader.SetBuffer(_generateUdfKernel, "_GridCellsBuffer", _gridCellsBuffer);
            
            int udfThreads = Mathf.CeilToInt(textureRes / 8.0f);
            _udfShader.Dispatch(_generateUdfKernel, udfThreads, udfThreads, 1);
        }

        public void Dispose()
        {
            _gridIndicesBuffer?.Dispose();
            _gridCellsBuffer?.Dispose();
        }
    }
}