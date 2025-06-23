using System;
using UnityEditor.UI;
using UnityEngine;
using static PlanetGen.ComputeShaderConstants;

namespace PlanetGen
{
    public class JumpFlooder: IDisposable
    {
        private readonly ComputeShader jumpFloodShader;
        private readonly int jumpFloodKernel;
        private readonly int seedKernel;
        private readonly int seedFromScalarFieldKernel;
        private readonly int finalizeKernel;
        

        // JFA intermediate textures
        private RenderTexture seedTexture;
        private RenderTexture jfaTempTexture;

        private int textureResolution;

        public JumpFlooder()
        {
            jumpFloodShader = Resources.Load<ComputeShader>(JumpFloodCompute.Path);
            jumpFloodKernel = jumpFloodShader.FindKernel(JumpFloodCompute.Kernels.JumpFlood);
            seedKernel = jumpFloodShader.FindKernel(JumpFloodCompute.Kernels.SeedFromSegments);
            seedFromScalarFieldKernel = jumpFloodShader.FindKernel(JumpFloodCompute.Kernels.SeedFromScalarField);
            finalizeKernel = jumpFloodShader.FindKernel(JumpFloodCompute.Kernels.FinalizeSDF);
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
        
        public void GenerateSeedsFromSegments(ComputeBuffer segmentsBuffer, ComputeBuffer segmentCountBuffer)
        {
            var shader = jumpFloodShader;
        
            shader.SetBuffer(seedKernel, "_Segments", segmentsBuffer);
            shader.SetBuffer(seedKernel, "_SegmentCount", segmentCountBuffer);
            shader.SetTexture(seedKernel, "_SeedTexture", seedTexture);
            shader.SetInt("_TextureResolution", textureResolution);
        
            int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
            shader.Dispatch(seedKernel, threadGroups, threadGroups, 1);
        }
        
        public void GenerateSeedsFromScalarField(RenderTexture scalarField, RenderTexture seedTexture, float isoValue)
        {
            var shader = jumpFloodShader;
            this.seedTexture = seedTexture;
            shader.SetTexture(seedFromScalarFieldKernel, "_ScalarField", scalarField);
            shader.SetFloat("_IsoValue", isoValue);
            shader.SetTexture(seedFromScalarFieldKernel, "_SeedTexture", seedTexture);
            shader.SetInt("_TextureResolution", textureResolution);

            int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
            shader.Dispatch(seedFromScalarFieldKernel, threadGroups, threadGroups, 1);
        }
        
        public void GenerateSeedsFromScalarField(RenderTexture scalarField, float isoValue)
        {
            var shader = jumpFloodShader;
    
            shader.SetTexture(seedFromScalarFieldKernel, "_ScalarField", scalarField);
            shader.SetFloat("_IsoValue", isoValue);
    
            // Use the seedTexture that belongs to this class instance
            shader.SetTexture(seedFromScalarFieldKernel, "_SeedTexture", this.seedTexture); 
    
            shader.SetInt("_TextureResolution", textureResolution);

            int threadGroups = Mathf.CeilToInt(textureResolution / 8.0f);
            shader.Dispatch(seedFromScalarFieldKernel, threadGroups, threadGroups, 1);
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