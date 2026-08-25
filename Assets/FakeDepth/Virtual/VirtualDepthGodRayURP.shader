Shader "Custom/VirtualDepthGodRayURP"
{
    Properties
    {
        [PerRendererData]
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _EffectColor ("Light Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0,5)) = 1.0
        _LightDirection ("Light Direction", Vector) = (0,0,0,0)
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
            Name "VirtualDepthGodRay"

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

            #define MAX_DEPTH_SAMPLES 20

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);


            CBUFFER_START(UnityPerMaterial)
                float4 _EffectColor;
                float4 _LightDirection;
                float _Intensity;
            CBUFFER_END


            // x = local min X
            // y = local min Y
            // z = local width
            // w = local height
            // xy = texture UV minimum
            // zw = texture UV maximum

            float4 _SpriteRect;
            float4 _UVRect;
            float _SliceCount; // Number of active virtual slices.
            float _VirtualDepths[MAX_DEPTH_SAMPLES];
            float _VirtualAlphas[MAX_DEPTH_SAMPLES];

            #include "Assets/FakeDepth/Virtual/VirtualDepthRaster.hlsl"


            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };


            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                half4 vertexColor : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 rasterPositionOS = BuildVirtualDepthRasterPositionOS(input.positionOS.xyz, _LightDirection.xy);

                output.positionOS = rasterPositionOS;
                output.positionCS = TransformObjectToHClip(rasterPositionOS);
                output.vertexColor = input.color;
                return output;
            }


            half4 Frag(Varyings input) : SV_Target
            {
                float3 cameraOS = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 surfacePointOS = input.positionOS;

                float3 rayDirectionOS = surfacePointOS - cameraOS;
                float rayZ = rayDirectionOS.z;

                if (abs(rayZ) < 0.00001)
                    discard;


                half accumulatedLight = 0.0h;
                int sliceCount = clamp((int)_SliceCount, 1, MAX_DEPTH_SAMPLES);


                [unroll]
                for (int sampleIndex = 0; sampleIndex < MAX_DEPTH_SAMPLES; ++sampleIndex)
                {
                    if (sampleIndex >= sliceCount)
                        break;


                    float virtualZ = _VirtualDepths[sampleIndex];
                    half layerWeight = _VirtualAlphas[sampleIndex];


                    // If this layer contributes nothing, skip all remaining work for this sample.
                    if (layerWeight <= 0.0001h)
                        continue;


                    // A plane that crossed behind the camera cannot contribute to this view.
                    if ((virtualZ - cameraOS.z) * -cameraOS.z <= 0.000001)
                        continue;


                    // Find where the camera-to-fragment ray intersects the virtual sprite plane located at 'virtualZ'.
                    // rayT tells us how far along the ray we must travel from the camera to reach that Z depth.
                    // How far along the camera ray do I travel before reaching to depth Z
                    float rayT = (virtualZ - cameraOS.z) / rayZ;


                    // Move from the camera along the view ray by rayT. The resulting position is the point on the virtual
                    // sprite that would be visible through this screen pixel.
                    // What is the exact XYZ position at that intersection?
                    float3 hitPositionOS = cameraOS + rayDirectionOS * rayT;


                    // Move the virtual mask through X/Y as depth increases.
                    // This creates the directional slant normally associated with light shafts.
                    hitPositionOS.xy -= _LightDirection.xy * virtualZ;


                    // Convert local XY position into normalized 0-1 sprite coordinates.
                    float2 spriteUV = (hitPositionOS.xy - _SpriteRect.xy) / _SpriteRect.zw;


                    // Virtual ray hit lies outside the sprite.
                    if (spriteUV.x < 0.0 || spriteUV.x > 1.0 || spriteUV.y < 0.0 || spriteUV.y > 1.0)
                        continue;


                    // Convert normalized sprite coordinates into the real texture / atlas UV.
                    float2 textureUV = lerp(_UVRect.xy, _UVRect.zw, spriteUV);


                    // Sample the virtual light mask.
                    // Alpha represents how much light this virtual slice contributes.
                    half spriteAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, textureUV).a;

                    if (spriteAlpha <= 0.001h)
                        continue;


                    // Apply artist-authored intensity by depth.
                    // This has already been evaluated by C#.
                    half layerLight = spriteAlpha * layerWeight;


                    // Add light contributed by this virtual depth slice.
                    accumulatedLight += layerLight;


                    // Once the accumulated light is saturated there is no reason to sample deeper slices.
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
