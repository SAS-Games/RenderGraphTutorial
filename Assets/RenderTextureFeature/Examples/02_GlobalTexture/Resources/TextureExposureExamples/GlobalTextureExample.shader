Shader "Hidden/RenderTextureFeature/Examples/GlobalTexture"
{
    Properties
    {
        _OverlayColor("Overlay Color", Color) = (1, 0.25, 0.15, 0.75)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "GlobalTextureOverlay"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_GlobalTextureExampleTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _OverlayColor;
            CBUFFER_END

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half mask = SAMPLE_TEXTURE2D_X(_GlobalTextureExampleTexture,sampler_PointClamp, input.texcoord).r;
                return half4(_OverlayColor.rgb, saturate(mask) * _OverlayColor.a);
            }
            ENDHLSL
        }
    }
}

