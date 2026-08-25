#ifndef VIRTUAL_DEPTH_LAYER_SAMPLING_INCLUDED
#define VIRTUAL_DEPTH_LAYER_SAMPLING_INCLUDED

#define VIRTUAL_DEPTH_MAX_LAYER_COUNT 20

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// _SpriteRect: xy = local minimum, zw = local size.
// _SpriteUVRect: xy = texture UV minimum, zw = texture UV maximum.
float4 _SpriteRect;
float4 _SpriteUVRect;
float _VirtualLayerCount;
float _VirtualLayerDepths[VIRTUAL_DEPTH_MAX_LAYER_COUNT];
float _VirtualLayerOpacities[VIRTUAL_DEPTH_MAX_LAYER_COUNT];

struct VirtualDepthViewRay
{
    float3 cameraOS;
    float3 directionOS;
};

int GetActiveVirtualDepthLayerCount()
{
    return clamp((int)_VirtualLayerCount, 1, VIRTUAL_DEPTH_MAX_LAYER_COUNT);
}

bool IsVirtualDepthLayerVisibleToCamera(float cameraZ, float layerDepth)
{
    return (layerDepth - cameraZ) * -cameraZ > 0.000001;
}

bool TryBuildVirtualDepthViewRay(float3 surfacePositionOS, out VirtualDepthViewRay viewRay)
{
    viewRay.cameraOS = TransformWorldToObject(_WorldSpaceCameraPos);
    viewRay.directionOS = surfacePositionOS - viewRay.cameraOS;
    return abs(viewRay.directionOS.z) >= 0.00001;
}

half SampleVirtualDepthLayerMask(VirtualDepthViewRay viewRay, float layerDepth, float2 layerOffsetPerDepth)
{
    if (!IsVirtualDepthLayerVisibleToCamera(viewRay.cameraOS.z, layerDepth))
        return 0.0h;

    float rayDistance = (layerDepth - viewRay.cameraOS.z) / viewRay.directionOS.z;
    float3 layerHitPositionOS = viewRay.cameraOS + viewRay.directionOS * rayDistance;

    // A positive offset moves the virtual mask by layerOffsetPerDepth * layerDepth.
    // Move the hit point in the opposite direction before sampling the source sprite.
    layerHitPositionOS.xy -= layerOffsetPerDepth * layerDepth;

    float2 normalizedSpriteUV = (layerHitPositionOS.xy - _SpriteRect.xy) / _SpriteRect.zw;
    if (any(normalizedSpriteUV < 0.0) || any(normalizedSpriteUV > 1.0))
        return 0.0h;

    float2 textureUV = lerp(_SpriteUVRect.xy, _SpriteUVRect.zw, normalizedSpriteUV);
    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, textureUV).a;
}

#endif
