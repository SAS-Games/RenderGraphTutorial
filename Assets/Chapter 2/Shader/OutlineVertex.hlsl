#ifndef OUTLINE_VERTEX_INCLUDED
#define OUTLINE_VERTEX_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "OutlineTypes.hlsl"

Interpolators Vert(MeshData v)
{
    Interpolators o;
    float3 positionOS = v.positionOS.xyz * (1 + _OutlineScale);

    o.positionCS = TransformObjectToHClip(positionOS);
    return o;
}

#endif