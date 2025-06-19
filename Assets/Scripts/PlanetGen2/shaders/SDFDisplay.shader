Shader "Universal Render Pipeline/Unlit/SDFSanityCheck"
{
    Properties
    {
        _SDFTexture ("SDF Texture", 2D) = "white" {}
        _ColorTexture ("Color Texture", 2D) = "white" {}
        _LineWidth ("Line Width", Float) = 0.02
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
        _BandSpacing ("Band Spacing", Float) = 0.05
        _MaxDistance ("Max Distance", Float) = 0.2
        [Toggle] _ShowBands ("Show Bands", Float) = 0
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
            
            // Add the color texture
            TEXTURE2D(_ColorTexture);
            SAMPLER(sampler_ColorTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _SDFTexture_ST;
                float4 _ColorTexture_ST;
                float _LineWidth;
                float4 _LineColor;
                float _BandSpacing;
                float _MaxDistance;
                float _ShowBands;
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
                
                // Check if this is the fallback value (very large distance)
                if (distance > 1000.0)
                {
                    // Show bright red for fallback/error values
                    return half4(1, 0, 0, 1);
                }
                
                // Sample color from the color texture
                half4 segmentColor = SAMPLE_TEXTURE2D(_ColorTexture, sampler_ColorTexture, input.uv);
                
                if (_ShowBands > 0.5)
                {
                    // Band rendering mode - bands only inside the shape (distance < _LineWidth threshold)
                    if (distance <= _LineWidth)
                    {
                        // We're inside the shape, now calculate bands
                        // Calculate distance to nearest band center
                        float bandPosition = distance / _BandSpacing;
                        float nearestBandCenter = round(bandPosition) * _BandSpacing;
                        float distanceToBandCenter = abs(distance - nearestBandCenter);
                        
                        // Show band if within lineWidth/2 of the band center
                        if (distanceToBandCenter <= _LineWidth * 0.25) // Make bands thinner for better visibility
                        {
                            return half4(segmentColor.rgb, 1.0); // Band color
                        }
                        else
                        {
                            return half4(0, 0, 0, 1); // Black between bands inside shape
                        }
                    }
                    else
                    {
                        return half4(0, 0, 0, 1); // Black outside the shape
                    }
                }
                else
                {
                    // Normal line rendering mode
                    // Anti-aliased line rendering using smoothstep
                    float lineCenter = _LineWidth * 0.5;
                    float aaWidth = fwidth(distance);
                    
                    // Create smooth falloff from line center to edge
                    float alpha = 1.0 - smoothstep(lineCenter - aaWidth, lineCenter + aaWidth, distance);
                    
                    // If alpha is too low, discard pixel for performance
                    if (alpha < 0.01)
                    {
                        discard;
                    }
                    
                    // Blend segment color with calculated alpha
                    return half4(segmentColor.rgb, alpha);
                }
            }
            ENDHLSL
        }
    }
}