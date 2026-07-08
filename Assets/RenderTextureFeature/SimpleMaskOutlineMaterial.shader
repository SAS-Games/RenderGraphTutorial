Shader "Custom/RenderTextureFeature/SimpleMaskOutlineMaterial"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 1, 0, 1)
        _OutlineWidth("Outline Width", Range(1, 8)) = 2
        _OutlineIntensity("Outline Intensity", Range(0, 5)) = 1

    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SimpleMaskOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_MyTexture);
            SAMPLER(sampler_MyTexture);

            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineIntensity;
            float4 _MyTexture_TexelSize;

            half SampleMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_MyTexture, sampler_MyTexture, uv).r;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texelSize = _MyTexture_TexelSize.xy;
                int radius = clamp((int)round(_OutlineWidth), 1, 8);

                half center = SampleMask(uv);
                half expanded = center;

                [loop]
                for (int y = -8; y <= 8; y++)
                {
                    [loop]
                    for (int x = -8; x <= 8; x++)
                    {
                        if (abs(x) > radius || abs(y) > radius)
                        {
                            continue;
                        }

                        expanded = max(expanded, SampleMask(uv + float2(x, y) * texelSize));
                    }
                }

                half edge = saturate(expanded - center);
                half alpha = edge * _OutlineColor.a * _OutlineIntensity;
                return half4(_OutlineColor.rgb * _OutlineIntensity, alpha);
            }
            ENDHLSL
        }
    }
}
