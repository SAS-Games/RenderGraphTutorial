Shader "Hidden/RenderTextureFeature/LayerBlur/BlurComposite"
{
    Properties
    {
        _BlurRadius("Blur Radius", Range(0, 8)) = 2
        _MaskThreshold("Mask Threshold", Range(0, 1)) = 0.5
        _MaskSoftness("Mask Softness", Range(0, 1)) = 0.05
        _Opacity("Opacity", Range(0, 1)) = 1
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

        TEXTURE2D_X(_LayerBlurredTexture);
        TEXTURE2D_X(_LayerBlurSourceTexture);
        CBUFFER_START(UnityPerMaterial)
            float4 _BlurDirection;
            float4 _BlurTexelSize;
            float _BlurRadius;
            float _MaskThreshold;
            float _MaskSoftness;
            float _Opacity;
        CBUFFER_END

        half EvaluateMaskCoverage(half mask)
        {
            float threshold = saturate(_MaskThreshold);
            float softness = saturate(_MaskSoftness);

            if (softness > 0.0001 && threshold < 0.9999)
            {
                return smoothstep(threshold, min(threshold + softness, 1.0), mask);
            }

            return step(threshold, mask);
        }

        half4 BlurFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            float2 sampleStep = _BlurDirection.xy * _BlurTexelSize.xy * max(_BlurRadius, 0.0);

            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * 0.2270270270h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + sampleStep * 1.0) * 0.1945945946h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - sampleStep * 1.0) * 0.1945945946h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + sampleStep * 2.0) * 0.1216216216h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - sampleStep * 2.0) * 0.1216216216h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + sampleStep * 3.0) * 0.0540540541h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - sampleStep * 3.0) * 0.0540540541h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + sampleStep * 4.0) * 0.0162162162h;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - sampleStep * 4.0) * 0.0162162162h;
            return color;
        }

        half4 CompositeFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
            half coverage = EvaluateMaskCoverage(mask);

            coverage *= mask > 0.0001h ? 1.0h : 0.0h;

            half4 sourceColor = SAMPLE_TEXTURE2D_X(_LayerBlurSourceTexture, sampler_LinearClamp, uv);
            half4 blurredColor = SAMPLE_TEXTURE2D_X(_LayerBlurredTexture, sampler_LinearClamp, uv);
            half3 finalColor = lerp(sourceColor.rgb, blurredColor.rgb, saturate(_Opacity));

            return half4(finalColor, saturate(coverage));
        }
        ENDHLSL

        Pass
        {
            Name "HorizontalBlur"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment BlurFragment
            ENDHLSL
        }

        Pass
        {
            Name "VerticalBlur"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment BlurFragment
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFragment
            ENDHLSL
        }
    }
}
