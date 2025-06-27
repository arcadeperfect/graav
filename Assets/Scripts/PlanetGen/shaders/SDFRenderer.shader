Shader "PlanetGen/ProceduralPlanetRenderer"
{
    Properties
    {
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
            
                // float t = SAMPLE_TEXTURE2D(_UDFTexture, sampler_linear_clamp, input.uv).r;
                // // float pixelWidth = 0.0001;
                // // float u = smoothstep(_LineWidth - pixelWidth, _LineWidth + pixelWidth, t);
                // float w = fwidth(t);
                // float u = 0;
                // if (w < 1)
                // {
                //      u = 1.0 - smoothstep(_LineWidth - w, _LineWidth + w, t);
                // }
                // // float u = 0;
                // // if (t < _LineWidth)
                // // {
                // //     u = 1;
                // // }
                //
                // return half4(u,u,u,1);
            // }
                
                // 1. Sample the base color from the original noise field
                half4 fieldColor = SAMPLE_TEXTURE2D(_ColorTexture, sampler_linear_clamp, input.uv);
            
                // --- MAIN OUTLINE RENDERING (Using the new precise UDF) ---
                // Sample the Unsigned Distance Field. The .r channel contains the distance
                // to the nearest segment in normalized UV space [0, 1].
                float udfDistance = SAMPLE_TEXTURE2D(_UDFTexture, sampler_linear_clamp, input.uv).r;
            
                // Use screen-space derivatives (fwidth) for consistent anti-aliasing at any scale
                float pixelWidth = fwidth(udfDistance);
                
                // _LineWidth is a normalized value.
                float lineAlpha = 0;
                if (pixelWidth < 1)
                {
                    lineAlpha = 1.0 - smoothstep(_LineWidth - pixelWidth, _LineWidth + pixelWidth, udfDistance);
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
            
                        // _BandLineWidth is in pixels
                        float band = 1.0 - smoothstep(_BandLineWidth, _BandLineWidth + 1.5, correctedDist);
                        bandMask = max(bandMask, band);
                    }
                }
            
                // --- COMPOSITING ---
                half4 finalColor = half4(fieldColor.rgb, 0);
            
                // Add bands first (darkened)
                finalColor.rgb = lerp(finalColor.rgb, fieldColor.rgb * 0.7, bandMask);
                finalColor.a = max(finalColor.a, bandMask);
            
                // Add main outline on top
                finalColor.rgb = lerp(finalColor.rgb, fieldColor.rgb, lineAlpha);
                finalColor.a = max(finalColor.a, lineAlpha);
            
                if (finalColor.a < 0.01)
                {
                    discard;
                }
            
                return finalColor;
            }

            // half4 frag(Varyings input) : SV_Target
            // {
            //
            //     half4 fieldColor = SAMPLE_TEXTURE2D(_ColorTexture, sampler_linear_clamp, input.uv);
            //
            //     half4 mainSdfData = SAMPLE_TEXTURE2D(_UDFTexture, sampler_linear_clamp, input.uv);
            //   
            //     float mainGrad = max(fwidth(mainSdfData.r), 1e-6);
            //     float adjustedLineWidth = _LineWidth * 0.01;
            //     float outlineAlpha = 1.0 - smoothstep(adjustedLineWidth, adjustedLineWidth + mainGrad,
            //                                           abs(mainSdfData.r));
            //
            //     // --- PROCEDURAL BAND RENDERING (With gradient compensation) ---
            //     half4 warpedSdfData = SAMPLE_TEXTURE2D(_WarpedSDFTexture, sampler_linear_clamp, input.uv);
            //     float warpedSignedDist = warpedSdfData.r * warpedSdfData.g;
            //
            //     float bandMask = 0;
            //     if (warpedSignedDist < 0)
            //     {
            //         float grad_mag = fwidth(warpedSignedDist);
            //         if (grad_mag == 0.0) { grad_mag = 1e-6; }
            //
            //         for (int j = 0; j < _NumberOfBands; j++)
            //         {
            //             float bandIso = _BandStartOffset + (j * _BandInterval);
            //             float distToBand = abs(warpedSignedDist - bandIso);
            //             float correctedDist = distToBand / grad_mag;
            //
            //             // _BandLineWidth is now in pixels!
            //             float band = 1.0 - smoothstep(_BandLineWidth, _BandLineWidth + 1.5, correctedDist);
            //             bandMask = max(bandMask, band);
            //         }
            //     }
            //
            //     // --- COMPOSITING ---
            //     half4 finalColor = half4(fieldColor.rgb, 0);
            //
            //     // Add bands first (darkened)
            //     finalColor.rgb = lerp(finalColor.rgb, fieldColor.rgb * 0.7, bandMask);
            //     finalColor.a = max(finalColor.a, bandMask);
            //
            //     // Add main outline on top
            //     finalColor.rgb = lerp(finalColor.rgb, fieldColor.rgb, outlineAlpha);
            //     finalColor.a = max(finalColor.a, outlineAlpha);
            //
            //     if (finalColor.a < 0.01)
            //     {
            //         discard;
            //     }
            //
            //     return finalColor;
            // }
            ENDHLSL
        }
    }
}
//
//Shader "PlanetGen/ProceduralPlanetRenderer"
//{
//    Properties
//    {
//        // Textures from the new compute pipeline
//        _ColorTexture ("Color Texture", 2D) = "white" {}
//        _SDFTexture ("Band SDF Texture (JFA)", 2D) = "white" {}
//        _WarpedSDFTexture("Warped SDF Texture", 2D) = "white" {}
//        _PreciseDistanceTexture("Precise Distance Field", 2D) = "white" {} // NEW
//
//        // Parameters now controlled by PlanetGenMain.cs
//        _LineWidth ("Main Line Width", Float) = 0.01
//        _BandLineWidth ("Band Line Width", Float) = 2.0 // Now in screen-space pixels
//        _NumberOfBands ("Number of Bands", Int) = 5
//        _BandStartOffset ("Band Start Offset", Float) = -0.05
//        _BandInterval ("Band Interval", Float) = 0.02
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
//            Blend SrcAlpha OneMinusSrcAlpha
//            ZWrite Off
//            Cull Off
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
//            // Texture samplers
//            TEXTURE2D(_ColorTexture);
//            TEXTURE2D(_SDFTexture);
//            TEXTURE2D(_WarpedSDFTexture);
//            TEXTURE2D(_PreciseDistanceTexture); // NEW
//            SAMPLER(sampler_linear_clamp); // A generic sampler
//
//            // Uniforms passed from C#
//            CBUFFER_START(UnityPerMaterial)
//                float4 _ColorTexture_ST;
//                float4 _SDFTexture_ST;
//                float4 _WarpedSDFTexture_ST;
//                float4 _PreciseDistanceTexture_ST; // NEW
//                float _LineWidth;
//                float _BandLineWidth;
//                int _NumberOfBands;
//                float _BandStartOffset;
//                float _BandInterval;
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
//                // 1. Sample the base color from the original noise field
//                half4 fieldColor = SAMPLE_TEXTURE2D(_ColorTexture, sampler_linear_clamp, input.uv);
//
//                // --- MAIN OUTLINE RENDERING (Using precise distance field) ---
//                // NEW: Use the precise distance field for the surface contour
//                float preciseDistance = SAMPLE_TEXTURE2D(_PreciseDistanceTexture, sampler_linear_clamp, input.uv).r;
//                
//                // Anti-aliasing based on screen-space derivatives
//                float pixelDistance = fwidth(preciseDistance);
//                float adjustedLineWidth = _LineWidth * 0.01;
//                
//                // Create sharp, anti-aliased outline
//                float outlineAlpha = 1.0 - smoothstep(
//                    adjustedLineWidth - pixelDistance * 0.5, 
//                    adjustedLineWidth + pixelDistance * 0.5, 
//                    preciseDistance
//                );
//
//                // --- PROCEDURAL BAND RENDERING (Using warped JFA SDF) ---
//                half4 warpedSdfData = SAMPLE_TEXTURE2D(_WarpedSDFTexture, sampler_linear_clamp, input.uv);
//                float warpedSignedDist = warpedSdfData.r * warpedSdfData.g;
//
//                float bandMask = 0;
//                if (warpedSignedDist < 0)
//                {
//                    float grad_mag = fwidth(warpedSignedDist);
//                    if (grad_mag == 0.0) { grad_mag = 1e-6; }
//
//                    for (int j = 0; j < _NumberOfBands; j++)
//                    {
//                        float bandIso = _BandStartOffset + (j * _BandInterval);
//                        float distToBand = abs(warpedSignedDist - bandIso);
//                        float correctedDist = distToBand / grad_mag;
//
//                        // _BandLineWidth is now in pixels!
//                        float band = 1.0 - smoothstep(_BandLineWidth, _BandLineWidth + 1.5, correctedDist);
//                        bandMask = max(bandMask, band);
//                    }
//                }
//
//                // --- COMPOSITING ---
//                half4 finalColor = half4(fieldColor.rgb, 0);
//
//                // Add bands first (darkened)
//                finalColor.rgb = lerp(finalColor.rgb, fieldColor.rgb * 0.7, bandMask);
//                finalColor.a = max(finalColor.a, bandMask);
//
//                // Add main outline on top (precise and sharp)
//                finalColor.rgb = lerp(finalColor.rgb, fieldColor.rgb, outlineAlpha);
//                finalColor.a = max(finalColor.a, outlineAlpha);
//
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