Shader "PlanetGen/SDFViz"
{
    Properties
    {
        _MainTex ("SDF Texture", 2D) = "white" {}
        _DistanceMultiplier ("Distance Multiplier", Float) = 10.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "Unlit"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _DistanceMultiplier;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample the SDF texture (RGBA format)
                float4 sdfData = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Extract distance from red channel and apply multiplier
                float distance = sdfData.r * _DistanceMultiplier;
                
                // Clamp to [0,1] range for visualization
                distance = saturate(distance);
                
                // Output as grayscale
                return half4(distance, distance, distance, 1.0);
            }
            ENDHLSL
        }
    }
}