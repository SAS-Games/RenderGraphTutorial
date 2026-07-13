Shader "Hidden/RenderTextureFeature/TextureExposureProof/GlobalTextureAndTexelSize"
{
    Properties
    {
        _ProofColor("Proof Color", Color) = (1, 0.85, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "GlobalTextureAndTexelSizeProof"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_TextureExposureProofMask);
            float4 _TextureExposureProofMask_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _ProofColor;
            CBUFFER_END

            half SampleMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(
                    _TextureExposureProofMask,
                    sampler_PointClamp,
                    uv).r;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texel = _TextureExposureProofMask_TexelSize.xy;
                if (texel.x <= 0.0 || texel.y <= 0.0)
                {
                    return half4(1.0h, 0.0h, 1.0h, 1.0h);
                }

                half center = SampleMask(uv);
                half maximum = center;
                half minimum = center;

                half sampleValue = SampleMask(uv + float2(texel.x, 0.0));
                maximum = max(maximum, sampleValue);
                minimum = min(minimum, sampleValue);

                sampleValue = SampleMask(uv - float2(texel.x, 0.0));
                maximum = max(maximum, sampleValue);
                minimum = min(minimum, sampleValue);

                sampleValue = SampleMask(uv + float2(0.0, texel.y));
                maximum = max(maximum, sampleValue);
                minimum = min(minimum, sampleValue);

                sampleValue = SampleMask(uv - float2(0.0, texel.y));
                maximum = max(maximum, sampleValue);
                minimum = min(minimum, sampleValue);

                half edge = saturate(maximum - minimum);
                return half4(_ProofColor.rgb, edge * _ProofColor.a);
            }
            ENDHLSL
        }
    }
}
