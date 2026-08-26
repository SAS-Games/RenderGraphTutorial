Shader "Hidden/RenderTextureFeature/OuterMaskOutline/Composite"
{
    // Produces an outside-only outline from the Chapter 7 selection mask.
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.82, 0, 1)
        _OutlineWidth("Outline Width", Range(1, 16)) = 3
        _OutlineSoftness("Outline Softness", Range(0, 8)) = 2
        _OutlineIntensity("Outline Intensity", Range(0, 5)) = 1
        _MaskThreshold("Mask Threshold", Range(0, 1)) = 0.5
        _EdgeSoftness("Edge Softness", Range(0.001, 0.25)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Assets/Chapter 7/MaskOutline/Shader/MorphologyUtils.hlsl"

        #define MAX_OUTLINE_RADIUS 24

        TEXTURE2D_X(_MorphologyTexture);

        CBUFFER_START(UnityPerMaterial)
            float4 _OutlineColor;
            float4 _MaskTexelSize;
            float _OutlineWidth;
            float _OutlineSoftness;
            float _OutlineIntensity;
            float _MaskThreshold;
            float _EdgeSoftness;
        CBUFFER_END

        half4 HorizontalMorphologyFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half2 morphology = FeatheredDilation1D(
                TEXTURE2D_X_ARGS(_BlitTexture, sampler_LinearClamp),
                input.texcoord,
                _MaskTexelSize.xy,
                float2(1.0, 0.0),
                _OutlineWidth,
                _OutlineSoftness,
                MAX_OUTLINE_RADIUS,
                _MaskThreshold,
                _EdgeSoftness,
                false
            );

            return half4(morphology, 0.0h, 1.0h);
        }

        half4 VerticalMorphologyFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half2 morphology = FeatheredDilation1D(
                TEXTURE2D_X_ARGS(_BlitTexture, sampler_LinearClamp),
                input.texcoord,
                _MaskTexelSize.xy,
                float2(0.0, 1.0),
                _OutlineWidth,
                _OutlineSoftness,
                MAX_OUTLINE_RADIUS,
                _MaskThreshold,
                _EdgeSoftness,
                true
            );

            return half4(morphology, 0.0h, 1.0h);
        }

        half4 CompositeFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            half center = ApplyMaskThreshold(
                SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv
                ).r,
                _MaskThreshold,
                _EdgeSoftness
            );
            half2 morphology = SAMPLE_TEXTURE2D_X(
                _MorphologyTexture,
                sampler_LinearClamp,
                uv
            ).rg;

            half solidOutside = saturate(morphology.r - center);
            half featheredOutside = saturate(morphology.g - center);
            half edge = max(solidOutside, featheredOutside);

            half alpha = saturate(
                edge * _OutlineColor.a * _OutlineIntensity
            );
            half3 color = _OutlineColor.rgb * _OutlineIntensity;
            return half4(color, alpha);
        }
        ENDHLSL

        Pass
        {
            Name "HorizontalMorphology"
            Blend Off
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment HorizontalMorphologyFragment
            ENDHLSL
        }

        Pass
        {
            Name "VerticalMorphology"
            Blend Off
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment VerticalMorphologyFragment
            ENDHLSL
        }

        Pass
        {
            Name "OuterOutlineComposite"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFragment
            ENDHLSL
        }
    }
}
