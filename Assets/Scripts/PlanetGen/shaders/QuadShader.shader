//Shader "Custom/QuadShader"
//{
//    SubShader
//    {
//        Tags { "RenderType"="Opaque" }
//        
//        Pass
//        {
//            CGPROGRAM
//            #pragma vertex vert
//            #pragma fragment frag
//            #pragma target 4.5
//            
//            #include "UnityCG.cginc"
//            
//            StructuredBuffer<float3> VertexBuffer;
//            StructuredBuffer<float4> VertexColorBuffer;
//            
//            struct v2f
//            {
//                float4 pos : SV_POSITION;
//                float4 color : COLOR;
//            };
//            
//            v2f vert(uint vertexID : SV_VertexID)
//            {
//                v2f o;
//                o.pos = UnityObjectToClipPos(float4(VertexBuffer[vertexID], 1.0));
//                o.color = VertexColorBuffer[vertexID];
//                return o;
//            }
//            
//            float4 frag(v2f i) : SV_Target
//            {
//                // return float4(1.0,0.0,0.0,1.0);
//                return i.color;
//            }
//            ENDCG
//        }
//    }
//}

Shader "Universal Render Pipeline/Custom/QuadShader"
{
    Properties
    {
        // Add properties here if needed
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            StructuredBuffer<float3> VertexBuffer;
            StructuredBuffer<float4> VertexColorBuffer;
            
            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = VertexBuffer[input.vertexID];
                output.positionHCS = TransformObjectToHClip(positionOS);
                output.color = VertexColorBuffer[input.vertexID];
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}