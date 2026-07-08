Shader "Hidden/RenderTextureFeature/MaskOutline/Composite"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.82, 0, 1)
        _OutlineWidth("Outline Width", Range(1, 16)) = 3
        _OutlineIntensity("Outline Intensity", Range(0, 5)) = 1
        _MaskThreshold("Mask Threshold", Range(0, 1)) = 0.5
        _OutsideOnly("Outside Only", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "MaskOutline"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define MAX_OUTLINE_RADIUS 16

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineIntensity;
                float4 _MaskTexelSize;
                float _MaskThreshold;
                float _OutsideOnly;
            CBUFFER_END

            half SampleMask(float2 uv)
            {
                half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
                return step(_MaskThreshold, mask);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texelSize = _MaskTexelSize.xy;
                float radius = clamp(_OutlineWidth, 1.0, 16.0);
                float radiusSquared = radius * radius;
                int sampleRadius = min((int)ceil(radius), MAX_OUTLINE_RADIUS);

                half center = SampleMask(uv);
                half expanded = center;

                [loop]
                for (int y = -sampleRadius; y <= sampleRadius; y++)
                {
                    [loop]
                    for (int x = -sampleRadius; x <= sampleRadius; x++)
                    {
                        float2 offset = float2(x, y);
                        if (dot(offset, offset) > radiusSquared)
                        {
                            continue;
                        }

                        expanded = max(expanded, SampleMask(uv + offset * texelSize));
                    }
                }

                half outsideEdge = saturate(expanded - center);
                half edge = lerp(expanded, outsideEdge, step(0.5, _OutsideOnly));
                half alpha = saturate(edge * _OutlineColor.a * _OutlineIntensity);

                return half4(_OutlineColor.rgb * _OutlineIntensity, alpha);
            }
            ENDHLSL
        }
    }
}
