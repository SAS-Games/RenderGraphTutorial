Shader "Hidden/RenderTextureFeature/Examples/WorldHeightGradientMask"
{
    Properties
    {
        _Bottom("World Bottom", Float) = 0
        _Top("World Top", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "WorldHeightGradientMask"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Bottom;
                float _Top;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float range = max(_Top - _Bottom, 0.0001);
                half mask = saturate((input.positionWS.y - _Bottom) / range);
                return half4(mask, mask, mask, mask);
            }
            ENDHLSL
        }
    }
}

