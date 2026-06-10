#ifndef OUTLINE_VERTEX_INCLUDED
#define OUTLINE_VERTEX_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "OutlineTypes.hlsl"

Interpolators Vert(MeshData v)
{
    Interpolators o;
    float3 normalOS = normalize(v.normalOS);
    float3 positionOS = v.positionOS.xyz + normalOS * _OutlineScale;
    
    o.positionCS = TransformObjectToHClip(positionOS);
    return o;
}

#endif