Shader "Hidden/Outline/SinglePassEdge"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1,1,1,1)
        _OutlineIntensity("Outline Intensity", Float) = 1.0
        _OutlineWidth("Outline Width", Float) = 1.0
        _PixelSize("Pixel Size", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "EdgeComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_OutlineMask);
            SAMPLER(sampler_OutlineMask);

            half4 _OutlineColor;
            float _OutlineIntensity;
            float _OutlineWidth;
            float4 _PixelSize;

            half4 frag(Interpolators input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // Sample the base game camera texture background (automatically bound by Blitter)
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // Fix vertical UV flipping logic between textures
                float2 maskUV = uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_BlitTexture_TexelSize.y < 0)
                {
                    maskUV.y = 1.0 - maskUV.y;
                }
                #endif

                // Sample the original center point pixel from your globally registered silhouette mask
                half centerMask = SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, maskUV).r;

                // Check 4 diagonal neighbors around the mask pixel to find the boundaries
                float2 offsets[4] = {
                    float2(1, 1), float2(-1, -1),
                    float2(1, -1), float2(-1, 1)
                };

                half maxNeighborMask = 0.0;
                for (int i = 0; i < 4; i++)
                {
                    float2 sampleUV = maskUV + (offsets[i] * _PixelSize.xy * _OutlineWidth);
                    half neighborSample = SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, sampleUV).r;
                    maxNeighborMask = max(maxNeighborMask, neighborSample);
                }

                // If a neighbor is white but the center is black, we have found an outer edge pixel!
                half edge = saturate(maxNeighborMask - centerMask);

                // Blend the outline color cleanly on top of the underlying game view
                half4 edgeColor = _OutlineColor * _OutlineIntensity;
                half4 finalColor = lerp(sceneColor, edgeColor, edge * edgeColor.a);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
