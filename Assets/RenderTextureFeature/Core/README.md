# Objects To Render Texture Feature

`ObjectsToRenderTextureFeature` is a URP `ScriptableRendererFeature` that renders a filtered set of scene objects into one or more temporary Render Graph textures.

The feature is a producer. It does not create the final visible effect by itself. Its job is to create named object buffers that other renderer features, fullscreen passes, materials, Shader Graphs, or debug views can read later in the same camera frame.

The mental model is:

```text
Camera frame
  -> choose objects with filters
  -> draw those objects into a temporary texture
  -> expose that texture by name and Render Graph handle
  -> another pass reads it
```

This core README intentionally avoids effect-specific setup. Individual effects should keep their own README files with their own exact material names, texture names, feature ordering, and tuning values.

## What This Feature Is For

Use this feature when you need a texture that represents some subset of objects in the current camera view.

The texture can contain:

- a black/white mask
- object colors rendered with original materials
- flat colors from an override material
- data encoded by a custom material
- a separate object buffer for a later fullscreen effect

The feature answers this question:

```text
Can I render only these objects into a texture during the normal URP frame?
```

If the answer is yes, this feature is the reusable producer.

## What This Feature Is Not

This feature is not:

- a post-processing effect by itself
- a replacement for a second camera
- a persistent `RenderTexture` asset
- a minimap, reflection, or portal camera system
- a built-in object-membership system
- a system that decides gameplay membership

It renders from the active camera using the active camera culling results. The output textures are temporary frame resources managed by Render Graph.

## Main Files

- `ObjectsToRenderTextureFeature.cs`: the renderer feature shown in the URP renderer asset.
- `RenderTexturePass.cs`: the render pass that creates an output texture and draws matching objects into it.
- `RenderTexturePass.Settings.cs`: the serializable inspector settings for each output texture.
- `RenderTextureDebugPass.cs`: optional debug pass that draws an output texture back to the camera.
- `RenderTextureDebug.shader`: hidden shader used by the debug pass.
- `RenderingHelpers.cs`: helper for creating a renderer list with a render-state override.

## High-Level Frame Flow

For each entry in `RenderTextureOutputSettings`, the feature does this:

1. Reads one `RenderTexturePass.Settings` object.
2. Creates or reuses a `RenderTexturePass`.
3. Calls `Setup` on that pass.
4. Enqueues the pass into the renderer.
5. If `Debug View` is enabled, creates or reuses a `RenderTextureDebugPass`.
6. Enqueues the debug pass after the output pass.

Then `RenderTexturePass` does this inside Render Graph:

1. Creates a raster render pass.
2. Builds a destination texture descriptor from the active camera descriptor.
3. Creates a temporary Render Graph texture.
4. Builds a renderer list from the selected filters.
5. Clears the destination texture to black.
6. Draws matching renderers into the destination texture.
7. Stores the texture handle in `FrameTextureRegistry`.
8. Exposes the texture globally using `Texture Name`.
9. Sets a matching texel-size vector.

## Data Flow

The generated texture is exposed in two ways.

### Global Shader Texture

The pass calls:

```csharp
builder.SetGlobalTextureAfterPass(destination, texturePropertyId);
```

That makes the texture available through the global shader property named by `Texture Name`.

If `Texture Name` is:

```text
_SomeObjectBuffer
```

then later shaders can sample:

```text
_SomeObjectBuffer
```

The pass also sets:

```text
_SomeObjectBuffer_TexelSize
```

with this value:

```text
(1 / width, 1 / height, width, height)
```

This is useful for screen-space pixel offsets, edge sampling, blur kernels, dilation, erosion, and neighbor sampling.

### Render Graph Texture Handle

Render Graph passes should prefer explicit texture handles when possible.

`FrameTextureRegistry` stores textures by shader property id:

```csharp
TryGetTexture(int texturePropertyId, out TextureHandle texture, out Vector4 texelSize)
```

A consumer pass can:

1. Convert the same texture name to a property id with `Shader.PropertyToID`.
2. Ask `FrameTextureRegistry` for the texture handle.
3. Declare it with `builder.UseTexture`.
4. Sample it in the consuming pass.

This avoids relying only on global state and makes Render Graph dependencies clearer.

## Renderer Feature Fields

### `ProfilingName`

Human-readable name used for profiling labels and generated pass names.

It does not change rendering behavior.

Use a name that describes the group of output textures this feature produces.

### `RenderTextureOutputSettings`

List of output texture configurations.

Each list element creates one output render pass and one temporary texture.

Use one element when you need one object buffer. Use multiple elements when different consumers need different object filters, texture names, formats, sizes, or materials.

This field has `FormerlySerializedAs` aliases:

- `RenderTextureOutputs`
- `TextureSettings`

Those aliases exist only to preserve inspector data from earlier development names.

## Output Settings Reference

Every item in `RenderTextureOutputSettings` is a `RenderTexturePass.Settings` object.

The settings can be grouped into:

- material override settings
- pass timing settings
- object filtering settings
- texture creation settings
- depth settings
- shader keyword settings
- debug settings

## Material Override Settings

### `Material`

Optional material used instead of each object's original material.

Use an override material when the output texture should contain effect-specific data instead of normal scene shading.

Typical override materials render:

- solid white
- a category color
- encoded normals
- encoded depth-like data
- custom ids or masks

Leave this empty when you want objects to render with their original materials.

Why it matters:

- With no override material, the texture represents the objects as they normally shade.
- With an override material, the texture becomes a controlled data buffer.

How to use it:

1. Create a shader/material that writes the data you want.
2. Assign that material here.
3. Make sure `Material Pass Index` points to the right pass.

### `Material Pass Index`

Controls which pass of the override material renders.

Values:

- `-1`: render all passes
- `0`: render only pass 0
- `1` or higher: render that specific pass index

Use `-1` for simple one-pass materials. Use a specific index when a shader has multiple passes and only one of them should write this output.

If the wrong shader pass is used, the texture may be empty, incorrectly colored, or more expensive than expected.

## Pass Timing Settings

### `Render Pass Event`

Controls when this output pass runs inside the URP frame.

This matters because the output must be created before any pass that consumes it.

General guidance:

- Use an opaque-stage event when the output only needs opaque objects.
- Use a later event when the output must include later-rendered objects.
- Keep producer passes before consumer passes.

If a consumer cannot find a texture, the issue is often feature order, pass event order, or a texture-name mismatch.

### `Render Pass Input`

Declares which URP frame resources this pass needs.

Common values:

- `None`: use when the pass does not need URP-provided depth, normals, motion, or color input.
- `Depth`: use when depth-aware rendering or material sampling needs camera depth.
- `Normal`: use when the material samples camera normals.
- `Color`: use when the material samples camera color.

Why it matters:

- Inputs can force URP to create or preserve additional textures.
- Extra inputs can increase memory and bandwidth.
- Missing inputs can cause shaders or depth-dependent behavior to fail.

Use the smallest input set that supports the output.

## Render Queue Filtering

### `Render Queue Lower Bound`

Minimum material render queue included in the renderer list.

Render queues decide broad render ordering categories. Filtering by queue lets this pass include only the object types it should capture.

Use a low value when you want broad inclusion. Raise it when you want to isolate a later render queue range.

### `Render Queue Upper Bound`

Maximum material render queue included in the renderer list.

The default `2499` captures opaque queues. Higher values can include transparent queues.

Why it matters:

- Opaque-only captures are usually simpler and more stable.
- Transparent captures can be order-dependent and may need different sorting or timing.

If objects are missing, check whether their materials are outside the queue range.

## Texture Creation Settings

### `Color Format`

The render texture format for the output color buffer.

Use a simple 8-bit-per-channel format for most masks and color buffers. Use a higher precision format only when the consumer actually needs it.

Why it matters:

- Higher precision costs more memory and bandwidth.
- Lower precision is usually enough for masks.
- Some platforms may support formats differently.

### `Texture Size Mode`

Controls how the output texture size is chosen.

Modes:

- `Camera`: base size comes from the active camera descriptor.
- `Custom`: size comes from `Texture Size`.

Use `Camera` when the texture is sampled in screen space and must align with the camera view.

Use `Custom` when the texture is not required to match the camera resolution.

### `Camera Size Multiplier`

Scales the active camera size when `Texture Size Mode` is `Camera`.

Meaning:

- `1`: same resolution as the active camera
- below `1`: lower resolution and cheaper
- above `1`: higher resolution and more expensive

The code clamps this value to `0..2` and clamps final width/height to at least `1`.

Why it matters:

- Lower values reduce texture cost.
- Lower values reduce edge precision.
- Higher values improve detail but increase memory and fill cost.

Depth note:

The active camera depth attachment is only used when the generated output is exactly the same size as the active camera depth texture. If `Camera Size Multiplier` is anything other than `1`, the pass skips the camera depth attachment because Render Graph requires color and depth attachments to have matching dimensions.

### `Texture Size`

Explicit width and height used when `Texture Size Mode` is `Custom`.

Ignored when `Texture Size Mode` is `Camera`.

The pass clamps width and height to at least `1`.

Use this for fixed-size buffers or non-screen-aligned data. Avoid it for effects that must line up with camera pixels unless you handle the mismatch yourself.

### `Filter Mode`

Controls how the output texture is sampled.

Common choices:

- `Point`: sharp, exact texel sampling.
- `Bilinear`: smooth interpolation between texels.

Why it matters:

- Hard masks often want `Point`.
- Soft compositing often benefits from `Bilinear`.
- Low-resolution outputs look blockier with `Point`.

### `Wrap Mode`

Controls sampling outside the `0..1` UV range.

For screen-space textures, `Clamp` is usually safest because it prevents the texture from repeating at screen edges.

Use other wrap modes only when the consumer shader intentionally samples outside normal screen UVs.

## Draw Ordering And Object Filtering

### `Sorting Criteria`

Controls renderer sorting inside the generated renderer list.

Use sorting that matches the render queue type you are capturing.

Why it matters:

- Opaque rendering usually wants front-to-back style sorting.
- Transparent rendering usually wants transparent sorting.
- Sorting can affect overdraw and visual correctness.

### `Layer Mask`

Unity GameObject layers included in this output.

This is the most common high-level filter.

Why it matters:

- It lets you choose object membership without changing materials.
- It works with Unity's existing layer workflow.
- It can be controlled at runtime by moving objects between layers.

How to use it:

1. Decide which GameObject layer or layers should be captured.
2. Set `Layer Mask` to those layers.
3. Make sure the camera can also see those objects when needed.

### `Render Layer Mask`

URP rendering layers included in this output.

Rendering layers are separate from Unity GameObject layers.

Use this when you want effect membership independent from gameplay, physics, or camera culling layers.

Why it matters:

- GameObject layers are often already used for physics, AI, camera culling, or gameplay.
- Rendering layers let a renderer participate in rendering effects without changing its GameObject layer.

Leave this at default if you are not using URP rendering layers.

### `Texture Name`

Global shader property name used for the output texture.

This is the contract between the producer and every consumer.

Rules:

- Start with `_` by convention.
- Use a unique name per output.
- Use the exact same name in consuming passes/materials.
- Avoid spaces and special characters.
- Do not leave it empty.

The code converts this name with:

```csharp
Shader.PropertyToID(TextureName)
```

The texel-size property is derived by appending:

```text
_TexelSize
```

to the texture name.

## Shader Pass Filtering

### `Light Mode`

Flags for common shader `LightMode` tags.

The renderer list uses these tags to decide which shader passes are valid draw candidates.

`Standard` includes common URP forward/unlit tags:

- `SRPDefaultUnlit`
- `UniversalForward`
- `UniversalForwardOnly`
- `LightweightForward`

Additional flags are available for depth and normal-related shader passes.

Why it matters:

If an object has the correct layer and render queue but still does not draw, its shader may not expose a pass with one of the selected `LightMode` tags.

### `Shader Tags`

Extra shader `LightMode` tag strings.

Use this for custom shaders that use custom pass tags.

How to use it:

1. Inspect the shader pass that should draw into this output.
2. Find its `LightMode` tag.
3. Add that tag string to `Shader Tags`.

This supplements `Light Mode`; it does not replace it.

## Global Keyword Settings

### `Global Shader Keywords`

Optional list of global shader keyword changes applied before and after this output renders.

Each entry has:

- `Name`
- `Disabled`
- `Before Render Mode`
- `After Render Mode`

Use this only when a shader requires a global keyword state for this capture pass.

Why it matters:

- Global keywords affect shader state beyond a single material.
- Incorrect keyword state can change unrelated rendering.
- Overuse can create hard-to-debug rendering differences.

Prefer separate materials, material properties, local shader keywords, or shader variants when possible.

### `GlobalKeyword.Name`

The keyword string to change.

Keep it exactly equal to the shader keyword name.

### `GlobalKeyword.Disabled`

Skips this entry without removing it from the list.

Use it to temporarily disable one keyword action while preserving the configuration.

### `GlobalKeyword.BeforeRenderMode`

Action applied before drawing the renderer list.

Options:

- `None`
- `Enable`
- `Disable`

### `GlobalKeyword.AfterRenderMode`

Action applied after drawing the renderer list.

Use this to restore state or set the next desired state.

If you enable a keyword before rendering, usually disable it after rendering unless another part of the frame intentionally needs it enabled.

## Depth Settings

### `Depth`

Enables a depth state override for this output.

When enabled and the output is exactly camera-sized, the pass also attaches the active camera depth texture.

Scaled camera outputs and custom-size outputs skip the active camera depth attachment. This avoids Render Graph errors caused by attaching a camera-depth texture whose dimensions do not match the generated color texture.

Why it matters:

- With depth testing, captured objects can respect scene occlusion.
- Without depth testing, the output may include objects regardless of what is in front of them.

Use `Depth` when the output should match visible camera surfaces. Keep `Camera Size Multiplier` at `1` when this output must use the active camera depth texture.

### `Write Depth`

Controls whether the pass writes to the camera depth attachment when `Depth` is enabled.

Usually keep this disabled.

Why:

- Most object-buffer outputs should not modify depth for later rendering.
- Writing depth can affect later passes in surprising ways.

Enable it only when the output pass is intentionally part of depth construction.

### `Depth Compare`

Depth comparison function used when `Depth` is enabled.

Common choices:

- `LessEqual`: normal visible-surface behavior.
- `Always`: ignores depth rejection.
- `Greater`: useful for specialized occlusion tests.

Choose this based on whether the output should represent visible surfaces, hidden surfaces, or all matching objects.

## Debug Settings

### `Debug View`

Adds a debug pass that draws this output texture to the camera.

Use it to verify that the producer texture contains what you expect.

Disable it for normal production use unless the debug image is intentionally part of the final frame.

### `Debug Display Mode`

Controls how the debug texture is drawn.

Modes:

- `Fullscreen`: displays the texture as the whole camera image.
- `Overlay`: tints the texture over the current camera color.

Use `Overlay` when you want to compare the mask against the scene.

Use `Fullscreen` when you want to inspect the texture alone.

### `Debug Color`

Tint and alpha used by overlay debug mode.

The alpha controls how strongly the debug texture appears over the scene.

### `Debug Render Pass Event`

Controls when the debug pass is drawn.

It should usually be later than the output pass so the texture already exists.

Use a late event when you want the debug view visible over the final camera color.

## RenderTexturePass Internals

### `FrameTextureRegistry`

`FrameTextureRegistry` is a Render Graph `ContextItem` shared by texture producers and consumers.

It is frame-local storage for generated texture handles.

It stores:

- a map from texture property id to a `TextureHandle` and its texel size

Every lookup is keyed. There is no ambiguous "last generated texture" in the production API, so the registry can safely hold multiple masks, distance fields, and internal color snapshots in one frame.

`RenderTexturePass.CustomTextureData` remains as a compatibility alias for older consumer scripts. New code should use `FrameTextureRegistry` or `FrameTextureResolver`.

Use:

```csharp
TryGetTexture(texturePropertyId, out TextureHandle texture, out Vector4 texelSize)
```

when writing a consumer pass.

### `Setup`

`Setup` copies settings into the pass and prepares pass state.

It sets:

- `renderPassEvent`
- `profilingSampler`
- depth `RenderStateBlock`
- requested pass input through `ConfigureInput`

This method is called every frame before the pass is enqueued, so inspector changes are reflected in the next frame.

### `RecordRenderGraph`

`RecordRenderGraph` declares the render pass to Unity's Render Graph.

It does not directly render immediately. Instead, it declares:

- the pass name
- pass data
- texture dependencies
- renderer list dependency
- color attachment
- optional depth attachment
- global texture export
- execute function

Render Graph later schedules and executes the pass.

### `InitPassData`

Builds the renderer list.

It gathers:

- `UniversalRenderingData`
- `UniversalCameraData`
- `UniversalLightData`

Then it creates:

- `DrawingSettings`
- `FilteringSettings`
- `RendererListHandle`

This is where most object-selection settings become actual renderer-list filters.

### `CreateDestinationTexture`

Creates the temporary texture that objects will be drawn into.

It starts from the camera target descriptor, then:

- applies `Color Format`
- removes depth bits
- disables MSAA
- applies camera/custom sizing
- applies filter and wrap mode

Depth is not stored inside this color texture. Depth is handled separately by attaching the active camera depth texture when requested.

The active camera depth texture is attached only when the generated color texture matches the camera size.

### `ApplyTextureSize`

Routes size handling based on `Texture Size Mode`.

Camera mode:

- starts from camera width and height
- applies `Camera Size Multiplier`
- clamps final size to at least `1x1`

Custom mode:

- uses `Texture Size`
- clamps final size to at least `1x1`
- disables dynamic scaling flags

### `SetDepthAttachment`

Attaches the active camera depth texture to the raster pass when possible.

It only does this when:

- `Depth` is enabled
- `Texture Size Mode` is `Camera`
- the generated output dimensions match the camera descriptor dimensions

It uses read access when `Write Depth` is disabled and read/write access when `Write Depth` is enabled.

Scaled camera outputs and custom-size outputs do not attach the active depth texture because depth and color dimensions may not match.

### `ExecutePass`

This is the function that actually runs on the raster command buffer.

It:

1. applies before-render global keyword actions
2. sets the texel-size global vector
3. clears the output texture to black
4. draws the renderer list
5. applies after-render global keyword actions

The clear-to-black behavior is important. It means the absence of drawn objects produces a predictable zero value.

## RenderTextureDebugPass Internals

`RenderTextureDebugPass` runs only when an output setting has `Debug View` enabled.

It:

1. looks up the texture from `FrameTextureRegistry`
2. reads that texture
3. writes to the active camera color texture
4. uses `RenderTextureDebug.shader`

The shader has two modes:

- pass `0`: fullscreen grayscale
- pass `1`: colored overlay

The debug pass is a diagnostic tool. It should not be required by consuming effects.

## RenderingHelpers Internals

`RenderingHelpers.CreateRendererListWithRenderStateBlock` creates a renderer list with a `RenderStateBlock`.

This is needed because the output pass can override depth behavior.

The helper creates the structures needed by `RendererListParams`:

- shader tag values
- render state blocks
- drawing settings
- filtering settings

Then it calls:

```csharp
renderGraph.CreateRendererList(param)
```

## Producer And Consumer Contract

A consumer pass must know:

- the texture name
- when the producer runs
- whether the texture is available through `FrameTextureRegistry`
- what data the texture contains
- what resolution/filtering to expect

The producer is responsible for:

- drawing the right objects
- using the expected material/output encoding
- exposing the texture under the agreed name
- producing the texture before the consumer reads it

The consumer is responsible for:

- using the same texture name
- declaring Render Graph texture reads when using handles
- interpreting the texture data correctly
- handling missing textures gracefully

## Setup Checklist

Use this checklist for any new effect built on top of this producer.

1. Add `ObjectsToRenderTextureFeature` to the URP renderer.
2. Add one element to `RenderTextureOutputSettings`.
3. Choose a unique `Texture Name`.
4. Decide which objects should be included.
5. Configure `Layer Mask` and/or `Render Layer Mask`.
6. Configure render queue bounds.
7. Assign an override `Material` if the output should be controlled data.
8. Choose `Texture Size Mode`.
9. Choose `Filter Mode` and `Wrap Mode`.
10. Decide whether depth testing is needed.
11. Enable `Debug View` temporarily and verify the output.
12. Add or configure the consuming pass.
13. Make sure the consumer runs after the producer.
14. Disable `Debug View` for production.

## Troubleshooting

### Output Texture Is Black

Check:

- the object is visible to the active camera
- the object layer is included in `Layer Mask`
- the renderer's rendering layer is included in `Render Layer Mask`
- the material render queue is within the configured range
- the shader pass has a matching `LightMode`
- custom shader tags are listed in `Shader Tags`
- the override `Material` is valid
- `Material Pass Index` points to an existing pass
- `Depth Compare` is not rejecting the object

Enable `Debug View` to inspect the producer output directly.

### Output Contains Too Many Objects

Narrow the filters:

- reduce `Layer Mask`
- reduce `Render Layer Mask`
- narrow render queue bounds
- use a more specific shader tag setup
- use a separate renderer layer or GameObject layer for effect membership

### Consumer Cannot Find The Texture

Check:

- the producer feature is present in the renderer
- the producer has a non-null output settings entry
- `Texture Name` is not empty
- producer and consumer use the exact same texture name
- the producer pass event is before the consumer pass event
- the renderer feature order puts the producer before the consumer when needed
- the consumer asks `FrameTextureRegistry` for the correct property id

### Depth Does Not Behave As Expected

Check:

- `Depth` is enabled
- `Render Pass Input` requests `Depth` when needed
- `Depth Compare` matches the intended visibility behavior
- `Texture Size Mode` is `Camera`
- `Camera Size Multiplier` is `1`
- `Write Depth` is disabled unless intentionally needed
- camera depth is available for the selected render event

### Transparent Objects Are Missing Or Incorrect

Check:

- render queue upper bound includes the transparent queue range
- sorting criteria matches transparent rendering
- render pass event is late enough
- the transparent shader has a matching pass tag

Transparent captures can be order-dependent. Prefer opaque proxy renderers or dedicated mask materials when possible.

### Custom Shader Objects Are Missing

Check the shader pass `LightMode` tag.

If the shader does not use a standard URP tag selected by `Light Mode`, add the custom tag string to `Shader Tags`.

### Texture Edges Are Too Pixelated

Try:

- use camera-size output
- increase `Camera Size Multiplier`
- avoid very small custom texture sizes
- use `Bilinear` filtering when soft sampling is acceptable
- adjust the consuming shader's sampling logic

### Texture Is Too Expensive

Try:

- fewer output entries
- lower `Camera Size Multiplier`
- smaller custom texture size
- narrower object filters
- simpler override materials
- avoid unnecessary `Render Pass Input` flags
- disable `Debug View`

## Production Guidance

- Keep texture names unique and stable.
- Keep producer and consumer feature order explicit.
- Prefer handle-based Render Graph reads in custom consumers.
- Use global texture names for shader-side access.
- Use debug view while authoring, then disable it.
- Keep `Write Depth` disabled unless there is a clear reason.
- Prefer simple override materials for masks and data buffers.
- Avoid transparent captures unless they are truly needed.
- Document each consumer's expected texture name and encoding in that consumer's own README.

## Limitations

- The output is based on the active camera culling results.
- The feature does not render from another viewpoint.
- The generated textures are temporary frame resources.
- Scaled and custom-size outputs do not attach active camera depth.
- Depth behavior depends on render event, depth availability, and URP configuration.
- Consumers must run after the producer.
- Global shader keyword changes affect global shader state and should be used carefully.
