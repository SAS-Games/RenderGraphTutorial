Shader "Hidden/RenderTextureFeature/MaskDistortion/Composite"
{
    Properties
    {
        _DistortionStrengthPixels("Distortion Strength Pixels", Range(0, 32)) = 4
        _DistortionFrequency("Distortion Frequency", Range(0.1, 80)) = 18
        _DistortionSpeed("Distortion Speed", Range(-10, 10)) = 0.35
        _ChromaticAberrationPixels("Chromatic Aberration Pixels", Range(0, 8)) = 0.4
        _MaskThreshold("Mask Threshold", Range(0, 1)) = 0.5
        _MaskSoftness("Mask Softness", Range(0, 1)) = 0.08
        _Opacity("Opacity", Range(0, 1)) = 0.65
        _TintColor("Tint Color", Color) = (0.75, 0.95, 1, 1)
        _TintStrength("Tint Strength", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "MaskDistortion"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_MaskDistortionSourceTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _SourceTexelSize;
                float _DistortionStrengthPixels;
                float _DistortionFrequency;
                float _DistortionSpeed;
                float _ChromaticAberrationPixels;
                float _MaskThreshold;
                float _MaskSoftness;
                float _Opacity;
                float4 _TintColor;
                float _TintStrength;
                float _TimeOffset;
            CBUFFER_END

            half GetCoverage(half mask)
            {
                float threshold = saturate(_MaskThreshold);
                float softness = saturate(_MaskSoftness);
                half coverage = mask > threshold ? 1.0h : 0.0h;

                if (softness > 0.0001 && threshold < 0.9999)
                {
                    coverage = smoothstep(threshold, min(threshold + softness, 1.0), mask);
                }

                return coverage * (mask > 0.0001 ? 1.0h : 0.0h);
            }

            float2 GetDistortionOffset(float2 uv)
            {
                float frequency = max(_DistortionFrequency, 0.1);
                float time = _TimeOffset * _DistortionSpeed;

                float waveA = sin((uv.y * frequency + time) * 6.2831853);
                float waveB = cos((uv.x * frequency * 1.37 - time * 0.83) * 6.2831853);
                float waveC = sin(((uv.x + uv.y) * frequency * 0.73 + time * 1.31) * 6.2831853);
                float waveD = cos(((uv.x - uv.y) * frequency * 0.91 - time * 1.17) * 6.2831853);

                float2 wave = float2(waveA + waveC * 0.5, waveB + waveD * 0.5) * 0.5;
                return wave * _DistortionStrengthPixels * _SourceTexelSize.xy;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
                half coverage = GetCoverage(mask);

                float2 offset = GetDistortionOffset(uv);
                float2 chromaticOffset = normalize(offset + 0.00001) *
                    _ChromaticAberrationPixels * _SourceTexelSize.xy;

                float2 sampleUv = saturate(uv + offset);
                half4 center = SAMPLE_TEXTURE2D_X(
                    _MaskDistortionSourceTexture,
                    sampler_LinearClamp,
                    sampleUv);

                if (_ChromaticAberrationPixels > 0.0001)
                {
                    half red = SAMPLE_TEXTURE2D_X(
                        _MaskDistortionSourceTexture,
                        sampler_LinearClamp,
                        saturate(sampleUv + chromaticOffset)).r;

                    half blue = SAMPLE_TEXTURE2D_X(
                        _MaskDistortionSourceTexture,
                        sampler_LinearClamp,
                        saturate(sampleUv - chromaticOffset)).b;

                    center.r = red;
                    center.b = blue;
                }

                half tintStrength = saturate(_TintStrength) * _TintColor.a;
                half3 tinted = lerp(center.rgb, _TintColor.rgb, tintStrength);
                half alpha = saturate(coverage * _Opacity);

                return half4(tinted, alpha);
            }

            ENDHLSL
        }
    }
}
