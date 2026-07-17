#ifndef URP_MULTI_PASS_BASE_FRAGMENT_INCLUDED
#define URP_MULTI_PASS_BASE_FRAGMENT_INCLUDED

#include "URPMultiPassBaseTypes.hlsl"

float4 BaseFrag(BaseInterpolators input) : SV_Target
{
    float4 texColor = _MainTex.Sample(sampler_MainTex, input.uv);
    return texColor * _BaseColor;
}

#endif
