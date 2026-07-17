#ifndef URP_MULTI_PASS_OUTLINE_FRAGMENT_INCLUDED
#define URP_MULTI_PASS_OUTLINE_FRAGMENT_INCLUDED

#include "URPMultiPassOutlineTypes.hlsl"

float4 OutlineFrag(OutlineInterpolators input) : SV_Target
{
    return _OutlineColor;
}

#endif
