#ifndef URP_MULTI_PASS_BASE_TYPES_INCLUDED
#define URP_MULTI_PASS_BASE_TYPES_INCLUDED

Texture2D _MainTex;
SamplerState sampler_MainTex;

float4 _BaseColor;

struct BaseMeshData
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
};

struct BaseInterpolators
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

#endif
