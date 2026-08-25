#ifndef VIRTUAL_DEPTH_PROXY_PROJECTION_INCLUDED
#define VIRTUAL_DEPTH_PROXY_PROJECTION_INCLUDED

#include "Assets/FakeDepth/ShaderLibrary/VirtualDepthLayerSampling.hlsl"

float3 CalculateVirtualDepthProxyPositionOS(float3 sourcePositionOS, float2 layerOffsetPerDepth)
{
    float3 cameraOS = TransformWorldToObject(_WorldSpaceCameraPos);
    float basePlaneDistance = -cameraOS.z;
    int activeLayerCount = GetActiveVirtualDepthLayerCount();

    float proxyDepth = sourcePositionOS.z;
    float nearestLayerDistance = abs(basePlaneDistance);
    bool hasVisibleLayer = false;

    // Place the proxy geometry on the contributing virtual layer closest to the camera.
    [unroll]
    for (int depthLayerIndex = 0; depthLayerIndex < VIRTUAL_DEPTH_MAX_LAYER_COUNT; ++depthLayerIndex)
    {
        if (depthLayerIndex >= activeLayerCount)
            break;

        if (_VirtualLayerOpacities[depthLayerIndex] <= 0.0001)
            continue;

        float layerDepth = _VirtualLayerDepths[depthLayerIndex];
        float layerDistance = layerDepth - cameraOS.z;

        if (!IsVirtualDepthLayerVisibleToCamera(cameraOS.z, layerDepth))
            continue;

        float distanceToCamera = abs(layerDistance);
        if (!hasVisibleLayer || distanceToCamera < nearestLayerDistance)
        {
            proxyDepth = layerDepth;
            nearestLayerDistance = distanceToCamera;
        }

        hasVisibleLayer = true;
    }

    if (!hasVisibleLayer)
        return sourcePositionOS;

    float proxyDistance = proxyDepth - cameraOS.z;
    float2 spriteMinimum = _SpriteRect.xy;
    float2 spriteMaximum = _SpriteRect.xy + _SpriteRect.zw;
    float2 proxyMinimum = float2(1e20, 1e20);
    float2 proxyMaximum = float2(-1e20, -1e20);

    // Project every shifted virtual rectangle onto the raster plane and take their union.
    // This also covers off-axis cameras, where the projected rectangles are not nested.
    [unroll]
    for (int boundsLayerIndex = 0; boundsLayerIndex < VIRTUAL_DEPTH_MAX_LAYER_COUNT; ++boundsLayerIndex)
    {
        if (boundsLayerIndex >= activeLayerCount)
            break;

        if (_VirtualLayerOpacities[boundsLayerIndex] <= 0.0001)
            continue;

        float layerDepth = _VirtualLayerDepths[boundsLayerIndex];
        float layerDistance = layerDepth - cameraOS.z;
        if (!IsVirtualDepthLayerVisibleToCamera(cameraOS.z, layerDepth))
            continue;

        float projectionScale = proxyDistance / layerDistance;
        float2 layerOffset = layerOffsetPerDepth * layerDepth;
        float2 projectedMinimum = cameraOS.xy + (spriteMinimum + layerOffset - cameraOS.xy) * projectionScale;
        float2 projectedMaximum = cameraOS.xy + (spriteMaximum + layerOffset - cameraOS.xy) * projectionScale;

        proxyMinimum = min(proxyMinimum, min(projectedMinimum, projectedMaximum));
        proxyMaximum = max(proxyMaximum, max(projectedMinimum, projectedMaximum));
    }

    float2 normalizedSpritePosition = (sourcePositionOS.xy - spriteMinimum) / _SpriteRect.zw;
    return float3(lerp(proxyMinimum, proxyMaximum, normalizedSpritePosition), proxyDepth);
}

#endif
