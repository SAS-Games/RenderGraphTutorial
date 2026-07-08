Shader "Hidden/RenderTextureFeature/DebugTexture"
{
    Properties
    {
        _DebugColor("Debug Color", Color) = (0, 1, 0, 0.75)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Fullscreen"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).r;
                return half4(mask, mask, mask, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Overlay"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 _DebugColor;

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half mask = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).r;
                return half4(_DebugColor.rgb, mask * _DebugColor.a);
            }
            ENDHLSL
        }
    }
}
