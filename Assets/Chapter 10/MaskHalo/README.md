# Chapter 10 - Mask Halo

`MaskHaloFeature` creates a character-style energy halo from one mask texture produced by `ObjectsToRenderTextureFeature`.

The effect contains three parts:

1. A broad colored aura.
2. A tighter, brighter glow near the silhouette.
3. A crisp full-resolution rim around the silhouette.

All three parts are rendered outside the mask. The character interior and unrelated environment pixels are not modified.

## Included Assets

- `MaskHaloFeature.cs`: renderer-feature settings and orchestration.
- `MaskHaloPass.cs`: Render Graph blur and composite implementation.
- `MaskHaloComposite.shader`: Kawase blur and additive halo composite shader.
- `MaskHaloComposite.mat`: material assigned to `MaskHaloFeature`.
- `Assets/RenderTextureFeature/Core/MaskedEffect/ObjectMask.shader`: shared flat-color mask shader used by all object-mask effects.
- `Assets/RenderTextureFeature/Core/MaskedEffect/ObjectMask.mat`: shared material assigned to the mask output.
- `MaskHaloRenderer.asset`: renderer configured with the mask producer followed by `MaskHaloFeature`.
- `MaskHalo.unity`: standalone example scene using renderer index `1`.

## Mental Model

```text
Character renderers
    -> ObjectsToRenderTextureFeature
    -> _CharacterHaloMask

_CharacterHaloMask
    -> downsampled Kawase blur
    -> soft expanded mask

Original mask + expanded mask
    -> broad outer glow
    -> concentrated inner glow
    -> crisp silhouette rim
    -> additive composite over camera color
```

The mask identifies the character. It does not contain halo colors or distances.

The blurred mask provides a smooth distance-like falloff around the silhouette. This is less exact than a true distance field, but it is stable, inexpensive, and easy to tune. JFA is not required for the current implementation.

## Renderer Setup

Use this renderer-feature order:

```text
1. ObjectsToRenderTextureFeature
2. MaskHaloFeature
```

### Mask Output

Add one output to `ObjectsToRenderTextureFeature`:

| Setting | Value |
| --- | --- |
| `Texture Name` | `_CharacterHaloMask` |
| `Material` | `ObjectMask` |
| `Material Pass Index` | `-1` |
| `Render Pass Event` | `AfterRenderingOpaques` |
| `Render Pass Input` | `Depth` |
| `Render Queue` | `0` to `5000` |
| `Texture Size Mode` | `Camera` |
| `Camera Size Multiplier` | `1` |
| `Filter Mode` | `Bilinear` |
| `Wrap Mode` | `Clamp` |
| `Layer Mask` | `Everything` |
| `Render Layer Mask` | `Effect Mask Primary` |
| `Depth` | Enabled |
| `Write Depth` | Disabled |
| `Depth Compare` | `LessEqual` |

Add `Effect Mask Primary` to the character renderer's `Rendering Layer Mask`. Its GameObject layer remains unchanged, so gameplay, physics, and camera filtering remain independent from the halo.

Enable the mask output's `Debug View` temporarily. The character must be white and everything else must be black. Disable `Debug View` after verification.

### Halo Feature

Add `MaskHaloFeature` after the mask producer and assign `MaskHaloComposite` to `Halo Material`.

Recommended starting values for the reference blue energy halo:

| Setting | Value |
| --- | --- |
| `Render Pass Event` | `AfterRenderingTransparents` |
| `Mask Texture Name` | `_CharacterHaloMask` |
| `Mask Threshold` | `0.5` |
| `Mask Softness` | `0.02` |
| `Opacity` | `1` |
| `Downsample` | `2` |
| `Blur Iterations` | `5` |
| `Blur Radius` | `2.5` |
| `Outer Glow Color` | Deep blue |
| `Outer Glow Intensity` | `1.25` |
| `Outer Glow Falloff` | `0.65` |
| `Inner Glow Color` | Electric blue |
| `Inner Glow Intensity` | `1.8` |
| `Inner Glow Tightness` | `2.2` |
| `Rim Color` | Pale cyan |
| `Rim Width` | `3` |
| `Rim Intensity` | `1.5` |

HDR colors and intensities above `1` work best when the camera uses HDR and Bloom is enabled in a URP Volume. Bloom is optional; the rim and aura remain visible without it.

## Controls

- `Downsample`: divides the temporary blur resolution. `2` is recommended. `4` is cheaper but less precise.
- `Blur Iterations`: controls aura smoothness and reach. Each iteration adds one fullscreen blur pass.
- `Blur Radius`: controls how far each blur pass expands the mask.
- `Outer Glow Falloff`: values below `1` preserve more distant glow. Increase it for a tighter aura.
- `Inner Glow Tightness`: increases concentration near the silhouette without adding passes.
- `Rim Width`: crisp outline width measured using the full-resolution mask.
- `Opacity`: scales the entire effect.

## Performance

The pass count is fixed by `Blur Iterations`:

```text
Total passes = Blur Iterations + 2 composites
```

The configured example uses five half-resolution blur passes, one additive glow composite, and one color-preserving rim composite, for seven passes total.

The temporary blur textures use a single-channel `R8` format. Only the final composite runs at camera resolution. The cost does not increase when `Blur Radius` increases, unlike a naive shader that searches more mask samples for every output pixel.

Recommended production ranges:

- Desktop/console: `Downsample 2`, `4-5` iterations.
- Mobile: `Downsample 4`, `3-4` iterations.
- Keep one halo feature per mask. Do not duplicate features to create the three visual bands; they are already produced by one composite.

## Troubleshooting

### No Halo

1. Confirm the camera uses `MaskHaloRenderer`.
2. Confirm `ObjectsToRenderTextureFeature` appears before `MaskHaloFeature`.
3. Confirm both features use `_CharacterHaloMask` exactly.
4. Enable mask `Debug View` and verify a white silhouette.
5. Confirm `MaskHaloComposite` is assigned to `Halo Material`.

### Halo Affects The Environment

The mask contains non-black environment pixels. Check the mask output's `Layer Mask`, override material, and debug view. The halo shader itself outputs zero where the expanded mask has no coverage.

### Halo Is Only A Thin Outline

Increase `Blur Iterations`, `Blur Radius`, or `Outer Glow Intensity`. Decrease `Outer Glow Falloff` to keep the distant aura visible.

### Halo Is Too Wide

Reduce `Blur Radius` first. Then reduce `Blur Iterations` or increase `Outer Glow Falloff`.

### Rim Is Jagged

Use a full camera-size mask, set its filter mode to `Bilinear`, and keep `Mask Softness` around `0.01-0.04`.
