#ifndef VIRTUAL_DEPTH_SPRITE_UNLIT_PASS_INCLUDED
#define VIRTUAL_DEPTH_SPRITE_UNLIT_PASS_INCLUDED

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 proxyPositionOS : TEXCOORD0;
    half4 vertexColor : COLOR;
};

Varyings Vert(Attributes input)
{
    Varyings output;
    float3 proxyPositionOS = CALCULATE_VIRTUAL_DEPTH_PROXY_POSITION(input);

    output.proxyPositionOS = proxyPositionOS;
    output.positionCS = TransformObjectToHClip(proxyPositionOS);
    output.vertexColor = input.color;
    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    VirtualDepthViewRay viewRay;
    if (!TryBuildVirtualDepthViewRay(input.proxyPositionOS, viewRay))
        discard;

    half accumulatedAlpha = 0.0h;
    int activeLayerCount = GetActiveVirtualDepthLayerCount();

    [unroll]
    for (int spriteLayerIndex = 0; spriteLayerIndex < VIRTUAL_DEPTH_MAX_LAYER_COUNT; ++spriteLayerIndex)
    {
        if (spriteLayerIndex >= activeLayerCount)
            break;

        float layerDepth = _VirtualLayerDepths[spriteLayerIndex];
        half layerOpacity = (half)_VirtualLayerOpacities[spriteLayerIndex];
        if (layerOpacity <= 0.0001h)
            continue;

        half layerMask = SampleVirtualDepthLayerMask(viewRay, layerDepth, float2(0.0, 0.0));
        if (layerMask <= 0.001h)
            continue;

        half layerAlpha = layerMask * layerOpacity;
        accumulatedAlpha = 1.0h - (1.0h - accumulatedAlpha) * (1.0h - layerAlpha);

        if (accumulatedAlpha >= 0.995h)
            break;
    }

    accumulatedAlpha *= (half)_EffectColor.a;
    accumulatedAlpha *= input.vertexColor.a;

    if (accumulatedAlpha <= 0.001h)
        discard;

    return half4((half3)_EffectColor.rgb, accumulatedAlpha);
}

#endif
