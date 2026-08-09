# Chapter 12 - Portal Effect Composition

This chapter creates one portal by composing renderer features from Chapters 7 through 10. It adds no new runtime C#, shaders, or materials.

## Reused Features

1. `RenderObjectsToTextureFeature` renders `PortalEffectProxy` into `_PortalMask` with the shared `ObjectMask` material.
2. `LayerBlurFeature` softens the scene inside the portal.
3. `MaskDistortionFeature` bends the blurred image and adds subtle chromatic separation.
4. `MaskHaloFeature` adds an outer aura, inner glow, and bright rim.
5. `MaskOutlineFeature` draws a final crisp cyan edge.

All consumers read the same `_PortalMask`. No duplicate mask or proxy assets are required.

## Included Assets

- `PortalEffect.unity`: standalone portal composition scene.
- `PortalEffectRenderer.asset`: renderer containing the complete feature stack.
- `Assets/RenderTextureFeature/Core/MaskedEffect/ObjectMask.mat`: shared mask override material.
- `Assets/RenderTextureFeature/Core/MaskedEffect/InvisibleMaskProxy.mat`: shared invisible material assigned to the portal proxy.

## Setup Contract

The renderer is registered as index `9` in `PC_RPAsset`. The scene camera uses renderer index `9`. `PortalEffectProxy` remains on the `Default` GameObject layer and adds the shared `Effect Mask Primary` rendering-layer bit.

```text
PortalEffectProxy
    -> _PortalMask
    -> blur
    -> distortion
    -> halo
    -> outline
    -> camera color
```

The feature order matters because all four consumers run at `AfterRenderingTransparents`. Later features process the camera result produced by earlier features.

## Starting Values

| Effect | Important values |
| --- | --- |
| Blur | Downsample `2`, Iterations `2`, Radius `2.5`, Opacity `0.65` |
| Distortion | Strength `8 px`, Frequency `10`, Speed `0.75`, Chromatic `1 px` |
| Halo | Downsample `2`, Iterations `4`, Radius `2.25` |
| Outline | Width `2 px`, Intensity `1.2`, Outside mode |

Move, scale, or disable `PortalEffectProxy` to change the portal region at runtime. Replace its cube mesh with a disc, ring, or authored portal mesh for the final silhouette.

## Performance

This example intentionally stacks several effects to teach composition. For production, disable any layer that does not materially improve the result. Blur and halo are the most expensive stages because they record multiple fullscreen passes.
