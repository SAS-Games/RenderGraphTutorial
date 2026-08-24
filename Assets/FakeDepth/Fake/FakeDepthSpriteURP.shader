Shader "Custom/FakeDepthSpriteURP"
{
    Properties
    {
        [PerRendererData]
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
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
            Name "FakeDepthSprite"

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

            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);


            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END


            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };


            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }


            half4 Frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);


                // Sprite texture
                // × overall tint
                // × per-depth vertex alpha

                half4 finalColor = texColor * _Color * input.color;
                return finalColor;
            }
            ENDHLSL
        }
    }
}