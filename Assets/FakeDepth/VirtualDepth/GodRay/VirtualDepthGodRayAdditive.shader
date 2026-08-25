Shader "Custom/URP/Virtual Depth/God Ray Additive"
{
    Properties
    {
        [PerRendererData]
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _EffectColor ("Light Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0,5)) = 1.0
        _LayerOffsetPerDepth ("Layer Offset Per Depth", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "VirtualDepthGodRayAdditive"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha One

            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EffectColor;
                float4 _LayerOffsetPerDepth;
                float _Intensity;
            CBUFFER_END

            #include "Assets/FakeDepth/ShaderLibrary/VirtualDepthProxyProjection.hlsl"


            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 proxyPositionOS : TEXCOORD0;
                half4 vertexColor : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 proxyPositionOS = CalculateVirtualDepthProxyPositionOS(input.positionOS.xyz, _LayerOffsetPerDepth.xy);

                output.proxyPositionOS = proxyPositionOS;
                output.positionCS = TransformObjectToHClip(proxyPositionOS);
                output.vertexColor = input.color;
                return output;
            }


            half4 Frag(Varyings input) : SV_Target
            {
                VirtualDepthViewRay viewRay;
                if (!TryBuildVirtualDepthViewRay(input.proxyPositionOS, viewRay))
                    discard;

                half accumulatedLight = 0.0h;
                int activeLayerCount = GetActiveVirtualDepthLayerCount();

                [unroll]
                for (int layerIndex = 0; layerIndex < VIRTUAL_DEPTH_MAX_LAYER_COUNT; ++layerIndex)
                {
                    if (layerIndex >= activeLayerCount)
                        break;

                    float layerDepth = _VirtualLayerDepths[layerIndex];
                    half layerOpacity = _VirtualLayerOpacities[layerIndex];

                    if (layerOpacity <= 0.0001h)
                        continue;

                    half layerMask = SampleVirtualDepthLayerMask(viewRay, layerDepth, _LayerOffsetPerDepth.xy);
                    if (layerMask <= 0.001h)
                        continue;

                    half layerLight = layerMask * layerOpacity;

                    // Add light contributed by this virtual depth layer.
                    accumulatedLight += layerLight;

                    // Once the accumulated light is saturated there is no reason to sample deeper layers.
                    if (accumulatedLight >= 1.0h)
                    {
                        accumulatedLight = 1.0h;
                        break;
                    }
                }

                accumulatedLight *= (half)_Intensity;
                accumulatedLight *= (half)_EffectColor.a;
                accumulatedLight *= input.vertexColor.a;

                accumulatedLight = saturate(accumulatedLight);

                if (accumulatedLight <= 0.001h)
                    discard;

                return half4((half3)_EffectColor.rgb, accumulatedLight);
            }

            ENDHLSL
        }
    }
}
