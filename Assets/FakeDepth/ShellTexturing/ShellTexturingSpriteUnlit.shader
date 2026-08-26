Shader "Custom/URP/Fake Depth/Shell Texturing/Sprite Unlit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0,1)) = 0.001
        [PerRendererData] _ShellOpacity ("Shell Opacity", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ShellTexturingSpriteUnlit"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _AlphaClipThreshold;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(ShellPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShellOpacity)
            UNITY_INSTANCING_BUFFER_END(ShellPerInstance)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half shellOpacity : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.shellOpacity = saturate(UNITY_ACCESS_INSTANCED_PROP(ShellPerInstance, _ShellOpacity));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(textureColor.a - _AlphaClipThreshold);

                half4 finalColor = textureColor * _Tint;
                finalColor.a *= input.shellOpacity;
                return finalColor;
            }
            ENDHLSL
        }
    }
}
