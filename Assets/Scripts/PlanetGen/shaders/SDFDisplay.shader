////
//Shader "PlanetGen/SDFDisplayDualPass"
//{
//    Properties
//    {
//        _SDFTexture ("Main SDF Texture", 2D) = "white" {}
//        _BandSDFTexture ("Band SDF Texture", 2D) = "white" {}
//        _ColorTexture ("Original Field Color Texture", 2D) = "white" {}
//        _LineWidth ("Main Line Width", Float) = 0.02
//        _BandLineWidth ("Band Line Width", Float) = 0.01
//        _BandColor ("Band Color", Color) = (0.5, 0.5, 0.5, 1)
//        _UseBandColor ("Use Band Color Override", Float) = 0
//    }
//
//    SubShader
//    {
//        Tags
//        {
//            "RenderType" = "Transparent"
//            "RenderPipeline" = "UniversalPipeline"
//            "Queue" = "Transparent"
//        }
//        LOD 100
//
//        Pass
//        {
//            Name "Unlit"
//
//            Blend SrcAlpha OneMinusSrcAlpha
//            ZWrite Off
//
//            HLSLPROGRAM
//            #pragma vertex vert
//            #pragma fragment frag
//
//            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//
//            struct Attributes
//            {
//                float4 positionOS : POSITION;
//                float2 uv : TEXCOORD0;
//            };
//
//            struct Varyings
//            {
//                float4 positionHCS : SV_POSITION;
//                float2 uv : TEXCOORD0;
//            };
//
//            TEXTURE2D(_SDFTexture);
//            TEXTURE2D(_BandSDFTexture);
//            TEXTURE2D(_ColorTexture);
//            SAMPLER(sampler_SDFTexture);
//            SAMPLER(sampler_BandSDFTexture);
//            SAMPLER(sampler_ColorTexture);
//
//            CBUFFER_START(UnityPerMaterial)
//                float4 _SDFTexture_ST;
//                float4 _BandSDFTexture_ST;
//                float4 _ColorTexture_ST;
//                float _LineWidth;
//                float _BandLineWidth;
//                // float4 _BandColor;
//                // float _UseBandColor;
//            CBUFFER_END
//
//            Varyings vert(Attributes input)
//            {
//                Varyings output;
//                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
//                output.uv = TRANSFORM_TEX(input.uv, _SDFTexture);
//                return output;
//            }
//
//            half4 frag(Varyings input) : SV_Target
//            {
//                // Sample both SDF textures
//                float4 mainSdfData = SAMPLE_TEXTURE2D(_SDFTexture, sampler_SDFTexture, input.uv);
//                float4 bandSdfData = SAMPLE_TEXTURE2D(_BandSDFTexture, sampler_BandSDFTexture, input.uv);
//
//                // Extract distance data from SDF textures
//                float mainSignedDistance = mainSdfData.r;
//                float bandSignedDistance = bandSdfData.r;
//
//                // Sample the original field color texture
//                float2 colorUV = TRANSFORM_TEX(input.uv, _ColorTexture);
//                half4 fieldColor = SAMPLE_TEXTURE2D(_ColorTexture, sampler_ColorTexture, colorUV);
//
//                // Check for invalid distances (debugging)
//                if (abs(mainSignedDistance) > 1000.0 && abs(bandSignedDistance) > 1000.0)
//                {
//                    return half4(1, 0, 0, 0.5); // Semi-transparent red for debugging
//                }
//
//                // --- MAIN LINE RENDERING ---
//                float mainDistanceFromZero = abs(mainSignedDistance);
//                float mainScreenPixelWidth = fwidth(mainDistanceFromZero);
//                float mainLineHalfWidth = _LineWidth * 0.5;
//
//                // Anti-aliased line rendering using smoothstep
//                float mainAlpha = 1.0 - smoothstep(
//                    mainLineHalfWidth - mainScreenPixelWidth,
//                    mainLineHalfWidth + mainScreenPixelWidth,
//                    mainDistanceFromZero
//                );
//
//                // --- BAND LINE RENDERING ---
//                // float bandDistanceFromZero = abs(bandSignedDistance);
//                float bandDistanceFromZero = abs(bandSignedDistance);
//                float bandScreenPixelWidth = fwidth(bandDistanceFromZero);
//                float bandLineHalfWidth = _BandLineWidth * 0.5;
//
//                // Anti-aliased band line rendering
//                float bandAlpha = 1.0 - smoothstep(
//                    bandLineHalfWidth - bandScreenPixelWidth,
//                    bandLineHalfWidth + bandScreenPixelWidth,
//                    bandDistanceFromZero
//                );
//
//                // --- COMBINE MAIN LINE AND BANDS ---
//                half4 finalColor = half4(0, 0, 0, 0);
//
//                // Main line takes priority and uses field colors
//                if (mainAlpha > 0.01)
//                {
//                    finalColor = half4(fieldColor.rgb, mainAlpha * fieldColor.a);
//                }
//                // Band lines appear where main line is not present
//                else if (bandAlpha > 0.01)
//                {
//                    // Use field color but with reduced intensity for distinction
//                    half4 bandFieldColor = fieldColor * 0.7; // Darken the field color for bands
//                    finalColor = half4(bandFieldColor.rgb, bandAlpha * bandFieldColor.a);
//                }
//
//                // Discard transparent pixels to improve performance
//                if (finalColor.a < 0.01)
//                {
//                    discard;
//                }
//
//                return finalColor;
//            }
//            ENDHLSL
//        }
//    }
//}

//
//
//


//
Shader "PlanetGen/SDFDisplayDualPass"
{
    Properties
    {
        _SDFTexture ("Main SDF Texture", 2D) = "white" {}
        _BandSDFTexture ("Band SDF Texture", 2D) = "white" {}
        _ColorTexture ("Original Field Color Texture", 2D) = "white" {}
        _LineWidth ("Main Line Width", Float) = 0.02
        _BandLineWidth ("Band Line Width", Float) = 0.01
        _BandColor ("Band Color", Color) = (0.5, 0.5, 0.5, 1)
        _UseBandColor ("Use Band Color Override", Float) = 0
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
            TEXTURE2D(_BandSDFTexture);
            TEXTURE2D(_ColorTexture);
            SAMPLER(sampler_SDFTexture);
            SAMPLER(sampler_BandSDFTexture);
            SAMPLER(sampler_ColorTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _SDFTexture_ST;
                float4 _BandSDFTexture_ST;
                float4 _ColorTexture_ST;
                float _LineWidth;
                float _BandLineWidth;
                // float4 _BandColor;
                // float _UseBandColor;
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
                // Sample both SDF textures
                float4 mainSdfData = SAMPLE_TEXTURE2D(_SDFTexture, sampler_SDFTexture, input.uv);
                float4 bandSdfData = SAMPLE_TEXTURE2D(_BandSDFTexture, sampler_BandSDFTexture, input.uv);

                // Extract distance data from SDF textures
                // Main surface: R = unsigned distance, ignore sign
                float mainUnsignedDistance = mainSdfData.r;
                
                // Bands: R = unsigned distance, G = sign
                float bandUnsignedDistance = bandSdfData.r;
                float bandSign = bandSdfData.g;
                float bandSignedDistance = bandUnsignedDistance * bandSign;

                // Sample the original field color texture
                float2 colorUV = TRANSFORM_TEX(input.uv, _ColorTexture);
                half4 fieldColor = SAMPLE_TEXTURE2D(_ColorTexture, sampler_ColorTexture, colorUV);

                // Check for invalid distances (debugging)
                if (mainUnsignedDistance > 1000.0 && bandUnsignedDistance > 1000.0)
                {
                    return half4(1, 0, 0, 0.5); // Semi-transparent red for debugging
                }

                // --- MAIN LINE RENDERING ---
                // Main surface ignores sign - uses unsigned distance directly
                float mainScreenPixelWidth = fwidth(mainUnsignedDistance);
                float mainLineHalfWidth = _LineWidth * 0.5;

                // Anti-aliased line rendering using smoothstep
                float mainAlpha = 1.0 - smoothstep(
                    mainLineHalfWidth - mainScreenPixelWidth,
                    mainLineHalfWidth + mainScreenPixelWidth,
                    mainUnsignedDistance
                );

                // --- BAND LINE RENDERING ---
                // Bands respect sign - use signed distance, but only show outside (positive sign)
                float bandDistanceFromZero = abs(bandSignedDistance);
                float bandScreenPixelWidth = fwidth(bandDistanceFromZero);
                float bandLineHalfWidth = _LineWidth * 0.5; // Use same width as main line

                // Anti-aliased band line rendering - only when outside (positive sign)
                float bandAlpha = 0.0;
                if (bandSign > 0.0) // Only render bands outside the planet
                {
                    bandAlpha = 1.0 - smoothstep(
                        bandLineHalfWidth - bandScreenPixelWidth,
                        bandLineHalfWidth + bandScreenPixelWidth,
                        bandDistanceFromZero
                    );
                }

                // --- COMBINE MAIN LINE AND BANDS ---
                half4 finalColor = half4(0, 0, 0, 0);

                // Main line takes priority and uses field colors
                if (mainAlpha > 0.01)
                {
                    finalColor = half4(fieldColor.rgb, mainAlpha * fieldColor.a);
                }
                // Band lines appear where main line is not present
                else if (bandAlpha > 0.01)
                {
                    // Use field color but with reduced intensity for distinction
                    half4 bandFieldColor = fieldColor * 0.7; // Darken the field color for bands
                    finalColor = half4(bandFieldColor.rgb, bandAlpha * bandFieldColor.a);
                }

                // Discard transparent pixels to improve performance
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