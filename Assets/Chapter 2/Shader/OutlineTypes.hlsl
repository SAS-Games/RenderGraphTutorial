#ifndef OUTLINE_TYPES_INCLUDED
#define OUTLINE_TYPES_INCLUDED

float _OutlineScale;
float4 _OutlineColor;

struct MeshData
{
    float4 positionOS : POSITION;
};

struct Interpolators
{
    float4 positionCS : SV_POSITION;
};

#endif