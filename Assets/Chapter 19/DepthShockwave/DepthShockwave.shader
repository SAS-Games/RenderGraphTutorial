Shader "Hidden/Chapter19/DepthShockwave/Composite"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Composite"
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment CompositeFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define MAX_SHOCKWAVES 8

            TEXTURE2D_X(_ShockwaveSourceTexture);
            TEXTURE2D_X_FLOAT(_ShockwaveDepthTexture);

            float4 _ShockwaveCentersRadii[MAX_SHOCKWAVES];
            float4 _ShockwaveParameters[MAX_SHOCKWAVES];

            CBUFFER_START(UnityPerMaterial)
                float4 _SourceTexelSize;
                float4 _WaveColor;
                int _ShockwaveCount;
                float _RingWidth;
                float _EdgeSoftness;
                float _SecondaryRingOffset;
                float _SecondaryRingStrength;
                float _DistortionPixels;
                float _ChromaticPixels;
                float _EmissionIntensity;
                float _Intensity;
            CBUFFER_END

            float RingEnvelope(float signedDistance)
            {
                float halfWidth = max(_RingWidth * 0.5, 0.005);
                return 1.0 - smoothstep(
                    halfWidth,
                    halfWidth + max(_EdgeSoftness, 0.001),
                    abs(signedDistance));
            }

            float SignedRefraction(float signedDistance, float envelope)
            {
                float halfWidth = max(_RingWidth * 0.5, 0.005);
                float phase = clamp(signedDistance / halfWidth, -1.0, 1.0);
                return sin(phase * PI) * envelope;
            }

            bool IsSkyDepth(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth <= 0.00001;
                #else
                    return rawDepth >= 0.99999;
                #endif
            }

            float ReconstructDeviceDepth(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth;
                #else
                    return lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
            }

            half4 CompositeFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
                half4 source = SAMPLE_TEXTURE2D_X_LOD(
                    _ShockwaveSourceTexture,
                    sampler_LinearClamp,
                    uv,
                    0);
                float rawDepth = SAMPLE_TEXTURE2D_X_LOD(
                    _ShockwaveDepthTexture,
                    sampler_PointClamp,
                    uv,
                    0).r;

                if (IsSkyDepth(rawDepth))
                    return source;

                float deviceDepth = ReconstructDeviceDepth(rawDepth);
                float3 worldPosition = ComputeWorldSpacePosition(
                    uv,
                    deviceDepth,
                    UNITY_MATRIX_I_VP);

                float2 distortionDirection = 0.0;
                float refractionStrength = 0.0;
                float emissionStrength = 0.0;

                [unroll]
                for (int shockwaveIndex = 0; shockwaveIndex < MAX_SHOCKWAVES; shockwaveIndex++)
                {
                    if (shockwaveIndex >= _ShockwaveCount)
                        break;

                    float4 centerRadius = _ShockwaveCentersRadii[shockwaveIndex];
                    float eventFade = _ShockwaveParameters[shockwaveIndex].x;
                    float surfaceDistance = distance(worldPosition, centerRadius.xyz);
                    float signedDistance = surfaceDistance - centerRadius.w;
                    float primaryEnvelope = RingEnvelope(signedDistance) * eventFade;
                    float primaryRefraction = SignedRefraction(
                        signedDistance,
                        primaryEnvelope);

                    float secondaryEnvelope = 0.0;
                    float secondaryRefraction = 0.0;
                    float secondaryRadius = centerRadius.w - _SecondaryRingOffset;
                    if (secondaryRadius > 0.0 && _SecondaryRingStrength > 0.0)
                    {
                        float secondarySignedDistance = surfaceDistance - secondaryRadius;
                        secondaryEnvelope = RingEnvelope(secondarySignedDistance) *
                            eventFade * _SecondaryRingStrength;
                        secondaryRefraction = SignedRefraction(
                            secondarySignedDistance,
                            secondaryEnvelope);
                    }

                    float4 centerClip = TransformWorldToHClip(centerRadius.xyz);
                    float4 centerScreen = ComputeScreenPos(centerClip);
                    float2 centerUv = centerScreen.xy / max(centerScreen.w, 0.0001);
                    float2 fromCenter = uv - centerUv;
                    float inverseLength = rsqrt(max(dot(fromCenter, fromCenter), 1e-8));
                    float2 screenDirection = fromCenter * inverseLength;

                    float combinedRefraction = primaryRefraction + secondaryRefraction;
                    distortionDirection += screenDirection * combinedRefraction;
                    refractionStrength += abs(combinedRefraction);
                    emissionStrength = max(
                        emissionStrength,
                        primaryEnvelope + secondaryEnvelope);
                }

                float intensity = max(_Intensity, 0.0);
                float2 distortionUv = distortionDirection *
                    (_DistortionPixels * intensity) * _SourceTexelSize.xy;
                float2 distortedUv = saturate(uv + distortionUv);
                float2 chromaticDirection = distortionDirection *
                    rsqrt(max(dot(distortionDirection, distortionDirection), 1e-8));
                float2 chromaticUv = chromaticDirection *
                    (_ChromaticPixels * saturate(refractionStrength) * intensity) *
                    _SourceTexelSize.xy;

                half red = SAMPLE_TEXTURE2D_X_LOD(
                    _ShockwaveSourceTexture,
                    sampler_LinearClamp,
                    saturate(distortedUv + chromaticUv),
                    0).r;
                half green = SAMPLE_TEXTURE2D_X_LOD(
                    _ShockwaveSourceTexture,
                    sampler_LinearClamp,
                    distortedUv,
                    0).g;
                half blue = SAMPLE_TEXTURE2D_X_LOD(
                    _ShockwaveSourceTexture,
                    sampler_LinearClamp,
                    saturate(distortedUv - chromaticUv),
                    0).b;

                half3 refractedColor = half3(red, green, blue);
                half visibleWave = saturate((half)emissionStrength * (half)intensity);
                half3 outputColor = lerp(source.rgb, refractedColor, saturate(refractionStrength));
                outputColor += _WaveColor.rgb * visibleWave * _EmissionIntensity;
                return half4(outputColor, source.a);
            }
            ENDHLSL
        }
    }
}
