#ifndef MORPHOLOGY_UTILS_INCLUDED
#define MORPHOLOGY_UTILS_INCLUDED

half ApplyMaskThreshold(half mask, float maskThreshold, float edgeSoftness)
{
    half lower = maskThreshold - edgeSoftness;
    half upper = maskThreshold + edgeSoftness;
    return smoothstep(lower, upper, mask);
}

half CalculateFeatherWeight(float distanceFromCenter, float solidWidth, float featherWidth)
{
    if (distanceFromCenter <= solidWidth)
        return 1.0h;

    if (featherWidth <= 0.001)
        return 0.0h;

    float featherDistance = distanceFromCenter - solidWidth;
    return 1.0h - saturate(featherDistance / featherWidth);
}

half3 SampleMorphologyState(
    TEXTURE2D_X_PARAM(sourceTexture, sourceSampler), float2 uv, float maskThreshold, float edgeSoftness,
    bool inputIsPackedMorphology)
{
    half3 source = SAMPLE_TEXTURE2D_X(sourceTexture, sourceSampler, uv).rgb;

    if (inputIsPackedMorphology)
        return source;

    half mask = ApplyMaskThreshold(source.r, maskThreshold, edgeSoftness);
    return mask.xxx;
}

// Uses the same packed layout as the feathered kernel, with G left unused.
// Returns R = dilation, G = 0 and B = erosion.
half3 Morphology1D(TEXTURE2D_X_PARAM(sourceTexture, sourceSampler), float2 uv, float2 texelSize, float2 direction,
                   float solidWidth, int maxRadius, float maskThreshold, float edgeSoftness,
                   bool inputIsPackedMorphology)
{
    int solidRadius = clamp((int)ceil(solidWidth), 0, maxRadius);
    half dilated = 0.0h;
    half eroded = 1.0h;

    [loop]
    for (int offset = -maxRadius; offset <= maxRadius; offset++)
    {
        int pixelDistance = abs(offset);
        if (pixelDistance > solidRadius)
            continue;

        float2 sampleUV = uv + direction * texelSize * (float)offset;
        half3 sampleState = SampleMorphologyState(
            TEXTURE2D_X_ARGS(sourceTexture, sourceSampler), sampleUV, maskThreshold, edgeSoftness,
            inputIsPackedMorphology);

        dilated = max(dilated, sampleState.r);
        eroded = min(eroded, sampleState.b);
    }

    return half3(dilated, 0.0h, eroded);
}

// Outside-only variant. Returns R = dilation and G = weighted dilation/feather.
half2 FeatheredDilation1D(
    TEXTURE2D_X_PARAM(sourceTexture, sourceSampler), float2 uv, float2 texelSize, float2 direction, float solidWidth,
    float featherWidth, int maxRadius, float maskThreshold, float edgeSoftness, bool inputIsPackedMorphology)
{
    int solidRadius = clamp((int)ceil(solidWidth), 0, maxRadius);
    int totalRadius = clamp((int)ceil(solidWidth + featherWidth), 0, maxRadius);

    half dilated = 0.0h;
    half weighted = 0.0h;

    [loop]
    for (int offset = -maxRadius; offset <= maxRadius; offset++)
    {
        int pixelDistance = abs(offset);
        if (pixelDistance > totalRadius)
            continue;

        float2 sampleUV = uv + direction * texelSize * (float)offset;
        half3 sampleState = SampleMorphologyState(
            TEXTURE2D_X_ARGS(sourceTexture, sourceSampler), sampleUV, maskThreshold, edgeSoftness,
            inputIsPackedMorphology);

        if (pixelDistance <= solidRadius)
            dilated = max(dilated, sampleState.r);

        half weight = CalculateFeatherWeight((float)pixelDistance, solidWidth, featherWidth);
        weighted = max(weighted, sampleState.g * weight);
    }

    return half2(dilated, weighted);
}

// Returns R = dilation, G = weighted dilation/feather and B = erosion.
half3 FeatheredMorphology1D(
    TEXTURE2D_X_PARAM(sourceTexture, sourceSampler), float2 uv, float2 texelSize, float2 direction, float solidWidth,
    float featherWidth, int maxRadius, float maskThreshold, float edgeSoftness, bool inputIsPackedMorphology)
{
    int solidRadius = clamp((int)ceil(solidWidth), 0, maxRadius);
    int totalRadius = clamp((int)ceil(solidWidth + featherWidth), 0, maxRadius);

    half dilated = 0.0h;
    half weighted = 0.0h;
    half eroded = 1.0h;

    [loop]
    for (int offset = -maxRadius; offset <= maxRadius; offset++)
    {
        int pixelDistance = abs(offset);
        if (pixelDistance > totalRadius)
            continue;

        float2 sampleUV = uv + direction * texelSize * (float)offset;
        half3 sampleState = SampleMorphologyState(
            TEXTURE2D_X_ARGS(sourceTexture, sourceSampler), sampleUV, maskThreshold, edgeSoftness,
            inputIsPackedMorphology);

        if (pixelDistance <= solidRadius)
        {
            dilated = max(dilated, sampleState.r);
            eroded = min(eroded, sampleState.b);
        }

        half weight = CalculateFeatherWeight((float)pixelDistance, solidWidth, featherWidth);
        weighted = max(weighted, sampleState.g * weight);
    }

    return half3(dilated, weighted, eroded);
}

#endif
