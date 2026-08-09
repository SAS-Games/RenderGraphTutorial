Shader "Hidden/Chapter11/JumpFloodDistanceField"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_JfaMaskTexture);
        TEXTURE2D_X(_JfaSeedTexture);

        CBUFFER_START(UnityPerMaterial)
            float4 _WorkingTexelSize;
            float4 _MaskTexelSize;
            float _MaskThreshold;
            float _SubpixelBoundary;
            float _JumpStep;
            float _MaxDistancePixels;
            float _DebugMode;
            float _DebugRangePixels;
            float _DebugContourSpacing;
            float _DebugOpacity;
        CBUFFER_END

        bool IsValidSeed(float2 seedUv)
        {
            return seedUv.x >= 0.0 && seedUv.y >= 0.0;
        }

        half SampleMask(float2 uv)
        {
            return step(
                _MaskThreshold,
                SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r);
        }

        float4 InitializeFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            half center = SampleMask(uv);
            float2 texel = _WorkingTexelSize.xy;
            half right = SampleMask(uv + float2( texel.x, 0.0));
            half left = SampleMask(uv + float2(-texel.x, 0.0));
            half up = SampleMask(uv + float2(0.0,  texel.y));
            half down = SampleMask(uv + float2(0.0, -texel.y));
            half upperRight = SampleMask(uv + float2( texel.x,  texel.y));
            half upperLeft = SampleMask(uv + float2(-texel.x,  texel.y));
            half lowerRight = SampleMask(uv + float2( texel.x, -texel.y));
            half lowerLeft = SampleMask(uv + float2(-texel.x, -texel.y));

            half edge = 0.0h;
            edge = max(edge, abs(right - center));
            edge = max(edge, abs(left - center));
            edge = max(edge, abs(up - center));
            edge = max(edge, abs(down - center));
            edge = max(edge, abs(upperRight - center));
            edge = max(edge, abs(upperLeft - center));
            edge = max(edge, abs(lowerRight - center));
            edge = max(edge, abs(lowerLeft - center));

            float2 seedUv = float2(-1.0, -1.0);
            if (edge > 0.0h)
            {
                seedUv = uv;
                float2 gradient = float2(
                    (right * 2.0h + upperRight + lowerRight) -
                    (left * 2.0h + upperLeft + lowerLeft),
                    (up * 2.0h + upperLeft + upperRight) -
                    (down * 2.0h + lowerLeft + lowerRight));
                float gradientLength = length(gradient);

                if (_SubpixelBoundary > 0.5 && gradientLength > 0.0001)
                {
                    float2 normal = gradient / gradientLength;
                    float2 directionToBoundary = center > 0.5h ? -normal : normal;
                    float boundaryScale = 0.5 / max(
                        max(abs(directionToBoundary.x), abs(directionToBoundary.y)),
                        0.0001);
                    seedUv = saturate(
                        uv + directionToBoundary * texel * boundaryScale);
                }
            }

            return float4(seedUv, 0.0, 1.0);
        }

        void ConsiderSeed(
            float2 pixelUv,
            float2 candidateSeed,
            inout float2 bestSeed,
            inout float bestDistanceSquared)
        {
            if (!IsValidSeed(candidateSeed))
            {
                return;
            }

            float2 delta = candidateSeed - pixelUv;
            float distanceSquared = dot(delta, delta);

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestSeed = candidateSeed;
            }
        }

        float4 JumpFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            float2 jumpUv = _WorkingTexelSize.xy * max(_JumpStep, 1.0);
            float2 bestSeed = float2(-1.0, -1.0);
            float bestDistanceSquared = 1e20;

            ConsiderSeed(uv, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rg, bestSeed, bestDistanceSquared);
            ConsiderSeed(uv, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2( jumpUv.x, 0.0)).rg, bestSeed, bestDistanceSquared);
            ConsiderSeed(uv, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(-jumpUv.x, 0.0)).rg, bestSeed, bestDistanceSquared);
            ConsiderSeed(uv, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(0.0,  jumpUv.y)).rg, bestSeed, bestDistanceSquared);
            ConsiderSeed(uv, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(0.0, -jumpUv.y)).rg, bestSeed, bestDistanceSquared);
            ConsiderSeed(uv, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2( jumpUv.x,  jumpUv.y)).rg, bestSeed, bestDistanceSquared);
            ConsiderSeed(uv, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(-jumpUv.x,  jumpUv.y)).rg, bestSeed, bestDistanceSquared);
            ConsiderSeed(uv, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2( jumpUv.x, -jumpUv.y)).rg, bestSeed, bestDistanceSquared);
            ConsiderSeed(uv, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(-jumpUv.x, -jumpUv.y)).rg, bestSeed, bestDistanceSquared);

            return float4(bestSeed, 0.0, 1.0);
        }

        float4 ResolveFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.texcoord;
            float2 seedUv = SAMPLE_TEXTURE2D_X(_JfaSeedTexture, sampler_PointClamp, uv).rg;
            half mask = step(
                _MaskThreshold,
                SAMPLE_TEXTURE2D_X(_JfaMaskTexture, sampler_PointClamp, uv).r);

            float distancePixels = _MaxDistancePixels;
            if (IsValidSeed(seedUv))
            {
                distancePixels = length((seedUv - uv) * _MaskTexelSize.zw);
                distancePixels = min(distancePixels, _MaxDistancePixels);
            }

            float signedDistance = mask > 0.5h ? -distancePixels : distancePixels;
            return float4(signedDistance, 0.0, 0.0, 1.0);
        }

        half4 DebugFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float distancePixels = SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_LinearClamp,
                input.texcoord).r;

            float range = max(_DebugRangePixels, 1.0);
            float normalizedDistance = saturate(abs(distancePixels) / range);
            half3 insideColor = half3(1.0h, 0.15h, 0.1h);
            half3 outsideColor = half3(0.05h, 0.35h, 1.0h);
            half3 signColor = distancePixels < 0.0 ? insideColor : outsideColor;
            half boundary = 1.0h - smoothstep(0.0, 1.5, abs(distancePixels));
            half3 color = lerp(signColor, 0.05h.xxx, normalizedDistance);
            color = lerp(color, 1.0h.xxx, boundary);

            if (_DebugMode > 1.5)
            {
                float spacing = max(_DebugContourSpacing, 1.0);
                float contourPosition = frac(abs(distancePixels) / spacing);
                float contourDistance = min(contourPosition, 1.0 - contourPosition);
                half contour = 1.0h - smoothstep(0.0, 0.08, contourDistance);
                color = lerp(color * 0.35h, 1.0h.xxx, contour);
            }

            return half4(color, saturate(_DebugOpacity));
        }
        ENDHLSL

        Pass
        {
            Name "InitializeBoundarySeeds"
            Blend Off
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment InitializeFragment
            ENDHLSL
        }

        Pass
        {
            Name "JumpFlood"
            Blend Off
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment JumpFragment
            ENDHLSL
        }

        Pass
        {
            Name "ResolveSignedDistance"
            Blend Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ResolveFragment
            ENDHLSL
        }

        Pass
        {
            Name "DebugDistanceField"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DebugFragment
            ENDHLSL
        }
    }
}
