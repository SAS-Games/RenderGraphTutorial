Shader "SolidColor"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Color;

            struct MeshData
            {
                float4 positionOS : POSITION;
            };

            struct Interpolators
            {
                float4 positionCS : SV_POSITION;
            };

            Interpolators Vert(MeshData IN)
            {
                Interpolators OUT;

                OUT.positionCS =TransformObjectToHClip(IN.positionOS.xyz);

                return OUT;
            }

            half4 Frag(Interpolators IN) : SV_Target
            {
                return _Color;
            }

            ENDHLSL
        }
    }
}