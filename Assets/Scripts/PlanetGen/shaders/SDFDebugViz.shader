﻿Shader "PlanetGen/SDFViz"
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
            
            // Add the structured buffer here, outside CBUFFER
            StructuredBuffer<float4> _SegmentColors;

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
                // Sample the SDF texture (RGBA format)
                float4 sdfData = SAMPLE_TEXTURE2D(_SDFTexture, sampler_SDFTexture, input.uv);
                
                // Extract distance from red channel
                float distance = sdfData.r;
                int segmentIndex = (int)sdfData.g;
                float interpFactor = sdfData.b;
                
                // Check if this is the fallback value (very large distance)
                if (distance > 1000.0)
                {
                    // Show bright red for fallback/error values
                    return half4(1, 0, 0, 1);
                }
                
                // Anti-aliased line rendering using smoothstep
                float lineCenter = _LineWidth * 0.5;
                float aaWidth = fwidth(distance); // Automatic AA width based on screen-space derivatives
                
                // Create smooth falloff from line center to edge
                float alpha = 1.0 - smoothstep(lineCenter - aaWidth, lineCenter + aaWidth, distance);
                
                // If alpha is too low, discard pixel for performance
                if (alpha < 0.01)
                {
                    discard;
                }
                
                // Sample segment color from buffer
                half4 segmentColor = (half4)_SegmentColors[segmentIndex];
                
                // Blend segment color with calculated alpha
                return half4(segmentColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}