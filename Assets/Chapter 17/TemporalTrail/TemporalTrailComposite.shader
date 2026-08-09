Shader "Hidden/Chapter17/TemporalTrail/Composite"
{
    Properties
    {
        [HDR] _TrailColor("Trail Color", Color) = (0.15, 0.8, 2, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_TemporalSourceTexture);
        TEXTURE2D_X(_TemporalHistoryTexture);
        TEXTURE2D_X(_TemporalMotionTexture);
        TEXTURE2D_X(_TemporalMaskTexture);

        CBUFFER_START(UnityPerMaterial)
            float4 _TrailColor;
            float _HistoryValid;
            float _HistoryRetention;
            float _CaptureCurrentFrame;
            float _MotionVectorScale;
            float _MaskThreshold;
            float _MaskSoftness;
            float _Intensity;
            float _SuppressCurrentFrame;
        CBUFFER_END

        half EvaluateMask(half value)
        {
            half softness = max(_MaskSoftness, 0.0001h);
            return smoothstep(_MaskThreshold, _MaskThreshold + softness, value);
        }
        ENDHLSL

        Pass
        {
            Name "TemporalAccumulation"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment AccumulateFragment

            half4 AccumulateFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half currentCoverage = EvaluateMask(
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r)
                    * _CaptureCurrentFrame;

                half2 motion = SAMPLE_TEXTURE2D_X(
                    _TemporalMotionTexture, sampler_LinearClamp, uv).xy;
                float2 historyUv = uv - motion * _MotionVectorScale;
                half historyInside =
                    (all(historyUv >= 0.0) && all(historyUv <= 1.0)) ? 1.0h : 0.0h;

                half4 previous = 0.0h;
                UNITY_BRANCH
                if (_HistoryValid > 0.5h && historyInside > 0.5h)
                {
                    previous = SAMPLE_TEXTURE2D_X(
                        _TemporalHistoryTexture, sampler_LinearClamp, historyUv);
                    previous *= _HistoryRetention;
                }

                half3 source = SAMPLE_TEXTURE2D_X(
                    _TemporalSourceTexture, sampler_LinearClamp, uv).rgb;
                half3 currentPremultiplied = source * currentCoverage;

                half outputCoverage = max(previous.a, currentCoverage);
                half3 outputColor = lerp(previous.rgb, currentPremultiplied, currentCoverage);
                return half4(outputColor, outputCoverage);
            }
            ENDHLSL
        }

        Pass
        {
            Name "TemporalComposite"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFragment

            half4 CompositeFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half4 trail = SAMPLE_TEXTURE2D_X(
                    _TemporalHistoryTexture, sampler_LinearClamp, uv);
                half currentCoverage = EvaluateMask(SAMPLE_TEXTURE2D_X(
                    _TemporalMaskTexture, sampler_LinearClamp, uv).r);

                half reveal = 1.0h - currentCoverage * _SuppressCurrentFrame;
                half3 trailColor = trail.rgb * _TrailColor.rgb * (_Intensity * reveal);
                return half4(source.rgb + trailColor, source.a);
            }
            ENDHLSL
        }
    }
}
