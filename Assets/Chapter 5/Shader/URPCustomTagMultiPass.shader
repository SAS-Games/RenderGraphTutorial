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
            #pragma vertex BaseVert
            #pragma fragment BaseFrag
            #include "../../Chapter 4/Shader/URPMultiPassBaseVertex.hlsl"
            #include "../../Chapter 4/Shader/URPMultiPassBaseFragment.hlsl"
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
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #include "../../Chapter 4/Shader/URPMultiPassOutlineVertex.hlsl"
            #include "../../Chapter 4/Shader/URPMultiPassOutlineFragment.hlsl"
            ENDHLSL
        }
    }
}
