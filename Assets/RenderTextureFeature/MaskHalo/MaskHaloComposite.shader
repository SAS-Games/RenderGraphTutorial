Shader "Hidden/RenderTextureFeature/MaskHalo/Composite"
{
    Properties
    {
        _OuterGlowColor("Outer Glow Color", Color) = (0, 0.18, 1, 1)
        _InnerGlowColor("Inner Glow Color", Color) = (0, 0.7, 1, 1)
        _RimColor("Rim Color", Color) = (0.55, 0.95, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_HaloBlurTexture);

        CBUFFER_START(UnityPerMaterial)
            float4 _BlurTexelSize;
            float4 _MaskTexelSize;
            float4 _OuterGlowColor;
            float4 _InnerGlowColor;
            float4 _RimColor;
            float _BlurOffset;
            float _MaskThreshold;
            float _MaskSoftness;
            float _OuterGlowIntensity;
            float _OuterGlowFalloff;
            float _InnerGlowIntensity;
            float _InnerGlowTightness;
            float _RimWidth;
            float _RimIntensity;
            float _Opacity;
        CBUFFER_END

        half SampleMask(float2 uv)
        {
            half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
            float threshold = saturate(_MaskThreshold);
            float softness = max(_MaskSoftness, 0.0001);
            return smoothstep(threshold, min(threshold + softness, 1.0), mask);
        }

        half4 KawaseBlurFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            float2 offset = _BlurTexelSize.xy * max(_BlurOffset, 0.0);

            half value = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r * 0.2h;
            value += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( offset.x,  offset.y)).r * 0.2h;
            value += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x,  offset.y)).r * 0.2h;
            value += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( offset.x, -offset.y)).r * 0.2h;
            value += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, -offset.y)).r * 0.2h;

            return half4(value, value, value, 1.0h);
        }

        half FindOuterRim(float2 uv, half centerMask)
        {
            float2 texel = _MaskTexelSize.xy * max(_RimWidth, 0.5);
            float2 diagonal = texel * 0.70710678;
            half neighbor = 0.0h;

            neighbor = max(neighbor, SampleMask(uv + float2( texel.x, 0.0)));
            neighbor = max(neighbor, SampleMask(uv + float2(-texel.x, 0.0)));
            neighbor = max(neighbor, SampleMask(uv + float2(0.0,  texel.y)));
            neighbor = max(neighbor, SampleMask(uv + float2(0.0, -texel.y)));
            neighbor = max(neighbor, SampleMask(uv + float2( diagonal.x,  diagonal.y)));
            neighbor = max(neighbor, SampleMask(uv + float2(-diagonal.x,  diagonal.y)));
            neighbor = max(neighbor, SampleMask(uv + float2( diagonal.x, -diagonal.y)));
            neighbor = max(neighbor, SampleMask(uv + float2(-diagonal.x, -diagonal.y)));

            return smoothstep(0.02h, 0.95h, saturate(neighbor - centerMask));
        }

        half4 GlowCompositeFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            half mask = SampleMask(uv);
            half outside = 1.0h - mask;
            half blurredMask = saturate(SAMPLE_TEXTURE2D_X(
                _HaloBlurTexture,
                sampler_LinearClamp,
                uv).r);

            half glowCoverage = blurredMask * outside;
            half outerGlow = pow(glowCoverage, max(_OuterGlowFalloff, 0.25));
            half innerGlow = pow(glowCoverage, max(_InnerGlowTightness, 1.0));

            half3 color = 0.0h;
            color += _OuterGlowColor.rgb * _OuterGlowColor.a * outerGlow * _OuterGlowIntensity;
            color += _InnerGlowColor.rgb * _InnerGlowColor.a * innerGlow * _InnerGlowIntensity;

            return half4(color * saturate(_Opacity), 0.0h);
        }

        half4 RimCompositeFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            half mask = SampleMask(uv);
            half rim = FindOuterRim(uv, mask);
            half alpha = saturate(rim * _RimColor.a * _Opacity);

            return half4(_RimColor.rgb * max(_RimIntensity, 0.0), alpha);
        }
        ENDHLSL

        Pass
        {
            Name "KawaseBlur"
            Blend Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment KawaseBlurFragment
            ENDHLSL
        }

        Pass
        {
            Name "GlowComposite"
            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment GlowCompositeFragment
            ENDHLSL
        }

        Pass
        {
            Name "RimComposite"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment RimCompositeFragment
            ENDHLSL
        }
    }
}
