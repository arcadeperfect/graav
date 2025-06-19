Shader "Universal Render Pipeline/Custom/Procedural Contour Shader (Unlit)"
{
    Properties { }

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

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Match your compute shader structs EXACTLY
            struct ContourVertex
            {
                float3 position;
                float3 normal;
                float4 color;
            };

            struct Triangle
            {
                ContourVertex v0;
                ContourVertex v1;
                ContourVertex v2;
            };

            // Buffer of triangles, not individual vertices
            StructuredBuffer<Triangle> TriangleBuffer;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float4 color        : COLOR;
                float3 normalWS     : TEXCOORD0; 
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Calculate which triangle and which vertex within that triangle
                uint triangleIndex = input.vertexID / 3;
                uint vertexInTriangle = input.vertexID % 3;

                // Get the triangle
                Triangle tri = TriangleBuffer[triangleIndex];
                
                // Get the specific vertex from the triangle
                ContourVertex vertexData;
                if (vertexInTriangle == 0)
                    vertexData = tri.v0;
                else if (vertexInTriangle == 1)
                    vertexData = tri.v1;
                else
                    vertexData = tri.v2;

                // Transform and output
                float3 positionOS = vertexData.position;
                float3 normalOS = vertexData.normal;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.normalWS = normalInputs.normalWS;
                output.color = vertexData.color;

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