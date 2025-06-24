Shader "PlanetGen/FieldPreview"
{
    Properties
    {
        _SDFTex ("Texture", 2D) = "green" {}
        _Mode("Display Mode", Int) = 0
        _Alpha("Opacity", Float) = 1
        _Mult("Mult" , Float) = 1
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
            Tags { "LightMode" = "UniversalForward" }
            
            // Enable alpha blending
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
            
            TEXTURE2D(_SDFTex);
            SAMPLER(sampler_SDFTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _SDFTex_ST;
                int _Mode;
                float _Alpha;
                float _Mult;
            CBUFFER_END


            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv; // Use raw UVs for debugging
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample the texture and return raw RGBA values
                half4 sdf = SAMPLE_TEXTURE2D(_SDFTex, sampler_SDFTex, input.uv);

                // return half4(sdf.r, sdf.g, sdf.b, 1); 

                sdf *= _Mult;
                
                switch (_Mode)
                {
                case 0:
                    return half4(sdf.rgb, _Alpha); // Return the raw SDF texture color
                case 1:
                    return half4(sdf.rrr, _Alpha); // Return the color texture color
                case 2:
                    return half4(sdf.ggg, _Alpha); // Return the color texture color
                case 3:
                    return half4(sdf.bbb, _Alpha); // Return the color texture color
                }
                return half4(0, 0, 0, 0); // Default to transparent if mode is invalid
                
                // switch (_Mode)
                // {
                //     case 0:
                //         return field * _Alpha; // Return the raw field texture color
                //     case 1:
                //         return col * _Alpha; // Return the color texture color
                //     case 2:
                //         return field.r * col * _Alpha; // Return the color texture color modulated by the red channel of the field
                // }
                // return half4(0, 0, 0, 0); // Default to transparent if mode is invalid
            }
            ENDHLSL
        }
    }
    
    Fallback "Universal Render Pipeline/Unlit"
}