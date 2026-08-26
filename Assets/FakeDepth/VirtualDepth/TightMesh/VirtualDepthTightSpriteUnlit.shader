Shader "Custom/URP/Virtual Depth/Tight Mesh/Sprite Unlit"
{
    Properties
    {
        [PerRendererData]
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _EffectColor ("Effect Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VirtualDepthTightSpriteUnlit"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EffectColor;
            CBUFFER_END

            #include "Assets/FakeDepth/ShaderLibrary/VirtualDepthTightProxyProjection.hlsl"
            #define CALCULATE_VIRTUAL_DEPTH_PROXY_POSITION(input) CalculateVirtualDepthTightProxyPositionOS(input.uv.x, float2(0.0, 0.0))
            #include "Assets/FakeDepth/ShaderLibrary/VirtualDepthSpriteUnlitPass.hlsl"
            ENDHLSL
        }
    }
}