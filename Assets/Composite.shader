Shader "Hidden/Outline/Composite"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1,1,1,1)
        _OutlineIntensity("Outline Intensity", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_OutlineMask); 
            TEXTURE2D(_OutlineDilated);
            SAMPLER(sampler_OutlineMask);
            SAMPLER(sampler_OutlineDilated);

            half4 _OutlineColor;
            float _OutlineIntensity;

            half4 frag(Interpolators input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Use standard blit UVs for the camera texture background
                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // FIX: Account for potential platform UV coordinate flipping on target buffers
                float2 maskUV = uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_BlitTexture_TexelSize.y < 0)
                {
                    maskUV.y = 1.0 - maskUV.y;
                }
                #endif

                // Sample your custom generated outlines with the corrected UV layout
                half baseMask = SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, maskUV).r;
                half dilatedMask = SAMPLE_TEXTURE2D(_OutlineDilated, sampler_OutlineDilated, maskUV).r;

                // Subtracting the original silhouette leaves ONLY the external expanded border edge
                half edge = saturate(dilatedMask - baseMask);

                // Apply outline color alpha blending manually over the screen context
                half4 edgeColor = _OutlineColor * _OutlineIntensity;
                
                // If edge is 0, this outputs sceneColor unmodified. If edge is 1, it blends the outline.
                half4 finalColor = lerp(sceneColor, edgeColor, edge * edgeColor.a);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
