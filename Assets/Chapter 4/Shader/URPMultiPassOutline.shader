Shader "Custom/URPMultiPassOutline"
{
    Properties
    {
        // --- Base Material Properties ---
        _MainTex ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        // --- Outline Properties ---
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }

        // ==========================================
        // PASS 1: The Main Object (Standard Forward Lit)
        // ==========================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" } // URP automatically runs this during Opaque phase [1]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct MeshData
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Interpolators
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            float4 _BaseColor;

            Interpolators vert(MeshData input)
            {
                Interpolators output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Interpolators input) : SV_Target
            {
                float4 texColor = _MainTex.Sample(sampler_MainTex, input.uv);
                return texColor * _BaseColor;
            }
            ENDHLSL
        }

        // ==========================================
        // PASS 2: The Outline Shell
        // ==========================================
        Pass
        {
            Name "Outline"
            // Using SRPDefaultUnlit makes URP render this pass right after the main opaque loops [1]
            Tags { "LightMode" = "SRPDefaultUnlit" } 
            
            Cull Front // Render only the inside back-faces [1]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct MeshData
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Interpolators
            {
                float4 positionCS   : SV_POSITION;
            };

            float4 _OutlineColor;
            float _OutlineWidth;

            Interpolators vert(MeshData input)
            {
                Interpolators output;
                // Extrude vertices along their normals to create the outer shell [1]
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