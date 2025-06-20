Shader "PlanetGen/UnlitMultiTex"
{
    Properties
    {
        _FieldTex ("Texture", 2D) = "white" {}
        _ColorTex ("Texture", 2D) = "white" {}
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
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _FieldTex);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample the texture and return raw RGBA values
                half4 field = SAMPLE_TEXTURE2D(_FieldTex, sampler_FieldTex, input.uv);
                half4 col = SAMPLE_TEXTURE2D(_ColorTex, sampler_ColorTex, input.uv);
                return col * field;
            }
            ENDHLSL
        }
    }
    
    Fallback "Universal Render Pipeline/Unlit"
}