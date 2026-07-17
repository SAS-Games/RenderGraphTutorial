#ifndef URP_MULTI_PASS_OUTLINE_TYPES_INCLUDED
#define URP_MULTI_PASS_OUTLINE_TYPES_INCLUDED

float4 _OutlineColor;
float _OutlineWidth;

struct OutlineMeshData
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct OutlineInterpolators
{
    float4 positionCS : SV_POSITION;
};

#endif
