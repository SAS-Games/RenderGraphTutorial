Shader "Custom/URPCustomTagMultiPass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }

        // ----------------------------------------------------
        // PASS 1: Normal Object Rendering (URP handles automatically)
        // ----------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct MeshData { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Interpolators { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Texture2D _MainTex; SamplerState sampler_MainTex; float4 _BaseColor;

            Interpolators vert(MeshData input)
            {
                Interpolators output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Interpolators input) : SV_Target
            {
                return _MainTex.Sample(sampler_MainTex, input.uv) * _BaseColor;
            }
            ENDHLSL
        }

        // ----------------------------------------------------
        // PASS 2: The Custom Outline (URP ignores until C# calls it)
        // ----------------------------------------------------
        Pass
        {
            Name "CustomOutlinePass"
            Tags { "LightMode" = "CustomOutlineTag" } // Your completely custom identifier
            
            Cull Front
            ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct MeshData { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Interpolators { float4 positionCS : SV_POSITION; };

            float4 _OutlineColor; float _OutlineWidth;

            Interpolators vert(MeshData input)
            {
                Interpolators output;
                float3 extrudedPositionOS = input.positionOS.xyz + (input.normalOS * _OutlineWidth);
                output.positionCS = TransformObjectToHClip(extrudedPositionOS);
                return output;
            }

            float4 frag(Interpolators input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
