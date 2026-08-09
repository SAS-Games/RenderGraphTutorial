Shader "Hidden/RenderTextureFeature/MaskOutline/Composite"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.82, 0, 1)
        _OutlineWidth("Outline Width", Range(1, 16)) = 3
        _OutlineIntensity("Outline Intensity", Range(0, 5)) = 1
        _MaskThreshold("Mask Threshold", Range(0, 1)) = 0.5
        _OutlineMode("Outline Mode", Float) = 0
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

        #define MAX_OUTLINE_RADIUS 16

        TEXTURE2D_X(_MorphologyTexture);

        CBUFFER_START(UnityPerMaterial)
            float4 _OutlineColor;
            float4 _MaskTexelSize;
            float _OutlineWidth;
            float _OutlineIntensity;
            float _MaskThreshold;
            float _OutlineMode;
        CBUFFER_END

        half SampleBinaryMask(float2 uv)
        {
            half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r;
            return step(_MaskThreshold, mask);
        }

        half4 HorizontalMorphologyFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            int radius = clamp((int)round(_OutlineWidth), 1, MAX_OUTLINE_RADIUS);
            half expanded = 0.0h;
            half eroded = 1.0h;

            [loop]
            for (int x = -radius; x <= radius; x++)
            {
                half mask = SampleBinaryMask(uv + float2(x * _MaskTexelSize.x, 0.0));
                expanded = max(expanded, mask);
                eroded = min(eroded, mask);
            }

            return half4(expanded, eroded, 0.0h, 1.0h);
        }

        half4 VerticalMorphologyFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            int radius = clamp((int)round(_OutlineWidth), 1, MAX_OUTLINE_RADIUS);
            half expanded = 0.0h;
            half eroded = 1.0h;

            [loop]
            for (int y = -radius; y <= radius; y++)
            {
                half2 morphology = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_PointClamp,
                    uv + float2(0.0, y * _MaskTexelSize.y)).rg;

                expanded = max(expanded, morphology.r);
                eroded = min(eroded, morphology.g);
            }

            return half4(expanded, eroded, 0.0h, 1.0h);
        }

        half4 CompositeFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            half center = SampleBinaryMask(uv);
            half2 morphology = SAMPLE_TEXTURE2D_X(
                _MorphologyTexture,
                sampler_PointClamp,
                uv).rg;

            half outsideEdge = saturate(morphology.r - center);
            half insideEdge = saturate(center - morphology.g);
            half edge = outsideEdge;

            if (_OutlineMode >= 1.5)
            {
                edge = max(outsideEdge, insideEdge);
            }
            else if (_OutlineMode >= 0.5)
            {
                edge = insideEdge;
            }

            half alpha = saturate(edge * _OutlineColor.a * _OutlineIntensity);
            return half4(_OutlineColor.rgb * _OutlineIntensity, alpha);
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
            Name "OutlineComposite"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CompositeFragment
            ENDHLSL
        }
    }
}
