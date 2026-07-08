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

        CBUFFER_START(UnityPerMaterial)
            float4 _BlurDirection;
            float4 _BlurTexelSize;
            float _BlurRadius;
            float _MaskThreshold;
            float _MaskSoftness;
            float _Opacity;
        CBUFFER_END

        half4 BlurFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            float2 sampleStep = _BlurDirection.xy * _BlurTexelSize.xy * max(_BlurRadius, 0.0);

            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * 0.2270270270;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + sampleStep * 1.0) * 0.1945945946;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - sampleStep * 1.0) * 0.1945945946;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + sampleStep * 2.0) * 0.1216216216;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - sampleStep * 2.0) * 0.1216216216;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + sampleStep * 3.0) * 0.0540540541;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - sampleStep * 3.0) * 0.0540540541;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + sampleStep * 4.0) * 0.0162162162;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - sampleStep * 4.0) * 0.0162162162;

            return color;
        }

        half4 CompositeFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
            float threshold = saturate(_MaskThreshold);
            float softness = saturate(_MaskSoftness);
            half coverage = mask > threshold ? 1.0h : 0.0h;

            if (softness > 0.0001 && threshold < 0.9999)
            {
                coverage = smoothstep(threshold, min(threshold + softness, 1.0), mask);
            }

            coverage *= mask > 0.0001 ? 1.0h : 0.0h;
            half4 blurredColor = SAMPLE_TEXTURE2D_X(_LayerBlurredTexture, sampler_LinearClamp, uv);

            return half4(blurredColor.rgb, saturate(coverage * _Opacity));
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

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFragment
            ENDHLSL
        }
    }
}
