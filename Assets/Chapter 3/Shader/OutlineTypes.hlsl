#ifndef OUTLINE_TYPES_INCLUDED
#define OUTLINE_TYPES_INCLUDED

float _OutlineScale;
float4 _OutlineColor;

struct MeshData
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct Interpolators
{
    float4 positionCS : SV_POSITION;
};

#endif