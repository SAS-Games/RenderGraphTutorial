Shader "Hidden/RenderTextureFeature/FrameTextureProcessing/MaskThreshold"
{
    Properties
    {
        _Threshold("Threshold", Range(0, 1)) = 0.5
        _Softness("Softness", Range(0, 1)) = 0
        [Toggle] _Invert("Invert", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "MaskThreshold"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Threshold;
                float _Softness;
                float _Invert;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half source = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    input.texcoord).r;

                float threshold = saturate(_Threshold);
                float softness = saturate(_Softness);
                half result = step(threshold, source);

                if (softness > 0.0001)
                {
                    float halfWidth = softness * 0.5;
                    result = smoothstep(
                        saturate(threshold - halfWidth),
                        saturate(threshold + halfWidth),
                        source);
                }

                result = lerp(result, 1.0h - result, saturate(_Invert));
                return half4(result, result, result, result);
            }

            ENDHLSL
        }
    }
}
