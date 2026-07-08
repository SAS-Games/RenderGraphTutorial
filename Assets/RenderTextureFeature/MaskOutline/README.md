# Mask Outline

This folder contains a self-contained mask-based outline effect for URP Render Graph.

The effect has two parts:

1. `ObjectsToRenderTextureFeature` renders any chosen object group into a mask texture.
2. `MaskOutlineFeature` reads that mask, expands it in screen space, and composites an outside outline over the camera color.

This is intentionally generic. It can outline hovered objects, enemies, interactables, targets, loot, objectives, or any other group that you can render into a mask.

## Included Assets

- `MaskOutlineFeature.cs`: renderer feature that draws the final outline.
- `MaskOutlineComposite.shader`: hidden fullscreen shader used by the feature.
- `MaskOutlineComposite.mat`: material assigned to `MaskOutlineFeature`.
- `MaskOutlineMask.shader`: flat white mask shader for objects that should be outlined.
- `MaskOutlineMask.mat`: material assigned to the mask output in `ObjectsToRenderTextureFeature`.

## Full Setup

### 1. Choose How Objects Enter The Mask

Decide how an object becomes part of the outline mask.

Common options:

- Put outlineable objects on a dedicated Unity layer.
- Move objects onto/off that layer at runtime.
- Use URP `Render Layer Mask` if GameObject layers are already used for gameplay or physics.
- Use child/proxy renderers if the visual object should stay on its normal layer.

For a first test, create a Unity layer such as `MaskOutline` and assign one visible mesh object to it.

### 2. Open The URP Renderer Asset

Open the renderer data asset used by your URP pipeline.

Common locations:

- `Project Settings > Graphics > Universal Render Pipeline Asset > Renderer List`
- `Project Settings > Quality > Rendering > Render Pipeline Asset`
- A renderer asset in your project, often named `Forward Renderer`, `Universal Renderer`, or similar

The renderer asset is where you add `ScriptableRendererFeature` entries.

### 3. Add `ObjectsToRenderTextureFeature`

Add `ObjectsToRenderTextureFeature` to the renderer feature list if it is not already there.

This feature must exist because it produces the mask texture that `MaskOutlineFeature` reads.

### 4. Add A Mask Output

In `ObjectsToRenderTextureFeature`, add one entry to `RenderTextureOutputSettings`.

Use these values:

- `Texture Name`: `_MaskOutlineMask`
- `Material`: `MaskOutlineMask`
- `Material Pass Index`: `-1`
- `Render Pass Event`: `AfterRenderingOpaques`
- `Render Pass Input`: `Depth`
- `Render Queue Lower Bound`: `0`
- `Render Queue Upper Bound`: `2499` for opaque objects, or `5000` if transparent objects must also define the outline mask
- `Color Format`: `ARGB32`
- `Texture Size Mode`: `Camera`
- `Camera Size Multiplier`: `1`
- `Filter Mode`: `Point`
- `Wrap Mode`: `Clamp`
- `Sorting Criteria`: `CommonOpaque`
- `Layer Mask`: your outline mask layer
- `Render Layer Mask`: leave default unless you are filtering by rendering layers
- `Light Mode`: `Standard`
- `Depth`: enabled
- `Write Depth`: disabled
- `Depth Compare`: `LessEqual`

Optional debug:

- Enable `Debug View` to see the mask on screen.
- Use `Debug Display Mode`: `Overlay` first. It is easier to confirm that masked objects are white.
- Disable `Debug View` after the mask is working.

### 5. Add `MaskOutlineFeature`

Add `MaskOutlineFeature` to the same renderer.

Put it after `ObjectsToRenderTextureFeature` in the renderer feature list.

Use these values:

- `Profiling Name`: `Mask Outline`
- `Composite Material`: `MaskOutlineComposite`
- `Render Pass Event`: `AfterRenderingTransparents`
- `Mask Texture Name`: `_MaskOutlineMask`
- `Outline Color`: any color you want
- `Outline Width`: start with `3`
- `Outline Intensity`: start with `1`
- `Mask Threshold`: start with `0.5`
- `Outside Only`: enabled

The `Mask Texture Name` must exactly match the mask output texture name from `ObjectsToRenderTextureFeature`.

### 6. Test In Scene

1. Assign a visible mesh object to your outline mask layer.
2. Make sure that layer is enabled in the `Layer Mask` field of the mask output.
3. Enter Play Mode or render the scene view with the URP renderer active.
4. If needed, enable `Debug View` on the mask output to confirm the object appears in the mask.
5. Disable `Debug View` and tune the outline settings.

## Runtime Use

`MaskOutlineFeature` does not decide which objects should be outlined. It outlines whatever appears in `_MaskOutlineMask`.

Common runtime options:

- Move selected/hovered/targeted objects to the mask layer.
- Toggle their rendering layer and use `Render Layer Mask`.
- Maintain separate child renderers on a mask-only layer if gameplay layers must not change.
- Use proxy meshes for a cleaner or simplified outline silhouette.

## Tuning

- `Outline Width`: larger values create a thicker outline. Higher values cost more shader samples.
- `Outline Intensity`: increases brightness and alpha strength.
- `Outline Color`: final composited outline color.
- `Mask Threshold`: increase if the mask has soft or noisy edges.
- `Outside Only`: keeps the outline outside the masked object silhouette. Disable it to tint the full expanded mask.
- `Camera Size Multiplier`: lower values on the mask output can make the outline cheaper but less precise.

## Troubleshooting

- No outline appears:
  - Confirm `ObjectsToRenderTextureFeature` exists before `MaskOutlineFeature`.
  - Confirm both features use `_MaskOutlineMask`.
  - Confirm `MaskOutlineComposite` is assigned to `Composite Material`.
  - Enable `Debug View` on the mask output and check whether the object is visible in the mask.

- The whole object is filled instead of only outlined:
  - Make sure `Outside Only` is enabled.

- The object is missing from the mask:
  - Check the object layer.
  - Check `Layer Mask`.
  - Check `Render Queue Lower Bound` and `Render Queue Upper Bound`.
  - If the object uses an unusual shader pass, add the needed tag to `Shader Tags`.

- The outline appears through walls:
  - Enable `Depth`.
  - Use `Depth Compare`: `LessEqual`.
  - Keep `Texture Size Mode`: `Camera` when using camera depth.

- The outline is jagged:
  - Keep `Camera Size Multiplier` at `1`.
  - Try `Filter Mode`: `Bilinear`.
  - Use a slightly higher `Outline Width`.
