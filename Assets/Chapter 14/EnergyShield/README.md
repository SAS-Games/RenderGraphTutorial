# Chapter 14 - Energy Shield

This chapter turns the existing masked-effect stack into a character-following energy shield. The visible Huscarl remains normally rendered while an invisible sphere supplies a separate shield mask.

## Reused Pipeline

1. `EnergyShieldProxy` follows the playable Huscarl and uses the shared `InvisibleMaskProxy` material.
2. `RenderObjectsToTextureFeature` captures the sphere into `_EnergyShieldMask` using the shared `ObjectMask` material.
3. `LayerBlurFeature` softens the scene visible through the shield.
4. `MaskDistortionFeature` bends the background inside the sphere.
5. `MaskHaloFeature` creates the cyan energy glow and smooth boundary rim.

The hard morphology outline is intentionally disabled for this chapter. Its binary edge is useful for selection highlights, but the halo rim produces a cleaner curved force-field silhouette. Distortion uses a higher threshold than the halo so refraction stays slightly inside the visible boundary.

```text
Invisible sphere around visible character
    -> _EnergyShieldMask
    -> blur + refraction
    -> smooth halo boundary
    -> camera color
```

## Controls

| Input | Action |
| --- | --- |
| `WASD` | Move the character relative to the camera |
| `E` | Toggle the energy shield |

## Setup Contract

The renderer is registered as index `11` in `PC_RPAsset`, and the scene camera uses index `11`. The proxy stays on the `Default` GameObject layer and adds `Effect Mask Primary` to its renderer, while the character remains on its normal gameplay layer.

## Reused Assets

- Chapter 12 environment
- Huscarl character prefab and animator
- Chapter 13 movement and follow-camera scripts
- Shared `ObjectMask` and `InvisibleMaskProxy` materials
- Existing blur, distortion, and halo features

No shader, HLSL, material, or character prefab is duplicated in this chapter. In a production game, call `SetShieldActive(bool)` from the health, damage, energy, or ability system.
