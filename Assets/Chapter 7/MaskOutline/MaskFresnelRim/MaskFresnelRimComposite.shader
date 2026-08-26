Shader "Hidden/RenderTextureFeature/MaskFresnelRim/Composite"
{
    Properties
    {
        _RimColor("Rim Color", Color) = (1, 0.85, 0.75, 1)
        _RimPower("Rim Power", Range(0.25, 8)) = 3
        _RimThreshold("Rim Threshold", Range(0, 1)) = 0.4
        _RimSoftness("Rim Softness", Range(0.001, 0.5)) = 0.12
        _RimIntensity("Rim Intensity", Range(0, 10)) = 1.5
        _MaskThreshold("Mask Threshold", Range(0, 1)) = 0.5
        _MaskEdgeSoftness("Mask Edge Softness", Range(0.001, 0.25)) = 0.1
        _NormalSmoothing("Normal Smoothing", Range(0, 2)) = 0.75
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

        Pass
        {
            Name "MaskFresnelRim"
            Blend One One
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FresnelFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Assets/Chapter 7/MaskOutline/Shader/MorphologyUtils.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                float4 _MaskTexelSize;
                float _RimPower;
                float _RimThreshold;
                float _RimSoftness;
                float _RimIntensity;
                float _MaskThreshold;
                float _MaskEdgeSoftness;
                float _NormalSmoothing;
            CBUFFER_END

            half SampleSelectionMask(float2 uv)
            {
                half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)).r;
                return ApplyMaskThreshold(mask, _MaskThreshold, _MaskEdgeSoftness);
            }

            void AccumulateNormal(inout float3 normalSum, inout half weightSum, float2 uv, float2 offset,
                                  half kernelWeight)
            {
                float2 sampleUv = saturate(uv + offset);
                half selection = SampleSelectionMask(sampleUv);
                half weight = selection * kernelWeight;
                normalSum += SampleSceneNormals(sampleUv, sampler_LinearClamp) * weight;
                weightSum += weight;
            }

            half4 FresnelFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half selection = SampleSelectionMask(uv);
                if (selection <= 0.0001h)
                    return 0.0h;

                float3 normalSum = SampleSceneNormals(uv, sampler_LinearClamp) * (selection * 4.0h);
                half normalWeight = selection * 4.0h;

                if (_NormalSmoothing > 0.001)
                {
                    float2 texel = _MaskTexelSize.xy * _NormalSmoothing;
                    AccumulateNormal(normalSum, normalWeight, uv, float2(texel.x, 0.0), 1.0h);
                    AccumulateNormal(normalSum, normalWeight, uv, float2(-texel.x, 0.0), 1.0h);
                    AccumulateNormal(normalSum, normalWeight, uv, float2(0.0, texel.y), 1.0h);
                    AccumulateNormal(normalSum, normalWeight, uv, float2(0.0, -texel.y), 1.0h);
                    AccumulateNormal(normalSum, normalWeight, uv, texel, 0.5h);
                    AccumulateNormal(normalSum, normalWeight, uv, -texel, 0.5h);
                    AccumulateNormal(normalSum, normalWeight, uv, float2(texel.x, -texel.y), 0.5h);
                    AccumulateNormal(normalSum, normalWeight, uv, float2(-texel.x, texel.y), 0.5h);
                }

                float normalLengthSquared = dot(normalSum, normalSum);
                if (normalWeight <= 0.0001h || normalLengthSquared <= 0.000001)
                    return 0.0h;

                float3 normalWS = normalSum * rsqrt(normalLengthSquared);
                float rawDepth = SampleSceneDepth(uv, sampler_PointClamp);

                #if UNITY_REVERSED_Z
                float deviceDepth = rawDepth;
                #else
                    float deviceDepth = lerp(
                        UNITY_NEAR_CLIP_VALUE,
                        1.0,
                        rawDepth
                    );
                #endif

                float3 positionWS = ComputeWorldSpacePosition(uv,deviceDepth,UNITY_MATRIX_I_VP);
                float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(positionWS));

                float normalDotView = saturate(dot(normalWS, viewDirectionWS));
                float fresnel = pow(saturate(1.0 - normalDotView),_RimPower);
                float antialiasing = max(fwidth(fresnel), 0.0001);
                float softness = max(_RimSoftness, antialiasing);
                half rim = smoothstep(_RimThreshold - softness,_RimThreshold + softness,fresnel);

                half strength = selection * rim *_RimColor.a * _RimIntensity;
                return half4(_RimColor.rgb * strength, 0.0h);
            }
            ENDHLSL
        }
    }
}