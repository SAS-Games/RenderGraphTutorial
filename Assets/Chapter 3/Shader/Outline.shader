Shader "Chapter3/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0,1,0,0)
        _OutlineScale ("Outline Width", Range(0,0.5)) = 0.05
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Name "Per Object Outline"
            Cull Front
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "OutlineVertex.hlsl"
            #include "OutlineFragment.hlsl"
            ENDHLSL
        }
    }
}