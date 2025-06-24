// using System;
// using UnityEditor.UI;
// using UnityEngine;
// using static PlanetGen.ComputeShaderConstants;
//
// namespace PlanetGen
// {
//     public class JumpFlooder: IDisposable
//     {
//         private readonly ComputeShader jumpFloodShader;
//         private readonly int jumpFloodKernel;
//         private readonly int seedFromSegmentsKernel;
//         private readonly int seedFromScalarFieldKernel;
//         private readonly int finalizeKernel;
//         
//
//         // JFA intermediate textures
//         private RenderTexture seedTexture;
//         private RenderTexture jfaTempTexture;
//
//         private int textureResolution;
//
//         public JumpFlooder()
//         {
//             jumpFloodShader = Resources.Load<ComputeShader>(JumpFloodCompute.Path);
//             jumpFloodKernel = jumpFloodShader.FindKernel(JumpFloodCompute.Kernels.JumpFlood);
//             seedFromSegmentsKernel = jumpFloodShader.FindKernel(JumpFloodCompute.Kernels.SeedFromSegments);
//             seedFromScalarFieldKernel = jumpFloodShader.FindKernel(JumpFloodCompute.Kernels.SeedFromScalarField);
//             finalizeKernel = jumpFloodShader.FindKernel(JumpFloodCompute.Kernels.FinalizeSDF);
//         }
//
//         public void InitJFATextures(int textureRes)
//         {
//             this.textureResolution = textureRes;
//
//             if (seedTexture != null) seedTexture.Release();
//             seedTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBFloat)
//             {
//                 enableRandomWrite = true,
//                 filterMode = FilterMode.Point // Important: use Point filtering for JFA
//             };
//             seedTexture.Create();
//
//             if (jfaTempTexture != null) jfaTempTexture.Release();
//             jfaTempTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBFloat)
//             {
//                 enableRandomWrite = true,
//                 filterMode = FilterMode.Point
//             };
//             jfaTempTexture.Create();
//         }
//         
//         
//
//         public void RunJumpFlood()
//         {
//             var shader = jumpFloodShader;
//
//             RenderTexture ping = seedTexture;
//             RenderTexture pong = jfaTempTexture;
//
//             // Calculate jump distances: start from half texture size, divide by 2 each iteration
//             int maxJump = Mathf.NextPowerOfTwo(textureResolution) / 2;
//
//             for (int jump = maxJump; jump >= 1; jump /= 2)
//             {
//                 shader.SetTexture(jumpFloodKernel, "_InputTexture", ping);
//                 shader.SetTexture(jumpFloodKernel, "_OutputTexture", pong);
//                 shader.SetInt("_JumpDistance", jump);
//                 shader.SetInt("_TextureResolution", textureResolution);
//
//                 int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
//                 shader.Dispatch(jumpFloodKernel, threadGroups, threadGroups, 1);
//
//                 // Swap ping and pong for next iteration
//                 (ping, pong) = (pong, ping);
//             }
//
//             // Final result is in 'ping' - copy to seedTexture if needed
//             if (ping != seedTexture)
//             {
//                 Graphics.CopyTexture(ping, seedTexture);
//             }
//         }
//         public void FinalizeSDF(RenderTexture outputTexture, bool outputUnsigned, RenderTexture scalarField,
//             float isoValue)
//         {
//             var shader = jumpFloodShader;
//
//             shader.SetTexture(finalizeKernel, "_JFAResult", seedTexture);
//             shader.SetTexture(finalizeKernel, "_SDFTexture", outputTexture);
//             shader.SetInt("_TextureResolution", textureResolution);
//             shader.SetBool("_OutputUnsigned", outputUnsigned);
//
//             // Set scalar field and iso value if provided (for sign determination)
//             if (scalarField != null)
//             {
//                 shader.SetTexture(finalizeKernel, "_ScalarField", scalarField);
//                 shader.SetFloat("_IsoValue", isoValue);
//                 shader.SetBool("_HasScalarField", true);
//             }
//             else
//             {
//                 shader.SetBool("_HasScalarField", false);
//             }
//
//             int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
//             shader.Dispatch(finalizeKernel, threadGroups, threadGroups, 1);
//         }
//         
//         public void GenerateSeedsFromSegments(ComputeBuffer segmentsBuffer, ComputeBuffer segmentCountBuffer)
//         {
//             var shader = jumpFloodShader;
//         
//             shader.SetBuffer(seedFromSegmentsKernel, "_Segments", segmentsBuffer);
//             shader.SetBuffer(seedFromSegmentsKernel, "_SegmentCount", segmentCountBuffer);
//             shader.SetTexture(seedFromSegmentsKernel, "_SeedTexture", seedTexture);
//             shader.SetInt("_TextureResolution", textureResolution);
//         
//             int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
//             shader.Dispatch(seedFromSegmentsKernel, threadGroups, threadGroups, 1);
//         }
//         
//         public void GenerateSeedsFromScalarField(RenderTexture scalarField, RenderTexture seedTexture, float isoValue)
//         {
//             var shader = jumpFloodShader;
//             this.seedTexture = seedTexture;
//             shader.SetTexture(seedFromScalarFieldKernel, "_ScalarField", scalarField);
//             shader.SetFloat("_IsoValue", isoValue);
//             shader.SetTexture(seedFromScalarFieldKernel, "_SeedTexture", seedTexture);
//             shader.SetInt("_TextureResolution", textureResolution);
//
//             int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
//             shader.Dispatch(seedFromScalarFieldKernel, threadGroups, threadGroups, 1);
//         }
//         
//         public void GenerateSeedsFromScalarField(RenderTexture scalarField, float isoValue)
//         {
//             var shader = jumpFloodShader;
//     
//             shader.SetTexture(seedFromScalarFieldKernel, "_ScalarField", scalarField);
//             shader.SetFloat("_IsoValue", isoValue);
//     
//             // Use the seedTexture that belongs to this class instance
//             shader.SetTexture(seedFromScalarFieldKernel, "_SeedTexture", this.seedTexture); 
//     
//             shader.SetInt("_TextureResolution", textureResolution);
//
//             int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
//             shader.Dispatch(seedFromScalarFieldKernel, threadGroups, threadGroups, 1);
//         }
//         
//         // ADDED: Dispose method to properly clean up resources
//         public void Dispose()
//         {
//             if (seedTexture != null)
//             {
//                 seedTexture.Release();
//                 seedTexture = null;
//             }
//             
//             if (jfaTempTexture != null)
//             {
//                 jfaTempTexture.Release();
//                 jfaTempTexture = null;
//             }
//         }
//     }
// }

using System;
using UnityEngine;

namespace PlanetGen
{
    public class JumpFlooder : IDisposable
    {
        private readonly ComputeShader jumpFloodShader;
        private readonly int jumpFloodKernel;
        private readonly int seedFromSegmentsKernel;
        private readonly int seedFromScalarFieldKernel;
        private readonly int finalizeKernel;
        private readonly int buildSegmentGridKernel;
        private readonly int seedFromSegmentsSPKernel;
        private readonly int finalizeSdfFromDenseKernel;

        private RenderTexture seedTexture;
        private RenderTexture jfaTempTexture;
        private int textureResolution;

        public JumpFlooder()
        {
            // Load the shader using the constant path
            jumpFloodShader = ComputeShaderConstants.JumpFloodCompute.Get();
            
            // Find kernels using the constant names
            jumpFloodKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.JumpFlood);
            seedFromSegmentsKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.SeedFromSegments);
            seedFromScalarFieldKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.SeedFromScalarField);
            finalizeKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.FinalizeSDF);
            buildSegmentGridKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.BuildSegmentGrid);
            seedFromSegmentsSPKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.SeedFromSegments_SP);
            finalizeSdfFromDenseKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.FinalizeSDF_FromDense);
        }
        

        public void InitJFATextures(int textureRes)
        {
            this.textureResolution = textureRes;

            // Helper to create or recreate render textures
            void CreateTexture(ref RenderTexture tex, RenderTextureFormat format, FilterMode filter)
            {
                if (tex != null && (tex.width != textureRes || tex.height != textureRes))
                {
                    tex.Release();
                    tex = null;
                }
                if (tex == null)
                {
                    tex = new RenderTexture(textureRes, textureRes, 0, format)
                    {
                        enableRandomWrite = true,
                        filterMode = filter
                    };
                    tex.Create();
                }
            }

            CreateTexture(ref seedTexture, RenderTextureFormat.ARGBFloat, FilterMode.Point);
            CreateTexture(ref jfaTempTexture, RenderTextureFormat.ARGBFloat, FilterMode.Point);
        }

        public void BuildSegmentGrid(ComputeBuffer segmentsBuffer, ComputeBuffer segmentCountBuffer, ComputeBuffer gridCountsBuffer, ComputeBuffer gridIndicesBuffer, int gridResolution, int maxSegmentsPerCell, int maxSegments)
        {
            var shader = jumpFloodShader;
            
            gridCountsBuffer.SetCounterValue(0);

            shader.SetBuffer(buildSegmentGridKernel, "_Segments", segmentsBuffer);
            shader.SetBuffer(buildSegmentGridKernel, "_SegmentCount", segmentCountBuffer);
            shader.SetInt("_GridResolution", gridResolution);
            shader.SetInt("_MaxSegmentsPerCell", maxSegmentsPerCell);
            shader.SetBuffer(buildSegmentGridKernel, "_GridCounts", gridCountsBuffer);
            shader.SetBuffer(buildSegmentGridKernel, "_GridIndices", gridIndicesBuffer);
            
            int threadGroups = Mathf.CeilToInt(maxSegments / 256.0f);
            shader.Dispatch(buildSegmentGridKernel, threadGroups, 1, 1);
        }

        // --- Other methods are unchanged, but would also use constants if they had unique parameters ---
        public void GenerateSeedsFromScalarField(RenderTexture scalarField, float isoValue)
        {
            var shader = jumpFloodShader;
            shader.SetTexture(seedFromScalarFieldKernel, "_ScalarField", scalarField);
            shader.SetFloat("_IsoValue", isoValue);
            shader.SetTexture(seedFromScalarFieldKernel, "_SeedTexture", this.seedTexture);
            shader.SetInt("_TextureResolution", textureResolution);
            int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
            shader.Dispatch(seedFromScalarFieldKernel, threadGroups, threadGroups, 1);
        }
        
         public void RunJumpFlood()
         {
             var shader = jumpFloodShader;

             RenderTexture ping = seedTexture;
             RenderTexture pong = jfaTempTexture;

             // Calculate jump distances: start from half texture size, divide by 2 each iteration
             int maxJump = Mathf.NextPowerOfTwo(textureResolution) / 2;

             for (int jump = maxJump; jump >= 1; jump /= 2)
             {
                 shader.SetTexture(jumpFloodKernel, "_InputTexture", ping);
                 shader.SetTexture(jumpFloodKernel, "_OutputTexture", pong);
                 shader.SetInt("_JumpDistance", jump);
                 shader.SetInt("_TextureResolution", textureResolution);

                 int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
                 shader.Dispatch(jumpFloodKernel, threadGroups, threadGroups, 1);

                 // Swap ping and pong for next iteration
                 (ping, pong) = (pong, ping);
             }

             // Final result is in 'ping' - copy to seedTexture if needed
             if (ping != seedTexture)
             {
                 Graphics.CopyTexture(ping, seedTexture);
             }
         }

         public void FinalizeSDF(RenderTexture outputTexture, bool outputUnsigned, RenderTexture scalarField,
             float isoValue)
         {
             var shader = jumpFloodShader;

             shader.SetTexture(finalizeKernel, "_JFAResult", seedTexture);
             shader.SetTexture(finalizeKernel, "_SDFTexture", outputTexture);
             shader.SetInt("_TextureResolution", textureResolution);
             shader.SetBool("_OutputUnsigned", outputUnsigned);

             // Set scalar field and iso value if provided (for sign determination)
             if (scalarField != null)
             {
                 shader.SetTexture(finalizeKernel, "_ScalarField", scalarField);
                 shader.SetFloat("_IsoValue", isoValue);
                 shader.SetBool("_HasScalarField", true);
             }
             else
             {
                 shader.SetBool("_HasScalarField", false);
             }

             int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
             shader.Dispatch(finalizeKernel, threadGroups, threadGroups, 1);
         }        
        public void Dispose()
        {
            seedTexture?.Release();
            jfaTempTexture?.Release();
            seedTexture = null;
            jfaTempTexture = null;
        }
        
        // ADD THIS NEW METHOD
        public void GenerateSeedsFromSegments_SP(ComputeBuffer segmentsBuffer, ComputeBuffer gridCountsBuffer, ComputeBuffer gridIndicesBuffer, int gridResolution, int maxSegmentsPerCell)
        {
            var shader = jumpFloodShader;
    
            // Set all the buffers needed for the grid-accelerated search
            shader.SetBuffer(seedFromSegmentsSPKernel, "_Segments", segmentsBuffer);
            shader.SetInt("_GridResolution", gridResolution);
            shader.SetInt("_MaxSegmentsPerCell", maxSegmentsPerCell);
            shader.SetBuffer(seedFromSegmentsSPKernel, "_GridCounts", gridCountsBuffer);
            shader.SetBuffer(seedFromSegmentsSPKernel, "_GridIndices", gridIndicesBuffer);
    
            // Set the output texture
            shader.SetTexture(seedFromSegmentsSPKernel, "_SeedTexture", this.seedTexture);
            shader.SetInt("_TextureResolution", textureResolution);
    
            // Dispatch one thread per pixel of the output texture
            int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
            shader.Dispatch(seedFromSegmentsSPKernel, threadGroups, threadGroups, 1);
        }

// ADD THIS NEW METHOD
        public void FinalizeSDF_FromDense(RenderTexture outputTexture, RenderTexture scalarField, float isoValue)
        {
            var shader = jumpFloodShader;

            // Input is the seed texture which now contains final distances
            shader.SetTexture(finalizeSdfFromDenseKernel, "_SeedTexture", this.seedTexture);
            shader.SetTexture(finalizeSdfFromDenseKernel, "_ScalarField", scalarField);
            shader.SetFloat("_IsoValue", isoValue);
            shader.SetBool("_HasScalarField", scalarField != null);

            // Final output texture
            shader.SetTexture(finalizeSdfFromDenseKernel, "_SDFTexture", outputTexture);
            shader.SetInt("_TextureResolution", textureResolution);

            int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
            shader.Dispatch(finalizeSdfFromDenseKernel, threadGroups, threadGroups, 1);
        }

        public void GenerateSeedsFromSegments(ComputeBuffer segmentsBuffer, ComputeBuffer segmentCountBuffer)
        {
            var shader = jumpFloodShader;

            shader.SetBuffer(seedFromSegmentsKernel, "_Segments", segmentsBuffer);
            shader.SetBuffer(seedFromSegmentsKernel, "_SegmentCount", segmentCountBuffer);
            shader.SetTexture(seedFromSegmentsKernel, "_SeedTexture", seedTexture);
            shader.SetInt("_TextureResolution", textureResolution);

            int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
            shader.Dispatch(seedFromSegmentsKernel, threadGroups, threadGroups, 1);
        }
    }
}