Shader "Chapter20/DamageFreezeTimeEcho"
{
    Properties
    {
        [HDR] _EchoTint ("Echo Tint", Color) = (0.06, 0.55, 1.6, 1)
        [HDR] _EdgeColor ("Dissolve Edge", Color) = (0.4, 1.4, 3, 1)
        _EdgeIntensity ("Edge Intensity", Range(0, 8)) = 2.5
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0.001, 0.3)) = 0.08
        _NoiseScale ("Noise Scale", Range(0.1, 20)) = 4.5
        _DistortionStrength ("Distortion Strength", Range(0, 0.5)) = 0.08
        _DistortionFrequency ("Distortion Frequency", Range(0.1, 20)) = 5
        _VerticalDrift ("Vertical Drift", Range(-2, 2)) = 0.35
        _SurfaceOffset ("Surface Offset", Range(0, 0.1)) = 0.015
        [HideInInspector] _EchoProgress ("Echo Progress", Range(0, 1)) = 0
        [HideInInspector] _EchoLifetimeProgress ("Lifetime Progress", Range(0, 1)) = 0
        [HideInInspector] _EchoSeed ("Echo Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest+20"
        }

        Pass
        {
            Name "TimeEcho"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex EchoVertex
            #pragma fragment EchoFragment
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EchoTint;
                float4 _EdgeColor;
                float _EdgeIntensity;
                float _DissolveEdgeWidth;
                float _NoiseScale;
                float _DistortionStrength;
                float _DistortionFrequency;
                float _VerticalDrift;
                float _SurfaceOffset;
                float _EchoProgress;
                float _EchoLifetimeProgress;
                float _EchoSeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float ValueNoise(float3 position)
            {
                float3 cell = floor(position);
                float3 fraction = frac(position);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);

                float n000 = Hash31(cell + float3(0, 0, 0));
                float n100 = Hash31(cell + float3(1, 0, 0));
                float n010 = Hash31(cell + float3(0, 1, 0));
                float n110 = Hash31(cell + float3(1, 1, 0));
                float n001 = Hash31(cell + float3(0, 0, 1));
                float n101 = Hash31(cell + float3(1, 0, 1));
                float n011 = Hash31(cell + float3(0, 1, 1));
                float n111 = Hash31(cell + float3(1, 1, 1));

                float lower = lerp(
                    lerp(n000, n100, fraction.x),
                    lerp(n010, n110, fraction.x),
                    fraction.y);
                float upper = lerp(
                    lerp(n001, n101, fraction.x),
                    lerp(n011, n111, fraction.x),
                    fraction.y);
                return lerp(lower, upper, fraction.z);
            }

            Varyings EchoVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float distortionWave = sin(
                    dot(positionWS, float3(1.7, 2.3, 1.1)) * _DistortionFrequency +
                    _EchoSeed * 6.28318 +
                    _EchoLifetimeProgress * 8.0);
                float distortion = distortionWave * _DistortionStrength *
                    smoothstep(0.05, 1.0, _EchoProgress);

                positionWS += normalWS * (_SurfaceOffset + distortion);
                positionWS.y += _VerticalDrift * _EchoLifetimeProgress;

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 EchoFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 noisePosition = input.positionWS * _NoiseScale + _EchoSeed * 17.17;
                float noise = ValueNoise(noisePosition);
                float edgeWidth = max(_DissolveEdgeWidth, 0.001);
                float threshold = lerp(-edgeWidth, 1.0 + edgeWidth, _EchoProgress);
                float remaining = noise - threshold;
                clip(remaining);

                half edge = 1.0h - smoothstep(0.0h, (half)edgeWidth, (half)remaining);
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirectionWS)), 2.0h);
                half pulse = 0.9h + 0.1h * sin(
                    (half)(_EchoLifetimeProgress * 18.0 + _EchoSeed * 11.0));

                half3 color = _EchoTint.rgb * (0.5h + 0.5h * fresnel) * pulse;
                color += _EdgeColor.rgb * edge * _EdgeIntensity;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
