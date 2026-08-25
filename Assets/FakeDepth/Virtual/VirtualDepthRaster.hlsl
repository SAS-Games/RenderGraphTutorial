#ifndef VIRTUAL_DEPTH_RASTER_INCLUDED
#define VIRTUAL_DEPTH_RASTER_INCLUDED

float3 BuildVirtualDepthRasterPositionOS(float3 sourcePositionOS, float2 offsetPerDepth)
{
    float3 cameraOS = TransformWorldToObject(_WorldSpaceCameraPos);
    float basePlaneDistance = -cameraOS.z;
    int sliceCount = clamp((int)_SliceCount, 1, MAX_DEPTH_SAMPLES);

    float rasterDepth = sourcePositionOS.z;
    float nearestDistance = abs(basePlaneDistance);
    bool hasVisibleSlice = false;

    // Rasterize on the contributing virtual plane closest to the camera.
    [unroll]
    for (int depthIndex = 0; depthIndex < MAX_DEPTH_SAMPLES; ++depthIndex)
    {
        if (depthIndex >= sliceCount)
            break;

        if (_VirtualAlphas[depthIndex] <= 0.0001)
            continue;

        float sampleDistance = _VirtualDepths[depthIndex] - cameraOS.z;

        // Ignore planes that crossed behind the camera relative to the base sprite.
        if (sampleDistance * basePlaneDistance <= 0.000001)
            continue;

        float distanceToCamera = abs(sampleDistance);
        if (!hasVisibleSlice || distanceToCamera < nearestDistance)
        {
            rasterDepth = _VirtualDepths[depthIndex];
            nearestDistance = distanceToCamera;
        }

        hasVisibleSlice = true;
    }

    if (!hasVisibleSlice)
        return sourcePositionOS;

    float rasterDistance = rasterDepth - cameraOS.z;
    float2 spriteMinimum = _SpriteRect.xy;
    float2 spriteMaximum = _SpriteRect.xy + _SpriteRect.zw;
    float2 rasterMinimum = float2(1e20, 1e20);
    float2 rasterMaximum = float2(-1e20, -1e20);

    // Project every shifted virtual rectangle onto the raster plane and take their union.
    // This also covers off-axis cameras, where the projected rectangles are not nested.
    [unroll]
    for (int boundsIndex = 0; boundsIndex < MAX_DEPTH_SAMPLES; ++boundsIndex)
    {
        if (boundsIndex >= sliceCount)
            break;

        if (_VirtualAlphas[boundsIndex] <= 0.0001)
            continue;

        float virtualDepth = _VirtualDepths[boundsIndex];
        float sampleDistance = virtualDepth - cameraOS.z;
        if (sampleDistance * basePlaneDistance <= 0.000001)
            continue;

        float projectionScale = rasterDistance / sampleDistance;
        float2 sliceOffset = offsetPerDepth * virtualDepth;
        float2 projectedMinimum = cameraOS.xy + (spriteMinimum + sliceOffset - cameraOS.xy) * projectionScale;
        float2 projectedMaximum = cameraOS.xy + (spriteMaximum + sliceOffset - cameraOS.xy) * projectionScale;

        rasterMinimum = min(rasterMinimum, min(projectedMinimum, projectedMaximum));
        rasterMaximum = max(rasterMaximum, max(projectedMinimum, projectedMaximum));
    }

    float2 spritePosition = (sourcePositionOS.xy - spriteMinimum) / _SpriteRect.zw;
    return float3(lerp(rasterMinimum, rasterMaximum, spritePosition), rasterDepth);
}

#endif
