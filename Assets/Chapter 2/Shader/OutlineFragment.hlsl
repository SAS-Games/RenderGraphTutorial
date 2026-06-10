#ifndef OUTLINE_FRAGMENT_INCLUDED
#define OUTLINE_FRAGMENT_INCLUDED

#include "OutlineTypes.hlsl"

half4 Frag(Interpolators i) : SV_Target
{
    return _OutlineColor;
}

#endif