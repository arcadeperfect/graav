using System;
using UnityEngine;

namespace PlanetGen.Compute
{
    public class JumpFlooder : IDisposable
    {
        private readonly ComputeShader jumpFloodShader;
        private readonly int jumpFloodKernel;
        private readonly int UdfFromSegmentsBruteForceKernel;
        private readonly int seedFromSegmentsHQKernel; // New high-quality kernel
        private readonly int seedFromScalarFieldKernel;
        private readonly int finalizeKernel;

        private RenderTexture seedTexture;
        private RenderTexture jfaTempTexture;
        private int textureResolution;

        public JumpFlooder()
        {

            jumpFloodShader = CSP.JumpFloodSdf.Get();
            jumpFloodKernel = CSP.JumpFloodSdf.Kernels.JumpFlood;
            UdfFromSegmentsBruteForceKernel = CSP.JumpFloodSdf.Kernels.UdfFromSegmentsBruteForce;
            seedFromScalarFieldKernel  = CSP.JumpFloodSdf.Kernels.SeedFromScalarField;
            finalizeKernel = CSP.JumpFloodSdf.Kernels.FinalizeSDF;

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

        // // Keep the original method for compatibility
        // public void GenerateSeedsFromSegments(ComputeBuffer segmentsBuffer, ComputeBuffer segmentCountBuffer)
        // {
        //     var shader = jumpFloodShader;
        //
        //     shader.SetBuffer(UdfFromSegmentsBruteForceKernel, "_Segments", segmentsBuffer);
        //     shader.SetBuffer(UdfFromSegmentsBruteForceKernel, "_SegmentCount", segmentCountBuffer);
        //     shader.SetTexture(UdfFromSegmentsBruteForceKernel, "_SeedTexture", seedTexture);
        //     shader.SetInt("_TextureResolution", textureResolution);
        //
        //     int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
        //     shader.Dispatch(UdfFromSegmentsBruteForceKernel, threadGroups, threadGroups, 1);
        // }
    }
}