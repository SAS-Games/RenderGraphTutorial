# Mask Distortion

`MaskDistortionFeature` is a screen-space distortion/refraction effect driven by a mask texture from `ObjectsToRenderTextureFeature`.

It is useful for frosted glass shimmer, heat haze, magic shields, portals, water panes, cloak shimmer, and any effect where a screen area should bend what is behind it.

## Included Assets

- `MaskDistortionFeature.cs`: URP Render Graph renderer feature.
- `MaskDistortionComposite.shader`: fullscreen composite shader.
- `MaskDistortionComposite.mat`: material assigned to the feature.

## How It Works

1. `ObjectsToRenderTextureFeature` renders selected objects or proxy meshes into a mask.
2. `MaskDistortionFeature` copies the current camera color.
3. The composite shader reads the mask and samples the copied camera color with a moving offset.
4. Only white mask areas receive distortion.

Black mask pixels stay unchanged.

## Renderer Setup

Renderer feature order:

```text
1. ObjectsToRenderTextureFeature
2. Optional effects that should happen before distortion
3. MaskDistortionFeature
```

For frosted glass that uses both blur and distortion:

```text
1. ObjectsToRenderTextureFeature
2. LayerBlurFeature
3. MaskDistortionFeature
```

This lets the distortion warp the already blurred glass area.

## Reusing An Existing Mask

You do not need a new mask if one already exists.

For the frosted glass setup, use:

```text
Mask Texture Name: _FrostedGlassBlurMask
```

For the layer blur demo, you can test with:

```text
Mask Texture Name: _LayerHeavyBlurMask
```

The `Mask Texture Name` must exactly match a `Texture Name` entry in `ObjectsToRenderTextureFeature`.

## Recommended Frosted Glass Values

Use these values in `MaskDistortionFeature`:

| Setting | Value |
| --- | --- |
| `Distortion Material` | `MaskDistortionComposite` |
| `Render Pass Event` | `AfterRenderingTransparents` |
| `Mask Texture Name` | `_FrostedGlassBlurMask` |
| `Distortion Strength Pixels` | `3` |
| `Distortion Frequency` | `18` |
| `Distortion Speed` | `0.2` |
| `Chromatic Aberration Pixels` | `0.25` |
| `Mask Threshold` | `0.5` |
| `Mask Softness` | `0.08` |
| `Opacity` | `0.35` |
| `Tint Color` | light blue or white |
| `Tint Strength` | `0.05` |

## Recommended Heat Haze Values

| Setting | Value |
| --- | --- |
| `Mask Texture Name` | your heat mask, for example `_HeatHazeMask` |
| `Distortion Strength Pixels` | `6` |
| `Distortion Frequency` | `32` |
| `Distortion Speed` | `1.2` |
| `Chromatic Aberration Pixels` | `0` |
| `Mask Threshold` | `0.25` |
| `Mask Softness` | `0.25` |
| `Opacity` | `0.55` |
| `Tint Strength` | `0` |

## Recommended Portal Values

| Setting | Value |
| --- | --- |
| `Mask Texture Name` | your portal mask, for example `_PortalMask` |
| `Distortion Strength Pixels` | `10` |
| `Distortion Frequency` | `10` |
| `Distortion Speed` | `0.75` |
| `Chromatic Aberration Pixels` | `1` |
| `Mask Threshold` | `0.45` |
| `Mask Softness` | `0.04` |
| `Opacity` | `0.8` |
| `Tint Strength` | `0.12` |

## Tuning

- Increase `Distortion Strength Pixels` for more bending.
- Increase `Distortion Frequency` for tighter shimmer.
- Increase `Distortion Speed` for faster movement.
- Increase `Mask Softness` when mask edges are too hard.
- Keep `Chromatic Aberration Pixels` low unless you want a glitch or portal look.
- Lower `Opacity` when gameplay readability matters.

## Troubleshooting

If no distortion appears:

1. Confirm `ObjectsToRenderTextureFeature` runs before `MaskDistortionFeature`.
2. Confirm the mask output `Texture Name` exactly matches `Mask Texture Name`.
3. Enable `Debug View` on the mask output and confirm the target area is white.
4. Confirm `Distortion Material` is assigned to `MaskDistortionComposite`.
5. Confirm `Opacity` and `Distortion Strength Pixels` are above `0`.

If the effect covers the wrong area, fix the mask output `Layer Mask`, not the distortion feature.
