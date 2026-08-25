#ifndef VIRTUAL_DEPTH_TIGHT_PROXY_PROJECTION_INCLUDED
#define VIRTUAL_DEPTH_TIGHT_PROXY_PROJECTION_INCLUDED

#include "Assets/FakeDepth/ShaderLibrary/VirtualDepthLayerSampling.hlsl"

#define VIRTUAL_DEPTH_TIGHT_PROXY_DIRECTION_COUNT 16

float4 _VirtualDepthTightProxyHalfPlanes[VIRTUAL_DEPTH_TIGHT_PROXY_DIRECTION_COUNT];

bool TryGetVirtualDepthTightProxyPlane(
        out float3 cameraOS,
        out int activeLayerCount,
        out float proxyDepth,
        out float proxyDistance)
{
    cameraOS = TransformWorldToObject(_WorldSpaceCameraPos);
    activeLayerCount = GetActiveVirtualDepthLayerCount();
    proxyDepth = 0.0;
    proxyDistance = -cameraOS.z;
    float nearestLayerDistance = abs(proxyDistance);
    bool hasVisibleLayer = false;

    [unroll]
    for (int tightProxyLayerIndex = 0; tightProxyLayerIndex < VIRTUAL_DEPTH_MAX_LAYER_COUNT; ++tightProxyLayerIndex)
    {
        if (tightProxyLayerIndex >= activeLayerCount)
            break;

        if (_VirtualLayerOpacities[tightProxyLayerIndex] <= 0.0001)
            continue;

        float layerDepth = _VirtualLayerDepths[tightProxyLayerIndex];
        float layerDistance = layerDepth - cameraOS.z;
        if (!IsVirtualDepthLayerVisibleToCamera(cameraOS.z, layerDepth))
            continue;

        float distanceToCamera = abs(layerDistance);
        if (!hasVisibleLayer || distanceToCamera < nearestLayerDistance)
        {
            proxyDepth = layerDepth;
            proxyDistance = layerDistance;
            nearestLayerDistance = distanceToCamera;
        }

        hasVisibleLayer = true;
    }

    return hasVisibleLayer;
}

float CalculateVirtualDepthProjectedSupport(int directionIndex, float3 cameraOS, int activeLayerCount,
                                            float proxyDistance, float2 layerOffsetPerDepth)
{
    float2 direction = _VirtualDepthTightProxyHalfPlanes[directionIndex].xy;
    float sourceSupport = _VirtualDepthTightProxyHalfPlanes[directionIndex].z;
    float projectedSupport = -1e20;

    [unroll]
    for (int tightSupportLayerIndex = 0; tightSupportLayerIndex < VIRTUAL_DEPTH_MAX_LAYER_COUNT; ++
         tightSupportLayerIndex)
    {
        if (tightSupportLayerIndex >= activeLayerCount)
            break;

        if (_VirtualLayerOpacities[tightSupportLayerIndex] <= 0.0001)
            continue;

        float layerDepth = _VirtualLayerDepths[tightSupportLayerIndex];
        float layerDistance = layerDepth - cameraOS.z;
        if (!IsVirtualDepthLayerVisibleToCamera(cameraOS.z, layerDepth))
            continue;

        float projectionScale = proxyDistance / layerDistance;
        float2 projectedTranslation = cameraOS.xy * (1.0 - projectionScale) + layerOffsetPerDepth * layerDepth *
            projectionScale;
        float layerSupport = dot(direction, projectedTranslation) + sourceSupport * projectionScale;
        projectedSupport = max(projectedSupport, layerSupport);
    }

    return projectedSupport;
}

float2 IntersectVirtualDepthSupportLines(float2 directionA, float supportA, float2 directionB, float supportB)
{
    float determinant = directionA.x * directionB.y - directionA.y * directionB.x;
    return float2(
        (supportA * directionB.y - directionA.y * supportB) / determinant,
        (directionA.x * supportB - supportA * directionB.x) / determinant);
}

float3 CalculateVirtualDepthTightProxyPositionOS(float boundaryIndexValue, float2 layerOffsetPerDepth)
{
    float3 cameraOS;
    int activeLayerCount;
    float proxyDepth;
    float proxyDistance;
    if (!TryGetVirtualDepthTightProxyPlane(cameraOS, activeLayerCount, proxyDepth, proxyDistance))
        return float3(0.0, 0.0, 0.0);

    int directionIndex = clamp((int)(boundaryIndexValue + 0.5), 0, VIRTUAL_DEPTH_TIGHT_PROXY_DIRECTION_COUNT - 1);
    int nextDirectionIndex = directionIndex + 1;
    if (nextDirectionIndex >= VIRTUAL_DEPTH_TIGHT_PROXY_DIRECTION_COUNT)
        nextDirectionIndex = 0;

    float2 directionA = _VirtualDepthTightProxyHalfPlanes[directionIndex].xy;
    float2 directionB = _VirtualDepthTightProxyHalfPlanes[nextDirectionIndex].xy;
    float supportA = CalculateVirtualDepthProjectedSupport(directionIndex, cameraOS, activeLayerCount, proxyDistance,
                                                           layerOffsetPerDepth);
    float supportB = CalculateVirtualDepthProjectedSupport(nextDirectionIndex, cameraOS, activeLayerCount,
                                                           proxyDistance, layerOffsetPerDepth);
    float2 proxyPosition = IntersectVirtualDepthSupportLines(directionA, supportA, directionB, supportB);
    return float3(proxyPosition, proxyDepth);
}

#endif
