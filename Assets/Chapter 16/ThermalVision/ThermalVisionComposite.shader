Shader "Hidden/Chapter16/ThermalVision/Composite"
{
    Properties
    {
        _CoolShadow("Cool Shadow", Color) = (0.005, 0.01, 0.055, 1)
        _CoolMid("Cool Mid", Color) = (0.015, 0.12, 0.32, 1)
        _CoolHighlight("Cool Highlight", Color) = (0.08, 0.65, 0.8, 1)
        _ColdHeat("Cold Heat", Color) = (0.2, 0, 0.35, 1)
        _WarmHeat("Warm Heat", Color) = (1, 0.05, 0, 1)
        _HotHeat("Hot Heat", Color) = (1.5, 0.55, 0, 1)
        _CoreHeat("Core Heat", Color) = (2.2, 2, 1.2, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ThermalComposite"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_ThermalSourceTexture);
            TEXTURE2D_X(_ThermalThroughWallMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _SourceTexelSize;
                float4 _CoolShadow;
                float4 _CoolMid;
                float4 _CoolHighlight;
                float4 _ColdHeat;
                float4 _WarmHeat;
                float4 _HotHeat;
                float4 _CoreHeat;
                float _EnvironmentContrast;
                float _MaskThreshold;
                float _MaskSoftness;
                float _SurfaceDetail;
                float _EdgeIntensity;
                float _NoiseStrength;
                float _ScanlineStrength;
                float _ScanlineFrequency;
                float _Opacity;
                float _Activation;
                float _ThroughWalls;
                float _TimeOffset;
            CBUFFER_END

            half SampleSelectedMask(float2 uv)
            {
                half visible = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
                half throughWall = SAMPLE_TEXTURE2D_X(_ThermalThroughWallMask, sampler_LinearClamp, uv).r;
                return lerp(visible, throughWall, saturate(_ThroughWalls));
            }

            half3 EvaluateCoolPalette(half value)
            {
                half lower = saturate(value * 2.0h);
                half upper = saturate((value - 0.5h) * 2.0h);
                return lerp(lerp(_CoolShadow.rgb, _CoolMid.rgb, lower), _CoolHighlight.rgb, upper);
            }

            half3 EvaluateHeatPalette(half value)
            {
                half first = saturate(value / 0.45h);
                half second = saturate((value - 0.45h) / 0.3h);
                half third = saturate((value - 0.75h) / 0.25h);
                half3 heat = lerp(_ColdHeat.rgb, _WarmHeat.rgb, first);
                heat = lerp(heat, _HotHeat.rgb, second);
                return lerp(heat, _CoreHeat.rgb, third);
            }

            half InterleavedNoise(float2 pixel)
            {
                return frac(52.9829189h * frac(dot(pixel, half2(0.06711056h, 0.00583715h))));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(_ThermalSourceTexture, sampler_LinearClamp, uv);
                half sourceLuminance = dot(source.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half coolValue = saturate((sourceLuminance - 0.5h) * _EnvironmentContrast + 0.5h);
                half3 thermalColor = EvaluateCoolPalette(coolValue);

                half mask = SampleSelectedMask(uv);
                half softness = max(_MaskSoftness, 0.0001h);
                half coverage = smoothstep(_MaskThreshold, _MaskThreshold + softness, mask);

                half heatSignal = saturate(mask + sourceLuminance * _SurfaceDetail);
                half pulse = sin((_TimeOffset * 1.7h + uv.y * 3.0h) * 6.2831853h) * 0.025h;
                heatSignal = saturate(heatSignal + pulse * coverage);

                float2 edgeTexel = _SourceTexelSize.xy * 1.5;
                half neighbor = SampleSelectedMask(uv + float2(edgeTexel.x, 0.0));
                neighbor = max(neighbor, SampleSelectedMask(uv - float2(edgeTexel.x, 0.0)));
                neighbor = max(neighbor, SampleSelectedMask(uv + float2(0.0, edgeTexel.y)));
                neighbor = max(neighbor, SampleSelectedMask(uv - float2(0.0, edgeTexel.y)));
                half neighborCoverage = smoothstep(_MaskThreshold, _MaskThreshold + softness, neighbor);
                half outerEdge = saturate(neighborCoverage - coverage);

                half3 heatColor = EvaluateHeatPalette(heatSignal);
                thermalColor = lerp(thermalColor, heatColor, coverage);
                thermalColor += _CoreHeat.rgb * outerEdge * _EdgeIntensity;

                half noise = (InterleavedNoise(uv * _SourceTexelSize.zw + _TimeOffset * 37.0h) - 0.5h) * _NoiseStrength;
                half scanline = sin((uv.y * _ScanlineFrequency + _TimeOffset * 4.0h) * 6.2831853h) * _ScanlineStrength;
                thermalColor *= 1.0h + noise - abs(scanline);

                half blend = saturate(_Opacity * _Activation);
                return half4(lerp(source.rgb, thermalColor, blend), source.a);
            }
            ENDHLSL
        }
    }
}
