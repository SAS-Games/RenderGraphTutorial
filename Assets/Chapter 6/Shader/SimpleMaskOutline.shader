Shader "Hidden/Chapter6/SimpleOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 1, 0, 1)
        _OutlineWidth("Outline Width", Range(0.001, 0.5)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Outline"
            Cull Front
            ZTest Always
            ZWrite Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalOS = normalize(input.normalOS);
                float3 expandedPositionOS =input.positionOS.xyz + normalOS * _OutlineWidth;

                output.positionCS = TransformObjectToHClip(expandedPositionOS);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
