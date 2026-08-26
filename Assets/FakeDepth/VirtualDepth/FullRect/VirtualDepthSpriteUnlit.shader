Shader "Custom/URP/Virtual Depth/Sprite Unlit"
{
    Properties
    {
        [PerRendererData]
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _EffectColor ("Effect Color", Color) = (0,0,0,1)
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
            Name "VirtualDepthSpriteUnlit"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha

            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/FakeDepth/ShaderLibrary/VirtualDepthProxyProjection.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EffectColor;
            CBUFFER_END


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

                float3 proxyPositionOS = CalculateVirtualDepthProxyPositionOS(input.positionOS.xyz, float2(0.0, 0.0));

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

                half accumulatedAlpha = 0.0h;
                int activeLayerCount = GetActiveVirtualDepthLayerCount();

                [unroll]
                for (int layerIndex = 0; layerIndex < VIRTUAL_DEPTH_MAX_LAYER_COUNT; ++layerIndex)
                {
                    if (layerIndex >= activeLayerCount)
                        break;

                    float layerDepth = _VirtualLayerDepths[layerIndex];
                    half layerOpacity = (half)_VirtualLayerOpacities[layerIndex];

                    if (layerOpacity <= 0.0001h)
                        continue;

                    half layerMask = SampleVirtualDepthLayerMask(viewRay, layerDepth, float2(0.0, 0.0));
                    if (layerMask <= 0.001h)
                        continue;

                    half layerAlpha = layerMask * layerOpacity;

                    // Combine virtual layers. Equivalent to compositing many copies of the SAME colored transparent sprite.
                    // A = 1 - Product(1 - Ai)
                    accumulatedAlpha = 1.0h - (1.0h - accumulatedAlpha) * (1.0h - layerAlpha);

                    // Nothing beyond here can visibly change an already opaque pixel.
                    if (accumulatedAlpha >= 0.995h)
                        break;
                }

                accumulatedAlpha *= (half)_EffectColor.a;
                accumulatedAlpha *= input.vertexColor.a;

                if (accumulatedAlpha <= 0.001h)
                    discard;

                return half4((half3)_EffectColor.rgb, accumulatedAlpha);
            }
            ENDHLSL
        }
    }
}
