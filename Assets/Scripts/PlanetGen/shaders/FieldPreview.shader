Shader "PlanetGen/FieldPreview"
{
    Properties
    {
        _FieldTex ("Texture", 2D) = "white" {}
        _ColorTex ("Texture", 2D) = "white" {}
        _Mode("Display Mode", Int) = 0
        _Alpha("Opacity", Float) = 1
        
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
            Name "ForwardLit"
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
            
            TEXTURE2D(_FieldTex);
            TEXTURE2D(_ColorTex);
            SAMPLER(sampler_FieldTex);
            SAMPLER(sampler_ColorTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _FieldTex_ST;
                float4 _ColorTex_ST;
                int _Mode;
                float _Alpha;
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
                half4 field = SAMPLE_TEXTURE2D(_FieldTex, sampler_FieldTex, input.uv);
                half4 col = SAMPLE_TEXTURE2D(_ColorTex, sampler_ColorTex, input.uv);

                switch (_Mode)
                {
                    case 0:
                        return field * _Alpha; // Return the raw field texture color
                    case 1:
                        return col * _Alpha; // Return the color texture color
                    case 2:
                        return field.r * col * _Alpha; // Return the color texture color modulated by the red channel of the field
                }
                return half4(0, 0, 0, 0); // Default to transparent if mode is invalid
            }
            ENDHLSL
        }
    }
    
    Fallback "Universal Render Pipeline/Unlit"
}