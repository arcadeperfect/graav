using System;
using UnityEditor.UI;
using UnityEngine;
using static PlanetGen.ComputeShaderConstants;

namespace PlanetGen
{
    public class JumpFlooder: IDisposable
    {
        ComputeShader JumpFloodShader;
        int _jumpFloodKernel;
        int _seedKernel;
        int _seedFromScalarFieldKernel;
        private int _finalizeKernel;
        

        // JFA intermediate textures
        private RenderTexture seedTexture;
        private RenderTexture jfaTempTexture;

        private int textureResolution;

        public JumpFlooder()
        {
            JumpFloodShader = Resources.Load<ComputeShader>(JumpFloodCompute.Path);
            _jumpFloodKernel = JumpFloodShader.FindKernel(JumpFloodCompute.Kernels.JumpFlood);
            _seedKernel = JumpFloodShader.FindKernel(JumpFloodCompute.Kernels.SeedFromSegments);
            _seedFromScalarFieldKernel = JumpFloodShader.FindKernel(JumpFloodCompute.Kernels.SeedFromScalarField);
            _finalizeKernel = JumpFloodShader.FindKernel(JumpFloodCompute.Kernels.FinalizeSDF);
        }

        public void InitJFATextures(int textureRes)
        {
            this.textureResolution = textureRes;

            if (seedTexture != null) seedTexture.Release();
            seedTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point // Important: use Point filtering for JFA
            };
            seedTexture.Create();

            if (jfaTempTexture != null) jfaTempTexture.Release();
            jfaTempTexture = new RenderTexture(textureRes, textureRes, 0, RenderTextureFormat.ARGBFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point
            };
            jfaTempTexture.Create();
        }
        
        

        public void RunJumpFlood()
        {
            var shader = JumpFloodShader;

            RenderTexture ping = seedTexture;
            RenderTexture pong = jfaTempTexture;

            // Calculate jump distances: start from half texture size, divide by 2 each iteration
            int maxJump = Mathf.NextPowerOfTwo(textureResolution) / 2;

            for (int jump = maxJump; jump >= 1; jump /= 2)
            {
                shader.SetTexture(_jumpFloodKernel, "_InputTexture", ping);
                shader.SetTexture(_jumpFloodKernel, "_OutputTexture", pong);
                shader.SetInt("_JumpDistance", jump);
                shader.SetInt("_TextureResolution", textureResolution);

                int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
                shader.Dispatch(_jumpFloodKernel, threadGroups, threadGroups, 1);

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
            var shader = JumpFloodShader;

            shader.SetTexture(_finalizeKernel, "_JFAResult", seedTexture);
            shader.SetTexture(_finalizeKernel, "_SDFTexture", outputTexture);
            shader.SetInt("_TextureResolution", textureResolution);
            shader.SetBool("_OutputUnsigned", outputUnsigned);

            // Set scalar field and iso value if provided (for sign determination)
            if (scalarField != null)
            {
                shader.SetTexture(_finalizeKernel, "_ScalarField", scalarField);
                shader.SetFloat("_IsoValue", isoValue);
                shader.SetBool("_HasScalarField", true);
            }
            else
            {
                shader.SetBool("_HasScalarField", false);
            }

            int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
            shader.Dispatch(_finalizeKernel, threadGroups, threadGroups, 1);
        }
        
        public void GenerateSeeds(ComputeBuffer segmentsBuffer, ComputeBuffer segmentCountBuffer)
        {
            var shader = JumpFloodShader;
        
            shader.SetBuffer(_seedKernel, "_Segments", segmentsBuffer);
            shader.SetBuffer(_seedKernel, "_SegmentCount", segmentCountBuffer);
            shader.SetTexture(_seedKernel, "_SeedTexture", seedTexture);
            shader.SetInt("_TextureResolution", textureResolution);
        
            int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
            shader.Dispatch(_seedKernel, threadGroups, threadGroups, 1);
        }
        
        public void GenerateSeedsFromScalarField(RenderTexture scalarField, RenderTexture seedTexture, float isoValue)
        {
            var shader = JumpFloodShader;
            this.seedTexture = seedTexture;
            shader.SetTexture(_seedFromScalarFieldKernel, "_ScalarField", scalarField);
            shader.SetFloat("_IsoValue", isoValue);
            shader.SetTexture(_seedFromScalarFieldKernel, "_SeedTexture", seedTexture);
            shader.SetInt("_TextureResolution", textureResolution);

            int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
            shader.Dispatch(_seedFromScalarFieldKernel, threadGroups, threadGroups, 1);
        }
        
        // ADDED: Dispose method to properly clean up resources
        public void Dispose()
        {
            if (seedTexture != null)
            {
                seedTexture.Release();
                seedTexture = null;
            }
            
            if (jfaTempTexture != null)
            {
                jfaTempTexture.Release();
                jfaTempTexture = null;
            }
        }
    }
}