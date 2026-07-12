Shader "Hidden/RenderTextureFeature/JumpFloodDistanceField/Outline"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (0, 0.75, 1, 1)
        _BandStartPixels("Band Start Pixels", Float) = 0
        _BandEndPixels("Band End Pixels", Float) = 16
        _SoftnessPixels("Softness Pixels", Float) = 2
        _Intensity("Intensity", Float) = 1.5
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

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _BandPlacement;
            float _BandStartPixels;
            float _BandEndPixels;
            float _SoftnessPixels;
            float _Intensity;
            float _Opacity;
        CBUFFER_END

        half EvaluateBand(float signedDistance)
        {
            float distanceFromEdge;
            half sideCoverage = 1.0h;
            float derivativeWidth = max(fwidth(signedDistance), 0.5);
            float softness = max(_SoftnessPixels, derivativeWidth);

            if (_BandPlacement < 0.5)
            {
                distanceFromEdge = signedDistance;
                sideCoverage = smoothstep(-softness, softness, signedDistance);
            }
            else if (_BandPlacement < 1.5)
            {
                distanceFromEdge = -signedDistance;
                sideCoverage = smoothstep(-softness, softness, -signedDistance);
            }
            else
            {
                distanceFromEdge = abs(signedDistance);
            }

            float bandStart = max(_BandStartPixels, 0.0);
            float bandEnd = max(_BandEndPixels, bandStart + 0.001);
            half startCoverage = bandStart <= 0.0001
                ? 1.0h
                : smoothstep(bandStart - softness, bandStart + softness, distanceFromEdge);
            half endCoverage = 1.0h - smoothstep(
                bandEnd - softness,
                bandEnd + softness,
                distanceFromEdge);
            return saturate(sideCoverage * startCoverage * endCoverage);
        }

        half4 AlphaFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float signedDistance = SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_LinearClamp,
                input.texcoord).r;
            half coverage = EvaluateBand(signedDistance);
            half alpha = saturate(coverage * _Color.a * _Opacity);
            return half4(_Color.rgb * max(_Intensity, 0.0), alpha);
        }

        half4 AdditiveFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float signedDistance = SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_LinearClamp,
                input.texcoord).r;
            half coverage = EvaluateBand(signedDistance);
            half strength = saturate(coverage * _Color.a * _Opacity);
            return half4(_Color.rgb * max(_Intensity, 0.0) * strength, 0.0h);
        }
        ENDHLSL

        Pass
        {
            Name "Alpha"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment AlphaFragment
            ENDHLSL
        }

        Pass
        {
            Name "Additive"
            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment AdditiveFragment
            ENDHLSL
        }
    }
}
