Shader "Custom/URPMultiPassOutline"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0.05, 0.5)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }

        // PASS 1: The Main Object (Standard Forward Lit)
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" } // URP automatically runs this during Opaque phase [1]

            HLSLPROGRAM
            #pragma vertex BaseVert
            #pragma fragment BaseFrag
            #include "URPMultiPassBaseVertex.hlsl"
            #include "URPMultiPassBaseFragment.hlsl"
            ENDHLSL
        }

       
        // PASS 2: The Outline Shell
        Pass
        {
            Name "Outline"
            // Using SRPDefaultUnlit makes URP render this pass right after the main opaque loops [1]
            Tags { "LightMode" = "SRPDefaultUnlit" } 
            
            Cull Front // Render only the inside back-faces [1]

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #include "URPMultiPassOutlineVertex.hlsl"
            #include "URPMultiPassOutlineFragment.hlsl"
            ENDHLSL
        }
    }
}
