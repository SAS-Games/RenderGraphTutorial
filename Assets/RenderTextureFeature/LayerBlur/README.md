# Layer Blur

This folder contains a self-contained layer-mask blur effect for URP Render Graph.

The effect has two parts:

1. `ObjectsToRenderTextureFeature` renders one or more object groups into mask textures.
2. `LayerBlurFeature` blurs the current camera color and composites different blur strengths through those masks.

Use this for frosted glass zones, magic blur fields, stealth shimmer areas, in-world blur panels, or any object/layer that should blur the scene inside its screen silhouette.

## Included Assets

- `LayerBlurFeature.cs`: renderer feature that creates the blur and composites it through one or more masks.
- `LayerBlur.shader`: hidden blur/composite shader used by the feature.
- `LayerBlur.mat`: material assigned to `LayerBlurFeature`.
- `LayerBlurMask.shader`: flat white mask shader for blur areas.
- `LayerBlurMask.mat`: material assigned to mask outputs in `ObjectsToRenderTextureFeature`.
- `LayerBlurEffectRecipes.md`: practical setup recipes with recommended starting values for common blur effects.

## How Multi-Layer Blur Works

`LayerBlurFeature` now has a list:

```text
Blur Layer Settings
```

Each entry reads a different mask texture and applies its own blur settings.

At the start of each blur render event, the feature copies the current camera color once. Every enabled blur entry at that render event blurs from that same clean source. This keeps light, medium, and heavy blur layers independent from each other instead of letting later entries blur a frame that already contains earlier blur composites.

Example:

```text
_LightBlurMask -> Blur Radius 1
_MediumBlurMask -> Blur Radius 3
_HeavyBlurMask -> Blur Radius 6
```

To feed those masks, add multiple `RenderTextureOutputSettings` entries in `ObjectsToRenderTextureFeature`.

The list order in `Blur Layer Settings` is the composite order. Later entries draw over earlier entries when masks overlap, but each entry still builds its blurred texture from the same clean source color.

## Full Setup

### 1. Create Or Choose Blur Layers

Create Unity layers such as:

- `LightBlur`
- `MediumBlur`
- `HeavyBlur`

Assign objects to the layer that matches the blur strength you want.

You can also use URP `Render Layer Mask` instead of Unity GameObject layers if gameplay, physics, or camera culling already use those layers.

### 2. Open The URP Renderer Asset

Open the renderer data asset used by your URP pipeline.

Common locations:

- `Project Settings > Graphics > Universal Render Pipeline Asset > Renderer List`
- `Project Settings > Quality > Rendering > Render Pipeline Asset`
- A renderer asset in your project, often named `Forward Renderer`, `Universal Renderer`, or similar

The renderer asset is where you add `ScriptableRendererFeature` entries.

### 3. Add `ObjectsToRenderTextureFeature`

Add `ObjectsToRenderTextureFeature` to the renderer feature list if it is not already there.

If you already added it for another effect, reuse the same feature and add more `RenderTextureOutputSettings` entries for blur masks.

### 4. Add One Mask Output Per Blur Layer

In `ObjectsToRenderTextureFeature`, add one `RenderTextureOutputSettings` entry for each blur strength.

Example output entries:

```text
Light blur output
  Texture Name: _LightBlurMask
  Material: LayerBlurMask
  Layer Mask: LightBlur

Medium blur output
  Texture Name: _MediumBlurMask
  Material: LayerBlurMask
  Layer Mask: MediumBlur

Heavy blur output
  Texture Name: _HeavyBlurMask
  Material: LayerBlurMask
  Layer Mask: HeavyBlur
```

Use these common values for each output:

- `Material`: `LayerBlurMask`
- `Material Pass Index`: `-1`
- `Render Pass Event`: `AfterRenderingOpaques`
- `Render Pass Input`: `Depth`
- `Render Queue Lower Bound`: `0`
- `Render Queue Upper Bound`: `2499` for opaque blur areas, or `5000` if transparent blur area objects should also define the mask
- `Color Format`: `ARGB32`
- `Texture Size Mode`: `Camera`
- `Camera Size Multiplier`: `1`
- `Filter Mode`: `Bilinear`
- `Wrap Mode`: `Clamp`
- `Sorting Criteria`: `CommonOpaque`
- `Render Layer Mask`: leave default unless you are filtering by rendering layers
- `Light Mode`: `Standard`
- `Depth`: enabled
- `Write Depth`: disabled
- `Depth Compare`: `LessEqual`

Optional debug:

- Enable `Debug View` on one mask output at a time.
- Use `Debug Display Mode`: `Overlay`.
- Disable `Debug View` after the mask is working.

### 5. Add `LayerBlurFeature`

Add `LayerBlurFeature` to the same renderer.

Put it after `ObjectsToRenderTextureFeature` in the renderer feature list.

Assign:

- `Blur Material`: `LayerBlur`

Then add one `Blur Layer Settings` entry per mask.

Example:

```text
Light blur entry
  Enabled: true
  Name: Light
  Mask Texture Name: _LightBlurMask
  Downsample: 2
  Iterations: 1
  Blur Radius: 1
  Opacity: 1

Medium blur entry
  Enabled: true
  Name: Medium
  Mask Texture Name: _MediumBlurMask
  Downsample: 2
  Iterations: 2
  Blur Radius: 3
  Opacity: 1

Heavy blur entry
  Enabled: true
  Name: Heavy
  Mask Texture Name: _HeavyBlurMask
  Downsample: 2
  Iterations: 3
  Blur Radius: 6
  Opacity: 1
```

For each entry:

- `Mask Texture Name` must exactly match one output texture name from `ObjectsToRenderTextureFeature`.
- `Render Pass Event` should usually be `AfterRenderingTransparents`.
- `Mask Threshold` can start at `0.5`.
- `Mask Softness` can start at `0.05`.

### 6. Test In Scene

1. Assign one visible mesh object to each blur layer.
2. Make sure each layer is enabled in its matching mask output.
3. Enter Play Mode or render the scene view with the URP renderer active.
4. Enable `Debug View` on each mask output one at a time to confirm each mask.
5. Disable `Debug View`.
6. Tune each `Blur Layer Settings` entry.

## Performance And Optimization

This feature is optimized for practical real-time use:

- It uses separable blur: horizontal pass plus vertical pass.
- It supports downsampling to reduce texture cost.
- Iterations are clamped from `1` to `4`.
- Blur radius is clamped from `0` to `8`.
- Disabled entries are skipped.
- Entries with `Opacity` set to `0` are skipped.
- Empty mask texture names are skipped.
- One renderer feature can manage multiple blur strengths, so you do not need many separate feature instances.

Important cost rule:

```text
Each active blur entry performs its own blur work.
```

That is necessary because different blur strengths create different blurred textures.

For better performance:

- Use fewer active blur entries.
- Increase `Downsample`.
- Lower `Iterations`.
- Lower `Blur Radius`.
- Use broad strength buckets like light/medium/heavy instead of many tiny variations.
- Avoid overlapping masks when possible.

## Feature Order

Recommended order in the renderer:

1. `ObjectsToRenderTextureFeature`
2. `LayerBlurFeature`

If you are also using mask outline:

1. `ObjectsToRenderTextureFeature`
2. `MaskOutlineFeature`
3. `LayerBlurFeature`

You can also swap the final two depending on whether the outline should be blurred or drawn sharply after the blur.

## Runtime Use

`LayerBlurFeature` does not choose objects by itself. It blurs wherever each configured mask texture is white.

Common options:

- Put blur-area objects on dedicated layers.
- Move objects between light/medium/heavy blur layers at runtime.
- Use `Render Layer Mask` if you do not want to change Unity layers.
- Use invisible proxy meshes on blur layers to define custom blur shapes.
- Disable individual blur entries at runtime by setting `Enabled` to false.

## Tuning

- `Downsample`: higher values are faster and softer, but lower resolution.
- `Iterations`: more horizontal/vertical pass pairs create smoother blur.
- `Blur Radius`: controls how far samples spread.
- `Mask Threshold`: controls the mask value where blur starts. Pure black mask pixels stay unblurred.
- `Mask Softness`: controls the soft transition width above `Mask Threshold`.
- `Opacity`: blends between the original scene and the blurred scene.
- `Camera Size Multiplier`: lower values on the mask output can make the mask cheaper but less precise.

## Troubleshooting

- No blur appears:
  - Confirm `ObjectsToRenderTextureFeature` exists before `LayerBlurFeature`.
  - Confirm every blur entry uses a mask texture name produced by `ObjectsToRenderTextureFeature`.
  - Confirm `LayerBlur` is assigned to `Blur Material`.
  - Enable `Debug View` on the mask output and check whether the blur object is visible in the mask.

- One blur layer works but another does not:
  - Check that each mask output has a unique `Texture Name`.
  - Check that each `Blur Layer Settings` entry uses the matching texture name.
  - Check that the object is on the correct layer or rendering layer.
  - Check that the entry is enabled and has `Opacity` above `0`.

- The wrong blur strength appears where masks overlap:
  - Reorder `Blur Layer Settings`.
  - Later entries composite over earlier entries.
  - Avoid overlapping masks if priority should be unambiguous.

- The blur appears everywhere:
  - Check that each mask output `Layer Mask` only includes the intended blur layer.
  - Check that the mask material is `LayerBlurMask`.
  - Increase `Mask Threshold` if your mask contains low non-black values outside the intended objects.
  - Use `Debug View` on the mask output and confirm the background is black.

- The blur area is missing:
  - Check the object layer.
  - Check `Layer Mask`.
  - Check `Render Queue Lower Bound` and `Render Queue Upper Bound`.
  - If the object uses an unusual shader pass, add the needed tag to `Shader Tags`.

- The blur appears through walls:
  - Enable `Depth`.
  - Use `Depth Compare`: `LessEqual`.
  - Keep `Texture Size Mode`: `Camera` when using camera depth.

- The blur is too expensive:
  - Increase `Downsample`.
  - Decrease `Iterations`.
  - Decrease `Blur Radius`.
  - Disable unused entries.
  - Reduce the number of blur strength buckets.

- The blur edge is too hard:
  - Increase `Mask Softness`.
  - Use `Filter Mode`: `Bilinear` on the mask output.
