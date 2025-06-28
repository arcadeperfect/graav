Shader "PlanetGen/ProceduralPlanetRenderer"
{
    Properties
    {
        _SurfaceBrightness ("Surface Brightness", Float) = 1
        _BandBrightness ("Band Brightness", Float) = 1

        // Textures from the new compute pipeline
        _ColorTexture ("Color Texture", 2D) = "white" {}
        _SDFTexture ("Main SDF Texture (Un-Warped)", 2D) = "white" {}
        _WarpedSDFTexture("Warped SDF Texture", 2D) = "white" {}
        _UDFTexture("UDF Texture", 2D) = "white" {}

        // Parameters now controlled by PlanetGenMain.cs
        _LineWidth ("Main Line Width", Float) = 0.01
        _BandLineWidth ("Band Line Width", Float) = 2.0 // Now in screen-space pixels
        _NumberOfBands ("Number of Bands", Int) = 5
        _BandStartOffset ("Band Start Offset", Float) = -0.05
        _BandInterval ("Band Interval", Float) = 0.02
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
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

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

            // Texture samplers
            TEXTURE2D(_ColorTexture);
            TEXTURE2D(_SDFTexture);
            TEXTURE2D(_WarpedSDFTexture);
            TEXTURE2D(_UDFTexture);
            SAMPLER(sampler_linear_clamp); // A generic sampler

            // Uniforms passed from C#
            CBUFFER_START(UnityPerMaterial)
                float4 _ColorTexture_ST;
                float4 _SDFTexture_ST;
                float4 _WarpedSDFTexture_ST;
                float _LineWidth;
                float _BandLineWidth;
                int _NumberOfBands;
                float _BandStartOffset;
                float _BandInterval;
                float _SurfaceBrightness;
                float _BandBrightness;
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
                // 1. Sample the base color from the original noise field
                half4 fieldColor = SAMPLE_TEXTURE2D(_ColorTexture, sampler_linear_clamp, input.uv);

                // --- MAIN OUTLINE RENDERING (Using the new precise UDF) ---
                float udfDistance = SAMPLE_TEXTURE2D(_UDFTexture, sampler_linear_clamp, input.uv).r;
                float pixelWidth = fwidth(udfDistance);

                // Fix: Apply brightness correctly to the line alpha
                float lineAlpha = 0;
                if (pixelWidth < 1)
                {
                    // Calculate base alpha first, then apply brightness
                    float baseLineAlpha = 1.0 - smoothstep(_LineWidth - pixelWidth, _LineWidth + pixelWidth,
                          udfDistance);
                    lineAlpha = baseLineAlpha * _SurfaceBrightness;
                }

                // --- PROCEDURAL BAND RENDERING (Using warped JFA SDF) ---
                half4 warpedSdfData = SAMPLE_TEXTURE2D(_WarpedSDFTexture, sampler_linear_clamp, input.uv);
                float warpedSignedDist = warpedSdfData.r * warpedSdfData.g;

                float bandMask = 0;
                if (warpedSignedDist < 0)
                {
                    float grad_mag = fwidth(warpedSignedDist);
                    if (grad_mag == 0.0) { grad_mag = 1e-6; }

                    for (int j = 0; j < _NumberOfBands; j++)
                    {
                        float bandIso = _BandStartOffset + (j * _BandInterval);
                        float distToBand = abs(warpedSignedDist - bandIso);
                        float correctedDist = distToBand / grad_mag;

                        float band = 1.0 - smoothstep(_BandLineWidth, _BandLineWidth + 1.5, correctedDist);
                        bandMask = max(bandMask, band);
                    }
                    // Fix: Apply band brightness here
                    bandMask *= _BandBrightness;
                }

                // --- COMPOSITING ---
                half4 finalColor = half4(fieldColor.rgb, 0);

                // Add bands first (with brightness control)
                half3 bandColor = fieldColor.rgb * 0.7 * _BandBrightness;
                finalColor.rgb = lerp(finalColor.rgb, bandColor, bandMask);
                finalColor.a = max(finalColor.a, bandMask);

                // Add main outline on top (with brightness control)
                half3 surfaceColor = fieldColor.rgb * _SurfaceBrightness;
                finalColor.rgb = lerp(finalColor.rgb, surfaceColor, lineAlpha);
                finalColor.a = max(finalColor.a, lineAlpha);

                if (finalColor.a < 0.01)
                {
                    discard;
                }

                return finalColor;
            }
            ENDHLSL
        }
    }
}