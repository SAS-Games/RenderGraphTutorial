#ifndef URP_MULTI_PASS_BASE_VERTEX_INCLUDED
#define URP_MULTI_PASS_BASE_VERTEX_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "URPMultiPassBaseTypes.hlsl"

BaseInterpolators BaseVert(BaseMeshData input)
{
    BaseInterpolators output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = input.uv;
    return output;
}

#endif
