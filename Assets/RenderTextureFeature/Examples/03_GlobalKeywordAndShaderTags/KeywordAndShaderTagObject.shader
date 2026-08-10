Shader "RenderTextureFeature/Examples/KeywordAndShaderTagObject"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.25, 0.55, 0.9, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ForwardFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 ForwardFrag(Varyings input) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "KeywordCapture"
            Tags { "LightMode"="RTFKeywordCapture" }

            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CaptureFrag
            #pragma multi_compile _ _RTF_KEYWORD_CAPTURE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 CaptureFrag(Varyings input) : SV_Target
            {
                #if defined(_RTF_KEYWORD_CAPTURE_ON)
                    return half4(1, 1, 1, 1);
                #else
                    return half4(0, 0, 0, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
