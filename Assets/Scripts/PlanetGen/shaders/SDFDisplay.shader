Shader "PlanetGen/ProceduralPlanetRenderer"
{
    Properties
    {
        // Textures from the new compute pipeline
        _ColorTexture ("Color Texture", 2D) = "white" {}
        _SDFTexture ("Main SDF Texture (Un-Warped)", 2D) = "white" {}
        _WarpedSDFTexture("Warped SDF Texture", 2D) = "white" {}

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

            // Helper for clean anti-aliased lines using a screen-space width
            float get_line_alpha(float signedDist, float pixelWidth)
            {
                // The screen-space width of the SDF's gradient
                float screenPixelWidth = fwidth(signedDist);
                // Smoothstep over the boundary
                return 1.0 - smoothstep(pixelWidth - screenPixelWidth, pixelWidth, abs(signedDist));
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Sample the base color from the original noise field
                half4 fieldColor = SAMPLE_TEXTURE2D(_ColorTexture, sampler_linear_clamp, input.uv);
                
                // --- MAIN OUTLINE RENDERING ---
                // We use the ORIGINAL, un-warped SDF so the main outline never changes.
                // The JFA gives us R=distance, G=sign.
                half4 mainSdfData = SAMPLE_TEXTURE2D(_SDFTexture, sampler_linear_clamp, input.uv);
                float mainSignedDist = mainSdfData.r * mainSdfData.g;
            
                // Render the main outline with a specified width
                float outlineAlpha = get_line_alpha(mainSignedDist, _LineWidth * 0.01);
                
                // --- PROCEDURAL BAND RENDERING ---
                // Now we use the WARPED SDF to calculate the bands.
                half4 warpedSdfData = SAMPLE_TEXTURE2D(_WarpedSDFTexture, sampler_linear_clamp, input.uv);
                float warpedSignedDist = warpedSdfData.r * warpedSdfData.g;
            
                float bandMask = 0;
                // Only draw bands inside the shape
                if (warpedSignedDist < 0) 
                {
                    // This is the KEY to consistent thickness: measure the local gradient of the warped field.
                    float grad_mag = fwidth(warpedSignedDist);
            
                    // Loop to generate each band procedurally
                    for (int j = 0; j < _NumberOfBands; j++)
                    {
                        // Calculate the target distance for the current band's centerline
                        float bandIso = _BandStartOffset + (j * _BandInterval);
                        
                        // Calculate the pixel's distance from that centerline
                        float distToBand = abs(warpedSignedDist - bandIso);
                        
                        // Normalize this distance by the gradient. This cancels out the stretching/squashing.
                        float correctedDist = distToBand / grad_mag;
            
                        // Now draw the band using a constant pixel width.
                        float band = 1.0 - smoothstep(_BandLineWidth, _BandLineWidth + 1.5, correctedDist);
                        
                        // Combine the alpha of all bands
                        bandMask = max(bandMask, band);
                    }
                }
            
                // --- COMPOSITING ---
                // Start with the base field color and make it transparent
                half4 finalColor = half4(fieldColor.rgb, 0);
            
                // Add bands first
                finalColor.rgb = lerp(finalColor.rgb, fieldColor.rgb * 0.7, bandMask); // Darken field color for bands
                finalColor.a = max(finalColor.a, bandMask);
            
                // Add main outline on top, which will overwrite bands where they overlap
                finalColor.rgb = lerp(finalColor.rgb, fieldColor.rgb, outlineAlpha);
                finalColor.a = max(finalColor.a, outlineAlpha);
                
                // Discard fully transparent pixels for performance
                if (finalColor.a < 0.01)
                {
                    discard;
                }
            
                return finalColor;
            }
            // Inside ProceduralPlanetRenderer.shader

            // half4 frag(Varyings input) : SV_Target
            // {
            //     // --- Sample all necessary data ---
            //     half4 fieldColor = SAMPLE_TEXTURE2D(_ColorTexture, sampler_linear_clamp, input.uv);
            //     half4 mainSdfData = SAMPLE_TEXTURE2D(_SDFTexture, sampler_linear_clamp, input.uv);
            //     half4 warpedSdfData = SAMPLE_TEXTURE2D(_WarpedSDFTexture, sampler_linear_clamp, input.uv);
            //
            //     float mainSignedDist = mainSdfData.r * mainSdfData.g;
            //     float warpedSignedDist = warpedSdfData.r * warpedSdfData.g;
            //
            //     //------------------------------------------------------------------------------------//
            //     // --- DEBUG MODES (Uncomment ONE at a time) ---
            //     //------------------------------------------------------------------------------------//
            //
            //     // MODE 1: Is the WarpedSDFTexture receiving data?
            //     // Should show a grayscale gradient. If it's all one color (e.g., black), the texture isn't being generated or set correctly.
            //     // return half4(warpedSdfData.rrr, 1);
            //
            //     // MODE 2: Is the sign correct? 
            //     // This should paint the INSIDE of the planet GREEN and the OUTSIDE RED.
            //     // If you see no green, the 'if (warpedSignedDist < 0)' condition is never met.
            //     if (warpedSignedDist < 0) { return half4(0, 1, 0, 1); } else { return half4(1, 0, 0, 1); }
            //
            //     // --- If Mode 2 shows green, continue debugging the band logic ---
            //     float grad_mag = fwidth(warpedSignedDist);
            //     if (grad_mag == 0) grad_mag = 1e-6; // Prevent division by zero
            //
            //     float bandMask = 0;
            //     if (warpedSignedDist < 0)
            //     {
            //         for (int j = 0; j < _NumberOfBands; j++)
            //         {
            //             float bandIso = _BandStartOffset + (j * _BandInterval);
            //             float distToBand = abs(warpedSignedDist - bandIso);
            //             float correctedDist = distToBand / grad_mag;
            //             float band = 1.0 - smoothstep(_BandLineWidth, _BandLineWidth + 1.5, correctedDist);
            //             bandMask = max(bandMask, band);
            //         }
            //     }
            //
            //     // MODE 3: Is the final bandMask being calculated?
            //     // This should show white where bands OUGHT to be, and black elsewhere.
            //     // If this is all black, the smoothstep logic is failing. This could mean _BandLineWidth is too small
            //     // or correctedDist is too large.
            //     // return bandMask;
            //
            //
            //     //------------------------------------------------------------------------------------//
            //     // --- ORIGINAL RENDERING LOGIC ---
            //     //------------------------------------------------------------------------------------//
            //
            //     // Re-enable this once debugging is complete.
            //     float outlineAlpha = get_line_alpha(mainSignedDist, _LineWidth * 0.01);
            //     half4 finalColor = half4(fieldColor.rgb, 0);
            //     finalColor.rgb = lerp(finalColor.rgb, fieldColor.rgb * 0.7, bandMask);
            //     finalColor.a = max(finalColor.a, bandMask);
            //     finalColor.rgb = lerp(finalColor.rgb, fieldColor.rgb, outlineAlpha);
            //     finalColor.a = max(finalColor.a, outlineAlpha);
            //     if (finalColor.a < 0.01) { discard; }
            //     return finalColor;
            // }
            ENDHLSL
        }
    }
}