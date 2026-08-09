#ifndef RENDER_TEXTURE_FEATURE_JUMP_FLOOD_DISTANCE_FIELD_INCLUDED
#define RENDER_TEXTURE_FEATURE_JUMP_FLOOD_DISTANCE_FIELD_INCLUDED

half JfaOutsideBand(float signedDistance, float width, float softness)
{
    float edgeSoftness = max(softness, 0.0001);
    half outside = step(0.0, signedDistance);
    half band = 1.0h - smoothstep(width, width + edgeSoftness, signedDistance);
    return outside * band;
}

half JfaInsideBand(float signedDistance, float width, float softness)
{
    float edgeSoftness = max(softness, 0.0001);
    float insideDistance = -signedDistance;
    half inside = step(0.0, insideDistance);
    half band = 1.0h - smoothstep(width, width + edgeSoftness, insideDistance);
    return inside * band;
}

half JfaEdgeBand(float signedDistance, float halfWidth, float softness)
{
    float edgeSoftness = max(softness, 0.0001);
    return 1.0h - smoothstep(halfWidth, halfWidth + edgeSoftness, abs(signedDistance));
}

half JfaOutsideGlow(float signedDistance, float falloff)
{
    float outsideDistance = max(signedDistance, 0.0);
    half outside = step(0.0, signedDistance);
    return outside * exp2(-outsideDistance * max(falloff, 0.0001));
}

half JfaInsideFill(float signedDistance, float softness)
{
    float edgeSoftness = max(softness, 0.0001);
    return 1.0h - smoothstep(-edgeSoftness, 0.0, signedDistance);
}

#endif
