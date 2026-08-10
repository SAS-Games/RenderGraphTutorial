Shader "Hidden/RenderTextureFeature/Examples/FrameTextureProcessingOverlay"
{
    Properties
    {
        _OverlayColor("Overlay Color", Color) = (0.8, 0.2, 1, 0.85)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ProcessedMaskOverlay"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OverlayColor;
            CBUFFER_END

            TEXTURE2D_X(_FrameProcessingResult);

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half mask = SAMPLE_TEXTURE2D_X(
                    _FrameProcessingResult,
                    sampler_PointClamp,
                    input.texcoord).r;
                return half4(_OverlayColor.rgb, saturate(mask) * _OverlayColor.a);
            }
            ENDHLSL
        }
    }
}
