

Shader "PlanetGen/SDFDisplayContour"
{
    Properties
    {
        _SDFTexture ("SDF Texture", 2D) = "white" {}
        _LineWidth ("Line Width", Float) = 0.02
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }
        LOD 100

        Pass
        {
            Name "Unlit"
            
            // Enable alpha blending for anti-aliasing
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_SDFTexture);
            SAMPLER(sampler_SDFTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _SDFTexture_ST;
                float _LineWidth;
                float4 _LineColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _SDFTexture);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float signedDistance = SAMPLE_TEXTURE2D(_SDFTexture, sampler_SDFTexture, input.uv).r;
                
                if (abs(signedDistance) > 1000.0)
                {
                    return half4(1, 0, 0, 1);
                }
                
                // --- ROBUST CONTOUR LINE RENDERING ---
                
                float distance = abs(signedDistance);
                
                // Get the width of a single screen pixel. 
                // This is a stable alternative to fwidth(distance) for anti-aliasing.
                // It ensures the fade is always one pixel wide, regardless of SDF complexity.
                float screenPixelWidth = fwidth(1.0);
                
                // Calculate the edge of the line.
                float lineEdge = _LineWidth * 0.5;

                // Create a smooth falloff over the width of one pixel, centered on the line's edge.
                // This is much more stable than the previous method for high-frequency SDFs.
                float alpha = 1.0 - smoothstep(lineEdge - screenPixelWidth, lineEdge + screenPixelWidth, distance);
                
                if (alpha < 0.01)
                {
                    discard;
                }
                
                return half4(_LineColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}