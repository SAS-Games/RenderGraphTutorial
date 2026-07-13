Shader "Hidden/RenderTextureFeature/TextureExposureProof/GlobalTexture"
{
    Properties
    {
        _ProofColor("Proof Color", Color) = (0, 1, 0.2, 0.65)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "GlobalTextureProof"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_TextureExposureProofMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _ProofColor;
            CBUFFER_END

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half mask = SAMPLE_TEXTURE2D_X(
                    _TextureExposureProofMask,
                    sampler_PointClamp,
                    input.texcoord).r;

                return half4(_ProofColor.rgb, saturate(mask) * _ProofColor.a);
            }
            ENDHLSL
        }
    }
}
