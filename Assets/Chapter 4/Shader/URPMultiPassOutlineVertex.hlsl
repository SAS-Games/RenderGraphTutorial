#ifndef URP_MULTI_PASS_OUTLINE_VERTEX_INCLUDED
#define URP_MULTI_PASS_OUTLINE_VERTEX_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "URPMultiPassOutlineTypes.hlsl"

OutlineInterpolators OutlineVert(OutlineMeshData input)
{
    OutlineInterpolators output;
    float3 extrudedPositionOS = input.positionOS.xyz + input.normalOS * _OutlineWidth;
    output.positionCS = TransformObjectToHClip(extrudedPositionOS);
    return output;
}

#endif
