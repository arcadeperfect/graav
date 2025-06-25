// using System;
// using UnityEngine;
//
// namespace PlanetGen
// {
//     public class JumpFlooder : IDisposable
//     {
//         private readonly ComputeShader jumpFloodShader;
//         private readonly int jumpFloodKernel;
//         private readonly int seedFromSegmentsKernel;
//         private readonly int seedFromScalarFieldKernel;
//         private readonly int finalizeKernel;
//         private readonly int buildSegmentGridKernel;
//         private readonly int seedFromSegmentsSPKernel;
//         private readonly int finalizeSdfFromDenseKernel;
//
//         private RenderTexture seedTexture;
//         private RenderTexture jfaTempTexture;
//         private int textureResolution;
//
//         public JumpFlooder()
//         {
//             jumpFloodShader = ComputeShaderConstants.JumpFloodCompute.GetShader();
//             jumpFloodKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.JumpFlood);
//             seedFromSegmentsKernel =
//                 jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.BruteForceUDF);
//             seedFromScalarFieldKernel =
//                 jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.SeedFromScalarField);
//             finalizeKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.FinalizeSDF);
//         }
//
//
//         public void InitJFATextures(int textureRes)
//         {
//             this.textureResolution = textureRes;
//
//             void CreateTexture(ref RenderTexture tex, RenderTextureFormat format, FilterMode filter)
//             {
//                 if (tex != null && (tex.width != textureRes || tex.height != textureRes))
//                 {
//                     tex.Release();
//                     tex = null;
//                 }
//
//                 if (tex == null)
//                 {
//                     tex = new RenderTexture(textureRes, textureRes, 0, format)
//                     {
//                         enableRandomWrite = true,
//                         filterMode = filter
//                     };
//                     tex.Create();
//                 }
//             }
//
//             CreateTexture(ref seedTexture, RenderTextureFormat.ARGBFloat, FilterMode.Point);
//             CreateTexture(ref jfaTempTexture, RenderTextureFormat.ARGBFloat, FilterMode.Point);
//         }
//         
//         public void GenerateSeedsFromScalarField(RenderTexture scalarField, float isoValue)
//         {
//             var shader = jumpFloodShader;
//             shader.SetTexture(seedFromScalarFieldKernel, "_ScalarField", scalarField);
//             shader.SetFloat("_IsoValue", isoValue);
//             shader.SetTexture(seedFromScalarFieldKernel, "_SeedTexture", this.seedTexture);
//             shader.SetInt("_TextureResolution", textureResolution);
//             int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
//             shader.Dispatch(seedFromScalarFieldKernel, threadGroups, threadGroups, 1);
//         }
//
//         public void RunJumpFlood()
//         {
//             var shader = jumpFloodShader;
//
//             RenderTexture ping = seedTexture;
//             RenderTexture pong = jfaTempTexture;
//
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
//             if (ping != seedTexture)
//             {
//                 Graphics.CopyTexture(ping, seedTexture);
//             }
//         }
//
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
//         public void Dispose()
//         {
//             seedTexture?.Release();
//             jfaTempTexture?.Release();
//             seedTexture = null;
//             jfaTempTexture = null;
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
        private readonly int seedFromSegmentsHQKernel; // New high-quality kernel
        private readonly int seedFromScalarFieldKernel;
        private readonly int finalizeKernel;

        private RenderTexture seedTexture;
        private RenderTexture jfaTempTexture;
        private int textureResolution;

        public JumpFlooder()
        {
            jumpFloodShader = ComputeShaderConstants.JumpFloodCompute.GetShader();
            jumpFloodKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.JumpFlood);
            seedFromSegmentsKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.BruteForceUDF);
            seedFromSegmentsHQKernel = jumpFloodShader.FindKernel("SeedFromSegmentsHQ"); // New kernel
            seedFromScalarFieldKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.SeedFromScalarField);
            finalizeKernel = jumpFloodShader.FindKernel(ComputeShaderConstants.JumpFloodCompute.Kernels.FinalizeSDF);
        }

        public void InitJFATextures(int textureRes)
        {
            this.textureResolution = textureRes;

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

        /// <summary>
        /// High-quality seeding from segments that only processes pixels near the contour
        /// </summary>
        public void GenerateSeedsFromSegmentsHQ(ComputeBuffer segmentsBuffer, ComputeBuffer segmentCountBuffer, float maxSeedDistance = 0.1f)
        {
            var shader = jumpFloodShader;

            shader.SetBuffer(seedFromSegmentsHQKernel, "_Segments", segmentsBuffer);
            shader.SetBuffer(seedFromSegmentsHQKernel, "_SegmentCount", segmentCountBuffer);
            shader.SetTexture(seedFromSegmentsHQKernel, "_SeedTexture", seedTexture);
            shader.SetInt("_TextureResolution", textureResolution);
            shader.SetFloat("_MaxSeedDistance", maxSeedDistance);

            int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
            shader.Dispatch(seedFromSegmentsHQKernel, threadGroups, threadGroups, 1);
        }

        public void RunJumpFlood()
        {
            var shader = jumpFloodShader;

            RenderTexture ping = seedTexture;
            RenderTexture pong = jfaTempTexture;

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

            if (ping != seedTexture)
            {
                Graphics.CopyTexture(ping, seedTexture);
            }
        }

        public void FinalizeSDF(RenderTexture outputTexture, bool outputUnsigned, RenderTexture scalarField, float isoValue)
        {
            var shader = jumpFloodShader;

            shader.SetTexture(finalizeKernel, "_JFAResult", seedTexture);
            shader.SetTexture(finalizeKernel, "_SDFTexture", outputTexture);
            shader.SetInt("_TextureResolution", textureResolution);
            shader.SetBool("_OutputUnsigned", outputUnsigned);

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

        // Keep the original method for compatibility
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