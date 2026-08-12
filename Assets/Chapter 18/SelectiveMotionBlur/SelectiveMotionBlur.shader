Shader "Hidden/Chapter18/SelectiveMotionBlur/Composite"
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

        TEXTURE2D_X(_SelectiveMotionTexture);
        TEXTURE2D_X(_SelectiveMotionSourceTexture);
        TEXTURE2D_X(_SelectiveMotionVelocityTexture);

        CBUFFER_START(UnityPerMaterial)
            float4 _SourceTexelSize;
            float4 _TileTexelSize;
            int _TileSize;
            int _SampleCount;
            float _ExposureScale;
            float _MaxBlurPixels;
            float _MaskThreshold;
            float _MaskSoftness;
            float _Intensity;
        CBUFFER_END

        half EvaluateMask(half value)
        {
            half softness = max(_MaskSoftness, 0.0001h);
            return smoothstep(_MaskThreshold, _MaskThreshold + softness, value);
        }

        float2 ClampVelocity(float2 velocity)
        {
            float2 velocityPixels = velocity / max(_SourceTexelSize.xy, 1e-6) * _ExposureScale;
            float speedPixels = length(velocityPixels);
            float scale = speedPixels > _MaxBlurPixels
                ? _MaxBlurPixels / max(speedPixels, 1e-5)
                : 1.0;
            return velocityPixels * scale * _SourceTexelSize.xy;
        }
        ENDHLSL

        Pass
        {
            Name "TileMax"
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment TileMaxFragment

            half4 TileMaxFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 tileCenterUv = input.texcoord;
                float2 tileOriginUv = tileCenterUv -
                    _SourceTexelSize.xy * (0.5 * (float)(_TileSize - 1));
                float2 bestVelocity = 0.0;
                half bestCoverage = 0.0h;
                float bestSpeedSq = 0.0;

                [loop]
                for (int y = 0; y < 16; y++)
                {
                    if (y >= _TileSize)
                        break;

                    [loop]
                    for (int x = 0; x < 16; x++)
                    {
                        if (x >= _TileSize)
                            break;

                        float2 sampleUv = tileOriginUv +
                            float2((float)x, (float)y) * _SourceTexelSize.xy;
                        half coverage = EvaluateMask(SAMPLE_TEXTURE2D_X(
                            _BlitTexture, sampler_PointClamp, sampleUv).r);
                        float2 velocity = SAMPLE_TEXTURE2D_X(
                            _SelectiveMotionTexture, sampler_PointClamp, sampleUv).xy;
                        float speedSq = dot(velocity, velocity) * coverage;

                        if (speedSq > bestSpeedSq)
                        {
                            bestSpeedSq = speedSq;
                            bestVelocity = velocity;
                        }

                        bestCoverage = max(bestCoverage, coverage);
                    }
                }

                return half4(bestVelocity, bestCoverage, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "NeighborMax"
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment NeighborMaxFragment

            half4 NeighborMaxFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 best = 0.0h;
                float bestSpeedSq = 0.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        half4 candidate = SAMPLE_TEXTURE2D_X(
                            _BlitTexture,
                            sampler_PointClamp,
                            uv + float2((float)x, (float)y) * _TileTexelSize.xy);
                        float speedSq = dot(candidate.xy, candidate.xy) * candidate.z;
                        if (speedSq > bestSpeedSq)
                        {
                            bestSpeedSq = speedSq;
                            best = candidate;
                        }
                        else
                        {
                            best.z = max(best.z, candidate.z);
                        }
                    }
                }

                return best;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment CompositeFragment

            half4 CompositeFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(
                    _SelectiveMotionSourceTexture, sampler_LinearClamp, uv);
                half4 velocityData = SAMPLE_TEXTURE2D_X(
                    _SelectiveMotionVelocityTexture, sampler_PointClamp, uv);
                float2 velocity = ClampVelocity(velocityData.xy);

                if (velocityData.z <= 0.0001h || dot(velocity, velocity) <= 1e-10)
                    return source;

                half3 accumulatedColor = 0.0h;
                half accumulatedWeight = 0.0h;
                half streakCoverage = 0.0h;
                int sampleCount = clamp(_SampleCount, 4, 24);

                [loop]
                for (int sampleIndex = 0; sampleIndex < 24; sampleIndex++)
                {
                    if (sampleIndex >= sampleCount)
                        break;

                    float normalizedIndex = (float)sampleIndex / (float)(sampleCount - 1);
                    float centeredOffset = normalizedIndex - 0.5;
                    float2 sampleUv = uv + velocity * centeredOffset;
                    half sampleMask = EvaluateMask(SAMPLE_TEXTURE2D_X_LOD(
                        _BlitTexture, sampler_LinearClamp, sampleUv, 0).r);
                    half weight = 1.0h - abs((half)centeredOffset) * 0.65h;

                    accumulatedColor += SAMPLE_TEXTURE2D_X_LOD(
                        _SelectiveMotionSourceTexture,
                        sampler_LinearClamp,
                        sampleUv,
                        0).rgb * weight;
                    accumulatedWeight += weight;
                    streakCoverage = max(streakCoverage, sampleMask);
                }

                half3 blurredColor = accumulatedColor / max(accumulatedWeight, 0.0001h);
                half blend = saturate(streakCoverage * velocityData.z * _Intensity);
                return half4(lerp(source.rgb, blurredColor, blend), source.a);
            }
            ENDHLSL
        }
    }
}
