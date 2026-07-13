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

> **Version scope:** this implementation and the API explanations below target Unity 6 with URP's Render Graph path. The project currently uses Unity `6000.3` and a corresponding URP package. Older URP compatibility-mode tutorials use different APIs such as `Execute`, `CommandBuffer.GetTemporaryRT`, and `cameraColorTarget`; those APIs are not the model used by this feature.

This document has two reading paths:

- Read **What This Feature Is For** through **Output Settings Reference** when configuring the feature in a renderer asset.
- Read **Detailed Source Walkthrough And Unity API Reference** when learning how the C# implementation works or when writing another producer/consumer pass.

This core README intentionally avoids effect-specific setup. Individual effects should keep their own README files with their own exact material names, texture names, feature ordering, and tuning values.

For optional single-pass processing of a generated texture, use `FrameTextureProcessingFeature`. It reads and writes named `FrameTextureRegistry` entries without coupling post-processing materials to this producer.

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
- `ObjectsToRenderTextureFeature.Validation.cs`: editor/lifecycle validation for output settings.
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
8. When `Texture Exposure` includes a global texture, exposes it globally using `Texture Name`.
9. Only when the selected mode includes texel size, sets a matching global texel-size vector.

## Data Flow

The generated texture is always stored in `FrameTextureRegistry`. Either global-texture mode can additionally expose it to ordinary shaders, while only the full shader-globals mode publishes `<TextureName>_TexelSize`.

### Global Shader Texture

The pass calls:

```csharp
builder.SetGlobalTextureAfterPass(destination, texturePropertyId);
```

When either global-texture exposure mode is enabled, that makes the texture available through the global shader property named by `Texture Name`.

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

- `-1`: use this feature's all-pass convention for the override material
- `0`: render only pass 0
- `1` or higher: render that specific pass index

Prefer `0` for a production one-pass mask material because the intent is explicit. Use `-1` only when every pass in the override material is intentionally part of the capture. Use a specific nonzero index when a multi-pass shader has one dedicated output pass.

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

Registry key and optional global shader property name used for the output texture.

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

### `Texture Exposure`

Controls how consumers can access the generated texture. It does not change which objects are rendered or what pixels are written into the texture.

The producer always executes this registration:

```csharp
textureRegistry.SetTexture(
    texturePropertyId,
    destination,
    texelSize);
```

Therefore all exposure modes support `FrameTextureRegistry`. The differences are whether the same texture is also published to Unity's global shader-property table and whether matching texel-size metadata is published globally.

#### `Frame Registry Only`

This mode exposes the texture only through the frame-local `FrameTextureRegistry` used by C# Render Graph passes.

What it provides:

- the generated `TextureHandle`
- the texture's `(1 / width, 1 / height, width, height)` texel-size vector
- a stable lookup key produced from `Texture Name`
- an explicit Render Graph resource that consumers declare with `UseTexture`

What it does not provide:

- a global shader texture visible automatically to every material
- a global `<TextureName>_TexelSize` shader vector
- a persistent texture that remains valid after the current camera/frame graph
- CPU access to the texture's pixels

A typical C# consumer uses `FrameTextureResolver`:

```csharp
private readonly FrameTextureResolver _resolver =
    new(nameof(MyRendererFeature));

_resolver.SetTextureName("_ObjectMask");

if (!_resolver.TryResolve(
        frameData,
        out TextureHandle mask,
        out Vector4 maskTexelSize))
{
    return;
}

builder.UseTexture(mask, AccessFlags.Read);
```

The call to `UseTexture` is important. Looking up a handle is not enough by itself; the consumer must declare how it uses the resource so Render Graph can establish producer-before-consumer ordering and manage the texture lifetime.

Use this mode for the package's C# effects, including:

- `MaskOutlineFeature`
- `MaskHaloFeature`
- `LayerBlurFeature`
- `JumpFloodDistanceFieldFeature`
- `DistanceFieldOutlineFeature`
- `MaskDistortionFeature`
- `FrameTextureProcessingFeature`
- `RenderTextureDebugPass`

Advantages:

- dependencies are explicit to Render Graph
- unused producer work can be culled when there are no other side effects
- no global texture-name collision with unrelated shaders
- no global texel-size command
- multiple named frame textures can coexist cleanly
- consumer code receives dimensions together with the handle

Disadvantages:

- ordinary scene materials cannot sample the texture merely by declaring the same property name
- a C# consumer pass is required to resolve and bind the texture
- the handle is valid only for the current frame's Render Graph
- producer and consumer order still must be correct

Recommended setup for the package effects:

```text
Texture Name: _ObjectMask
Texture Exposure: Frame Registry Only
Global Shader Keywords: Empty
```

Set the consuming feature's mask/source texture name to `_ObjectMask` exactly.

#### `Frame Registry + Global Texture`

This mode keeps the registry entry and publishes only the texture through Unity's Render Graph-aware API:

```csharp
builder.SetGlobalTextureAfterPass(
    destination,
    texturePropertyId);
```

It does not execute a command-buffer global vector update, so this mode does not require `AllowGlobalStateModification(true)` when global keyword actions are empty.

Use this mode when an ordinary material or Shader Graph needs the texture but does not require a globally published `<TextureName>_TexelSize` vector.

Advantages:

- follows Unity's tracked global-texture publication path
- ordinary shaders can sample the texture by name
- retains registry access for C# Render Graph consumers
- avoids command-buffer global-state permission when no global keywords are active
- allows Render Graph to cull the producer if no declared consumer or other side effect needs it

Disadvantages:

- shaders expecting `<TextureName>_TexelSize` do not receive it from this feature
- global property names can collide with unrelated systems
- shader/pass ordering still determines when this frame's texture becomes available

This is the recommended global mode when the texture alone is sufficient.

#### Executable Exposure Proof

The `TextureExposureProof` folder contains two strict diagnostic consumers that use one producer texture named `_TextureExposureProofMask`:

- `GlobalTextureProofFeature` requires the `GlobalTexture` capability and draws a green mask fill.
- `GlobalTextureAndTexelSizeProofFeature` requires both `GlobalTexture` and `GlobalTexelSize` and draws a yellow one-texel edge.

Neither feature falls back to the registry's `TextureHandle`. They use `UseGlobalTexture` and direct shader-global declarations, so they provide an executable A/B test for the exposure modes. See `TextureExposureProof/README.md` for the complete setup and expected result matrix.

#### `Frame Registry + Global Texture + Texel Size`

The backing enum member for this Inspector option is `FrameRegistryAndShaderGlobals`. It remains serialized value `0` so existing renderer assets preserve their previous behavior.

This mode keeps the registry entry and additionally publishes the output as a global shader texture:

```csharp
builder.SetGlobalTextureAfterPass(
    destination,
    texturePropertyId);
```

It also publishes:

```text
<TextureName>_TexelSize
```

For `_ObjectMask`, ordinary shaders can receive:

```hlsl
TEXTURE2D_X(_ObjectMask);
float4 _ObjectMask_TexelSize;
```

The texture is available after the producer pass has executed. A shader that runs before the producer cannot read this frame's result. Do not retain or rely on the transient Render Graph texture in a later frame.

Use this mode when:

- a regular scene material directly samples the output by property name
- Shader Graph directly samples a global texture property
- a third-party shader expects a named global mask or buffer
- adding a dedicated C# consumer pass is not appropriate

Advantages:

- ordinary materials can sample the output without knowing about `FrameTextureRegistry`
- Shader Graph and hand-written shaders can share the same named texture
- existing shader APIs that expect a global texture remain compatible
- C# Render Graph consumers can still use the registry

Disadvantages:

- the global texel-size command requires `AllowGlobalStateModification(true)`
- global state introduces a Render Graph synchronization point
- a pass that allows global-state modification cannot be culled
- later passes cannot be reordered before that synchronization point
- common names can collide with global properties owned by other systems
- shader execution order becomes part of the texture contract

This full shader-global mode is the default so renderer assets created before `Texture Exposure` was added preserve their previous behavior.

#### Exposure Mode Comparison

| Question | Frame Registry Only | Registry + Global Texture | Registry + Global Texture + Texel Size |
| --- | --- | --- | --- |
| Registered in `FrameTextureRegistry`? | Yes | Yes | Yes |
| Usable by C# Render Graph effects? | Yes | Yes | Yes |
| Automatically visible to ordinary shaders? | No | Yes | Yes |
| Publishes global `<TextureName>_TexelSize`? | No | No | Yes |
| Requires global state when keywords are empty? | No | No | Yes |
| Can be culled when no consumer or side effect exists? | Yes | Yes | No |
| Recommended use | Package/C# effects | Shader access to texture only | Shaders requiring texture and texel metadata |

`Texture Exposure` and `Global Shader Keywords` are independent settings. `Frame Registry Only` means the texture is registry-only; it does not prohibit a separately configured global keyword change. If active global keyword actions are configured, the pass must still allow global-state modification even in registry-only mode.

## Shader Pass Filtering

Shader pass filtering answers this question:

```text
Which Pass inside each selected object's shader is eligible for this draw?
```

It is separate from layers, render queues, rendering layers, shader keywords, and texture exposure.

The complete selection pipeline is approximately:

```text
camera culling
  -> GameObject Layer Mask
  -> Rendering Layer Mask
  -> render queue bounds
  -> matching LightMode shader pass
  -> optional override material/pass
  -> draw into the generated texture
```

An object can pass every layer and queue test and still be absent when none of its shader passes matches the configured `LightMode` names.

### `Light Mode`

Flags for common shader `LightMode` tags.

`LightMode` is a ShaderLab Pass tag. URP uses it to identify the purpose of an individual pass inside a shader:

```shaderlab
Pass
{
    Name "ForwardLit"
    Tags { "LightMode" = "UniversalForward" }

    HLSLPROGRAM
    // Vertex and fragment programs.
    ENDHLSL
}
```

The renderer list receives a list of `ShaderTagId` values created from the selected flags. A renderer is drawable only when Unity can use a compatible shader pass from that list.

`Standard` includes common URP forward/unlit tags:

- `SRPDefaultUnlit`
- `UniversalForward`
- `UniversalForwardOnly`
- `LightweightForward`

The available flags have these intended roles:

| Light Mode | Typical meaning |
| --- | --- |
| `SRPDefaultUnlit` | Untagged/default unlit or extra SRP pass; URP treats a pass without `LightMode` as this value |
| `UniversalForward` | Normal URP forward geometry pass with lighting |
| `UniversalForwardOnly` | Forward-only geometry pass usable in forward and deferred renderers |
| `LightweightForward` | Legacy Lightweight/early URP forward tag retained for compatible shaders |
| `DepthOnly` | Camera-space depth-only pass |
| `DepthNormals` | Depth-and-normal pass name used by compatible URP/custom shaders |
| `DepthNormalsOnly` | URP depth-and-normal prepass, particularly relevant to deferred rendering and SSAO |

`Standard` is the safest default for color or flat-mask capture because it covers common visible geometry passes. Depth and normal tags should be selected only when the desired shader pass really writes useful data to this output. A depth-only pass is not automatically a good color-mask pass.

Why it matters:

If an object has the correct layer and render queue but still does not draw, its shader may not expose a pass with one of the selected `LightMode` tags.

When to change it:

- enable `DepthOnly` when intentionally drawing a shader's depth pass
- enable a depth-normal tag when intentionally capturing normals/depth data
- disable broad forward tags when a narrowly controlled custom pass should be the only eligible pass
- keep `Standard` for ordinary URP Lit, Simple Lit, and Unlit objects

When not to change it:

- do not use it as an object category system; use layers or rendering layers
- do not add tags merely because their names appear in a shader
- do not expect it to enable a shader feature; use material properties or keywords for shader variants
- do not add `ShadowCaster` expecting a camera-space object mask; shadow passes use shadow-rendering assumptions

### `Shader Tags`

Extra custom `LightMode` tag values appended to the built-in `Light Mode` selection.

This field expects the value on the right side of a Pass tag:

```shaderlab
Tags { "LightMode" = "ObjectMask" }
```

The corresponding Inspector value is:

```text
Shader Tags
  Element 0: ObjectMask
```

Do not enter:

- the shader asset path
- the shader's `Name`
- the Pass `Name`
- `RenderType`
- `Queue`
- the complete text `LightMode=ObjectMask`

Use this for custom shaders that use custom pass tags.

How to use it:

1. Inspect the shader pass that should draw into this output.
2. Find its `LightMode` tag.
3. Add that tag string to `Shader Tags`.
4. Keep the spelling and capitalization exact.
5. Confirm that layers, rendering layers, and render queue bounds also include the object.
6. Enable the producer's debug view and verify the result.

This supplements `Light Mode`; it does not replace it.

Example custom capture pass:

```shaderlab
Pass
{
    Name "ObjectMaskPass"
    Tags { "LightMode" = "ObjectMask" }

    ZWrite Off
    ZTest LEqual

    HLSLPROGRAM
    #pragma vertex Vert
    #pragma fragment Frag

    half4 Frag(Varyings input) : SV_Target
    {
        return half4(1, 1, 1, 1);
    }
    ENDHLSL
}
```

For a tightly controlled custom-pass workflow, set `Light Mode` to `None` and add only `ObjectMask`. For general URP materials, retain `Standard` and add custom tags only for additional shader families.

Advantages of custom shader tags:

- precisely selects a pass designed for the generated texture
- avoids relying on the visual forward pass to encode mask/data output
- works with custom shader architectures
- can reduce ambiguity when one shader contains many passes

Disadvantages of custom shader tags:

- creates a string contract between the renderer feature and shader source
- a typo or renamed tag makes affected objects disappear silently from the output
- every shader family that needs capture support must provide a compatible pass
- an overly broad built-in selection can still make another pass eligible

Troubleshooting a missing object:

1. Confirm that the camera culls the object as expected.
2. Confirm `Layer Mask`.
3. Confirm `Render Layer Mask`.
4. Confirm render queue bounds.
5. Confirm that the shader contains a matching `LightMode` pass.
6. Confirm `Material Pass Index` when an override material is assigned.
7. Confirm the pass event occurs before the consuming effect.

### Shader Tags Versus Shader Keywords

These settings solve different problems:

| Setting | Selects or changes | Example |
| --- | --- | --- |
| `Light Mode` / `Shader Tags` | Which shader Pass Unity draws | Select the Pass tagged `ObjectMask` |
| `Global Shader Keywords` | Which compiled variant of an eligible Pass runs | Enable `OBJECT_MASK_CAPTURE` |

A shader tag cannot enable keyword-controlled code. A keyword cannot make an ineligible `LightMode` pass enter the renderer list.

## Global Keyword Settings

### `Global Shader Keywords`

Optional list of global shader keyword changes applied before and after this output renders.

A shader keyword selects conditional shader behavior. For variant keywords, Unity compiles different shader programs and selects a matching variant from the enabled keyword state.

A hand-written shader might declare a global keyword like this:

```hlsl
#pragma multi_compile _ OBJECT_MASK_CAPTURE

half4 Frag(Varyings input) : SV_Target
{
#if defined(OBJECT_MASK_CAPTURE)
    return half4(1, 1, 1, 1);
#else
    return EvaluateNormalSurface(input);
#endif
}
```

This feature can enable `OBJECT_MASK_CAPTURE` before drawing its renderer list and disable it afterward.

Do not declare the keyword with a `_local` directive for this field:

```hlsl
#pragma shader_feature_local OBJECT_MASK_CAPTURE
```

Global keyword commands do not control a local keyword with the same name. Prefer local keywords for ordinary per-material features, but use a genuinely global declaration when this pass must drive the variant globally.

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
- Active global keyword commands require `AllowGlobalStateModification(true)`.
- The global-state declaration prevents pass culling and restricts Render Graph scheduling.
- Every variant keyword can contribute to shader variant count and build complexity.

Prefer separate materials, material properties, local shader keywords, or shader variants when possible.

Good reasons to use this setting:

- a shader already provides a global capture variant that must be active only for this draw
- a third-party shader exposes the needed behavior only through a global keyword
- multiple materials rendered by the same output must enter the same global variant without mutating every material asset
- the keyword state must be encoded in GPU command order immediately around `DrawRendererList`

Poor reasons to use this setting:

- producing a normal white mask when an override mask material can do it directly
- controlling one material that can use a local keyword or material property
- making the output texture globally accessible; use `Texture Exposure` for that
- selecting which Pass Unity draws; use `Light Mode` or `Shader Tags`
- selecting which objects are included; use layers, rendering layers, and queues

### `GlobalKeyword.Name`

The keyword string to change.

Keep it exactly equal to the global keyword name declared by the shader. Names are case-sensitive contracts. Empty and whitespace-only names are ignored by this implementation.

### `GlobalKeyword.Disabled`

Skips this entry without removing it from the list.

Use it to temporarily disable one keyword action while preserving the configuration.

A disabled entry does not count as active global-state modification.

### `GlobalKeyword.BeforeRenderMode`

Action applied before drawing the renderer list.

Options:

- `None`
- `Enable`
- `Disable`

Examples:

| Before mode | State used by renderer-list draws |
| --- | --- |
| `None` | Whatever state was already active |
| `Enable` | Keyword is enabled before drawing |
| `Disable` | Keyword is disabled before drawing |

### `GlobalKeyword.AfterRenderMode`

Action applied after drawing the renderer list. It determines the state seen by later rendering commands.

Important: this setting does not capture or remember the previous keyword state. It applies the exact action selected.

For example, `Before = Enable` and `After = Disable` guarantees that this renderer-list draw sees the keyword enabled and later commands see it disabled. If the keyword was already enabled before this pass, this combination does not restore that earlier enabled state; it still disables the keyword afterward.

Use `After = None` only when another system intentionally owns the later state. Leaving a keyword enabled can affect later passes, later renderer features, and subsequent camera rendering until another command changes it.

Recommended isolated transition:

```text
Name: OBJECT_MASK_CAPTURE
Disabled: false
Before Render Mode: Enable
After Render Mode: Disable
```

### When A Keyword Entry Is Active

An entry causes global-state modification only when all of these are true:

- the array element exists
- `Disabled` is false
- `Name` is not empty or whitespace
- either `Before Render Mode` or `After Render Mode` is not `None`

An empty array, disabled entries, unnamed entries, or entries with both actions set to `None` do not require global keyword commands.

### Shader Variant And Build Considerations

Changing a keyword at runtime does not create a missing shader variant. The required variant must survive shader compilation and build stripping.

Practical rules:

- use `multi_compile` when a global runtime state must reliably select both variants
- use `shader_feature` carefully because variants unused by build-time materials can be stripped
- keep keyword sets small because combinations multiply variant counts
- use stage-specific directives such as `multi_compile_fragment` when only one stage needs the keyword and the target graphics APIs benefit
- verify a development player build, not only the Editor
- use strict shader-variant matching or shader-variant logging when diagnosing missing variants

### Global Keyword Advantages And Disadvantages

| Advantages | Disadvantages |
| --- | --- |
| Changes a variant at an exact point in GPU command order | Affects global shader state rather than one material |
| Can control many rendered materials consistently | Requires Render Graph global-state permission |
| Avoids editing every material instance | Prevents pass culling and adds a scheduling constraint |
| Supports shader-authored capture variants | Can leak into later rendering when the after action is wrong |
| Useful for compatible third-party shader contracts | Adds variant-management and stripping risk |

### Keyword Troubleshooting

If enabling a keyword appears to do nothing:

1. Confirm the name and capitalization.
2. Confirm the shader declares a global keyword, not a `_local` keyword.
3. Confirm the selected `LightMode` pass contains the keyword declaration.
4. Confirm the required variant was not stripped from the player build.
5. Confirm the before action executes before the renderer-list draw.
6. Confirm an override material is not replacing the shader you expected to receive the keyword.
7. Inspect the Frame Debugger or Render Graph Viewer to verify pass order.

If unrelated rendering changes:

1. Check whether `After Render Mode` is `None`.
2. Check whether another feature uses the same global keyword name.
3. Use a unique package/project prefix for custom global keywords.
4. Prefer an override material or local material keyword when global scope is unnecessary.

## Why Global State Is Conditional

The producer records this logic:

```csharp
if (passData.PublishGlobalTexture)
{
    builder.SetGlobalTextureAfterPass(
        destination,
        passData.TexturePropertyId);
}

if (passData.PublishGlobalTexelSize ||
    Settings.HasActiveGlobalKeywordChanges(passData.GlobalKeywords))
{
    builder.AllowGlobalStateModification(true);
}
```

### First Condition: Publish The Texture

`SetGlobalTextureAfterPass` is Render Graph's explicit API for binding a generated `TextureHandle` to a global shader property after the producer has written it.

It is called for both global-texture modes. Registry-only consumers already receive the handle through `FrameTextureRegistry` and do not need a global binding.

`SetGlobalTextureAfterPass` itself is declared to Render Graph and does not require `AllowGlobalStateModification(true)`.

### Second Condition: Permit Command-Buffer Global Changes

`AllowGlobalStateModification(true)` declares that commands recorded by `ExecutePass` modify state outside the pass's normal attachments and resources.

There are two possible command-buffer side effects in this implementation:

1. Shader-global exposure executes `SetGlobalVector` for `<TextureName>_TexelSize`.
2. Active keyword entries execute `EnableShaderKeyword` or `DisableShaderKeyword`.

That is why the condition uses logical OR:

```text
publish global texel size OR execute active global keyword changes
```

The behavior matrix is:

| Texture exposure | Active keyword action | Global texture | Global texel size | Allow global state |
| --- | --- | --- | --- | --- |
| Frame Registry Only | No | No | No | No |
| Frame Registry Only | Yes | No | No | Yes |
| Registry + Global Texture | No | Yes | No | No |
| Registry + Global Texture | Yes | Yes | No | Yes |
| Registry + Global Texture + Texel Size | No | Yes | Yes | Yes |
| Registry + Global Texture + Texel Size | Yes | Yes | Yes | Yes |

### Is The Code Correct?

Yes, it is correct for the current `ExecutePass` implementation.

It satisfies these rules:

- global texture publication occurs only in modes that promise global texture access
- registry-only mode avoids global texture and texel-size publication
- every `SetGlobalVector`, `EnableShaderKeyword`, and `DisableShaderKeyword` path has declared global-state permission
- no global-state permission is requested for registry-only or global-texture-only mode when there is no active keyword action
- `SetGlobalTextureAfterPass` remains in recording code rather than being issued as an unsafe command-buffer texture binding

The two conditions must remain synchronized with `ExecutePass`. If the global texel-size `SetGlobalVector` call is removed in the future, `PublishGlobalTexelSize` would no longer need to participate in the second condition. If another command-buffer global operation is added, its activation condition must also be included.

Do not replace the second condition with only `HasActiveGlobalKeywordChanges(...)` while `SetGlobalVector` still runs. Doing so would execute an undeclared global-state command whenever the texel-size mode is selected.

The cost of `AllowGlobalStateModification(true)` is why the code derives it automatically rather than exposing a manual checkbox. In this Unity version it introduces a graph synchronization point, prevents later passes from moving before the pass, and disables pass culling.

## Configuration Validation

`ObjectsToRenderTextureFeature` validates its output list in two lifecycle locations:

```text
OnValidate() -> when serialized values change in the Unity Editor
Create()     -> when URP creates or recreates the renderer feature
```

Validation is intentionally not performed in full inside `AddRenderPasses`, because that method runs per camera and should remain a small scheduling path. `AddRenderPasses` keeps one inexpensive runtime guard: null settings and empty texture names are skipped.

### Validation Rules

| Condition | Severity and behavior |
| --- | --- |
| Settings element is null | Warning; output is skipped |
| `Texture Name` is empty | Warning; output is skipped |
| Duplicate `Texture Name` | Warning; both entries remain configured, but registry/global keys collide and must be fixed |
| Unknown serialized exposure enum | Warning; runtime uses the backward-compatible global texture plus texel-size policy |
| Queue lower bound is greater than upper bound | Warning; configured range cannot represent the intended filter |
| Custom texture width or height is non-positive | Warning; runtime clamps each invalid dimension to 1 pixel |
| `Light Mode` is `None` and no valid custom Shader Tag exists | Warning; no shader pass can be selected |
| Active keyword action has no name | Warning; entry is ignored |
| Active keyword name is duplicated | Warning; actions execute in list order and may conflict |
| Keyword changes before rendering but has no after action | Warning; changed state can leak into later passes/cameras |
| Registry-only or global-texture-only mode has active global keyword actions | Warning; valid configuration, but keywords still disable pass culling through global state |

Warnings describe consequences but do not silently rewrite legitimate user settings. The only automatic behavior is the documented runtime fallback or safety clamp.

### Warning Deduplication

Validation messages are cached by the feature instance. `Create()` does not repeat messages already reported by `OnValidate()`. When an Inspector edit triggers `OnValidate`, the cache is cleared so the current configuration is checked again and a reintroduced problem can be reported.

No warning cache is static, so separate renderer features validate independently.

### Exposure Validation By Construction

The enum makes unsupported publication combinations impossible:

```text
Frame Registry Only
  -> no global texture, no global texel size

Frame Registry + Global Texture
  -> global texture, no global texel size

Frame Registry + Global Texture + Texel Size
  -> global texture and global texel size
```

There is no mode that publishes global texel size without publishing its corresponding global texture.

The pass converts the enum into a private immutable policy containing:

```csharp
bool PublishGlobalTexture;
bool PublishGlobalTexelSize;
```

Render Graph declarations and execution commands both use this same policy. This prevents the Inspector choice, `SetGlobalTextureAfterPass`, `SetGlobalVector`, and `AllowGlobalStateModification` from drifting apart.

### What Static Validation Cannot Prove

The feature cannot determine all cross-asset contracts automatically. Users must still verify:

- the consuming feature uses the exact same texture name
- the producer runs before the consumer
- a global shader declares the expected texture property
- a global shader that needs pixel offsets obtains texel size from the selected mode or computes dimensions itself
- a keyword is declared globally rather than with a `_local` directive
- required shader variants survive player-build stripping
- selected objects actually contain a compatible `LightMode` pass

Use the debug view, Frame Debugger, Render Graph Viewer, development builds, and shader variant logging for these runtime relationships.

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

## Detailed Source Walkthrough And Unity API Reference

This section explains the implementation in execution order. For each important Unity or URP API it answers four questions:

1. What does the API represent?
2. Why does this feature call it?
3. What happens if the call or setting is removed?
4. When should another renderer feature use or avoid it?

### The Three Different Kinds Of Work

The code becomes easier to understand when it is divided into three layers.

#### Feature scheduling

`ObjectsToRenderTextureFeature` is a `ScriptableRendererFeature`. It owns settings and reusable pass instances. It decides which passes URP should enqueue for a camera.

This layer does not issue draw commands.

#### Render Graph recording

`RenderTexturePass.RecordRenderGraph` declares resources, dependencies, attachments, and the function that will issue commands.

Recording describes future GPU work. A call such as `builder.SetRenderAttachment` does not immediately bind a render target on the GPU.

#### Raster command generation

`ExecutePass` runs through the render function registered with `SetRenderFunc`. It receives a `RasterGraphContext` and records actual raster commands such as clear and draw.

Keeping these phases separate prevents a common misunderstanding:

```text
AddRenderPasses        -> choose and enqueue work
RecordRenderGraph      -> declare work and dependencies
ExecutePass            -> issue the commands for that work
```

### Per-Camera Lifecycle

For each camera rendered by a renderer containing this feature, the practical flow is:

```text
Renderer feature is created or deserialized
  -> Create()
      -> allocate/reuse C# pass objects

Each camera frame
  -> AddRenderPasses(...)
      -> Setup(...) each configured output pass
      -> EnqueuePass(...)
      -> optionally enqueue a debug pass

URP records the camera Render Graph
  -> RecordRenderGraph(...)
      -> create renderer list
      -> create destination texture
      -> declare reads, writes, and attachments
      -> register output handle
      -> assign ExecutePass

URP compiles and executes the graph
  -> ExecutePass(...)
      -> update required global state
      -> clear destination
      -> draw renderer list
```

`AddRenderPasses` runs per camera, not once per game. A scene view, preview camera, reflection camera, camera stack, and game camera can therefore each invoke the feature. The current implementation does not filter camera types. Add an early camera check in `AddRenderPasses` if an effect should only run for game cameras.

### `ObjectsToRenderTextureFeature` Class Declaration

```csharp
public partial class ObjectsToRenderTextureFeature : ScriptableRendererFeature
```

`ScriptableRendererFeature` is URP's extension point for inserting one or more `ScriptableRenderPass` instances into a renderer.

The class is partial so scheduling and validation remain in separate source files while Unity still sees one renderer-feature type. `partial` changes source organization only; it creates no extra component, renderer feature, or runtime object.

Why it is used:

- the feature appears in a Universal Renderer asset
- Unity serializes its public settings in that asset
- URP calls its lifecycle methods
- it can enqueue custom passes for each camera

If the class did not inherit from `ScriptableRendererFeature`, it would be an ordinary C# class. It would not appear under **Add Renderer Feature**, and URP would never call `Create` or `AddRenderPasses`.

Use a renderer feature when work must be integrated into URP's camera frame. Do not use one for gameplay-only logic or an operation that can be expressed entirely inside an existing object shader.

### `DebugShaderName`

```csharp
private const string DebugShaderName =
    "Hidden/RenderTextureFeature/DebugTexture";
```

This is the ShaderLab name, not a file path. `Hidden/` keeps the shader out of the normal material shader menu while still allowing code to locate it.

The constant avoids repeating a string and prevents accidental mismatch between material creation calls.

If the shader name does not match the shader's declared name, `CoreUtils.CreateEngineMaterial` returns no usable material and debug visualization is skipped. Normal output generation still works because the debug material is not part of the producer pass.

### `FormerlySerializedAs`

```csharp
[FormerlySerializedAs("RenderTextureOutputs")]
[FormerlySerializedAs("TextureSettings")]
public List<RenderTexturePass.Settings> RenderTextureOutputSettings;
```

`FormerlySerializedAs` tells Unity that an older serialized field name now maps to this field. It protects renderer assets and prefabs from losing values when a C# field is renamed.

Why it is used here:

- the list was renamed during development
- existing renderer assets may still contain an older YAML field name
- Unity can migrate those values into the current field

Without these attributes, assets saved with an old name would not populate the new field. Unity would use the new field's default value, making it look as if configuration had been erased.

Use this attribute when renaming a serialized field. Do not keep adding aliases for names that were never released or serialized anywhere, because unnecessary migration metadata makes ownership harder to understand.

### Reusable Pass Lists

```csharp
private readonly List<RenderTexturePass> _renderPasses = new();
private readonly List<RenderTextureDebugPass> _debugPasses = new();
```

The feature stores one pass object per settings entry and reuses it across frames.

Why this matters:

- `AddRenderPasses` is called per camera
- allocating a new pass every call creates managed garbage
- stable pass instances retain cached property IDs and profiling samplers

`EnsurePassCount` grows the pools only when the settings list becomes larger. It does not shrink them because retaining a few lightweight C# pass objects is cheaper and simpler than repeated destruction/recreation while editing the list.

If pass objects were created every frame, rendering could produce avoidable garbage collection pressure. If one pass instance were shared by every list entry in the same frame, later calls to `Setup` would overwrite the settings needed by earlier enqueued entries.

### `Create()`

```csharp
public override void Create()
{
    EnsurePassCount(GetRenderTextureOutputSettingsCount());
}
```

URP calls `Create` when the feature is created and whenever serialization causes it to be recreated.

This method prepares reusable C# objects. It does not create frame textures because frame textures depend on the active camera and only exist while a Render Graph is being recorded.

If `Create` did nothing, `AddRenderPasses` could still recover because it also calls `EnsurePassCount`, but the first camera after a settings change might have to grow the pass lists. Keeping steady-state allocation out of the per-camera path is preferable.

Use `Create` for pass instances and resources whose lifetime matches the renderer feature. Do not store a current-frame `TextureHandle` here; handles are valid only for their frame's Render Graph.

### `AddRenderPasses(...)`

```csharp
public override void AddRenderPasses(
    ScriptableRenderer renderer,
    ref RenderingData renderingData)
```

URP calls this method while building the pass sequence for a camera.

Parameters:

- `ScriptableRenderer renderer`: the active renderer accepting custom passes.
- `RenderingData renderingData`: camera/frame information available during renderer setup.

The current implementation does not read `renderingData`, but the parameter is still required by the override. It is the correct place to filter cameras, for example by `renderingData.cameraData.cameraType`.

The method performs these steps:

1. Count configured outputs.
2. Return immediately when there are none.
3. Ensure one pass object exists per list index.
4. Ignore null settings elements.
5. Configure each output pass with `Setup`.
6. Enqueue each output pass.
7. Optionally configure and enqueue a debug pass.

An early return is useful because an empty feature should add no Render Graph work.

Avoid expensive searches, asset loading, or repeated material construction in `AddRenderPasses`; it is a per-camera method. The debug material is lazily created only once when debug output is first requested.

### `ScriptableRenderer.EnqueuePass`

```csharp
renderer.EnqueuePass(renderPass);
```

`EnqueuePass` submits the pass object to the active renderer's pass queue. URP later orders queued passes primarily by `renderPassEvent`.

If this call is removed:

- `Setup` may still run
- no `RecordRenderGraph` call occurs for that pass
- no texture is created
- no objects are drawn
- consumers cannot resolve the output

When multiple passes use the same `RenderPassEvent`, enqueue order matters. In practice, renderer-feature order and list order define the intended producer-before-consumer sequence.

### `CoreUtils.CreateEngineMaterial`

```csharp
_debugMaterial = CoreUtils.CreateEngineMaterial(DebugShaderName);
```

`CreateEngineMaterial` finds the named shader and creates a runtime engine material suitable for SRP utility rendering.

It is preferable to raw `new Material(Shader.Find(...))` for this internal material because Core RP centralizes the expected hide flags and engine-resource behavior.

The material is only needed for the optional debug pass. Creating it lazily avoids owning a material when every output has `Debug View` disabled.

Do not use this string-based pattern for an essential production shader unless its build inclusion is guaranteed. A serialized material reference is generally safer for effect shaders because the asset reference helps shader stripping systems see the dependency.

### `Dispose(bool)` And `CoreUtils.Destroy`

```csharp
protected override void Dispose(bool disposing)
{
    CoreUtils.Destroy(_debugMaterial);
}
```

Renderer features can outlive individual frames. Any material they create must be destroyed when the feature is disposed.

`CoreUtils.Destroy` chooses the appropriate Unity destruction behavior for editor and player contexts.

If the material were not destroyed, repeated renderer reloads, script recompiles, or feature recreation could leak native Unity objects even though the managed C# reference eventually disappears.

Do not destroy inspector-assigned materials that the feature does not own. Only destroy resources created by this feature.

### `RenderTexturePass` Class Declaration

```csharp
public partial class RenderTexturePass : ScriptableRenderPass
```

`ScriptableRenderPass` represents a unit of work that URP can schedule at a `RenderPassEvent`.

The class is `partial` because its runtime pass logic and serializable `Settings` type are kept in separate files:

- `RenderTexturePass.cs`: recording and execution
- `RenderTexturePass.Settings.cs`: inspector model and cached shader-tag conversion

`partial` is a C# organization feature. The compiler combines both declarations into one class. It has no runtime rendering cost.

### Why `PassData` Exists

```csharp
private class PassData
{
    public RendererListHandle RendererListHandle;
    public Settings.GlobalKeyword[] GlobalKeywords;
    public int TexturePropertyId;
    public int TexelSizePropertyId;
    public Vector4 TexelSize;
    public bool PublishGlobalTexture;
    public bool PublishGlobalTexelSize;
}
```

Render Graph separates recording from execution. `PassData` is the explicit data bridge between those phases.

During `RecordRenderGraph`, the code fills this object. Later, Render Graph passes it to the registered render function.

Only values required by `ExecutePass` belong here. Keeping unrelated feature state out of `PassData` makes dependencies clearer and avoids capturing mutable fields in execution lambdas.

If execution read `_settings` directly instead, the pass would depend on mutable feature state that might be changed by another camera or a later `Setup` call before execution.

### `Setup(...)` In Detail

```csharp
public void Setup(string profilingName, Settings settings)
```

`Setup` transfers the current settings into a reusable pass and prepares state that URP reads before recording.

#### Settings reference

```csharp
_settings = settings;
```

The pass stores the settings object by reference. It does not clone it. Inspector changes therefore affect the next setup/record cycle.

Do not mutate the same settings object from unrelated systems during Render Graph recording. Treat renderer settings as configuration, not per-object gameplay state.

#### `Shader.PropertyToID`

```csharp
_texturePropertyId = Shader.PropertyToID(settings.TextureName);
_texelSizePropertyId =
    Shader.PropertyToID($"{settings.TextureName}_TexelSize");
```

Unity shader properties have string names such as `_SelectionOutlineMask`. `Shader.PropertyToID` converts a name to the integer identifier accepted by material, command-buffer, and Render Graph APIs.

Why integer IDs are used:

- repeated integer lookups are more efficient than repeated string lookups
- IDs avoid repeating the same spelling throughout execution code
- the same ID becomes the key in `FrameTextureRegistry`

The surrounding name comparison recalculates IDs only when `TextureName` changes. It also avoids allocating the interpolated texel-size string every frame.

Property IDs are stable during one application run but are not persistent identifiers. Never serialize an ID, store it in an asset, or send it over a network. Serialize the property name and regenerate the ID at runtime.

This call does not create a shader property or allocate a texture. It only obtains an identifier for a name.

#### `renderPassEvent`

```csharp
renderPassEvent = settings.RenderPassEvent;
```

`renderPassEvent` tells URP at which injection point this pass belongs.

If it is not assigned, the base class default determines timing, which may place the producer before camera resources are ready or after a consumer expected the texture.

Use the earliest event that has the resources and culling state the pass needs, while still preceding every consumer.

#### `ProfilingSampler`

```csharp
profilingSampler =
    MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(...);
```

`ProfilingSampler` provides the marker shown by Unity's CPU/GPU profiler and Render Graph tooling.

The helper reuses the sampler while the name is unchanged. Recreating profiler objects in a per-camera setup path would create avoidable managed allocations.

Removing the sampler does not change the pixels, but diagnostics become generic or harder to attribute.

#### `RenderStateBlock(RenderStateMask.Nothing)`

```csharp
_renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
```

`RenderStateBlock` describes GPU render-state overrides. `RenderStateMask.Nothing` means **override no state**. It does not mean "render nothing."

Starting from `Nothing` is important because the feature should preserve material-defined blend, raster, stencil, and depth behavior unless a setting explicitly requests an override.

If the mask accidentally included state categories with uninitialized values, materials could render with unexpected depth, stencil, blend, or culling behavior.

#### `RenderStateMask.Depth` And `DepthState`

```csharp
_renderStateBlock.mask |= RenderStateMask.Depth;
_renderStateBlock.depthState =
    new DepthState(writeEnabled, function);
```

When `Depth` is enabled, the mask says that the depth portion of the block is active. `DepthState` then controls:

- whether passing fragments write depth
- which `CompareFunction` determines whether a fragment passes the depth test

Setting `depthState` without adding `RenderStateMask.Depth` has no effect because Unity only applies state categories enabled in the mask.

This state override is only half of depth support. A compatible depth attachment must also be declared in `RecordRenderGraph`. State says **how** to test/write depth; the attachment says **which depth texture** is used.

#### `ConfigureInput`

```csharp
ConfigureInput(settings.RenderPassInput);
```

`ConfigureInput` declares URP frame inputs required by the pass, such as depth, normals, motion, or camera color.

Why declaration matters:

- URP can create required prepasses or intermediate textures
- URP can avoid those resources when the pass does not need them
- the renderer can make correct optimization decisions

If a shader samples camera depth or normals but the pass does not request them, those resources may be unavailable or contain a fallback value. If every input is requested "just in case," URP may add unnecessary memory and rendering work.

This setting is separate from the custom output's optional depth attachment. `ConfigureInput(Depth)` requests camera depth as an input resource; `SetRenderAttachmentDepth` attaches depth to this raster pass.

### `RecordRenderGraph(...)` In Detail

```csharp
public override void RecordRenderGraph(
    RenderGraph renderGraph,
    ContextContainer frameData)
```

URP calls this method while recording the active camera's Render Graph.

- `RenderGraph renderGraph` creates graph resources and pass nodes.
- `ContextContainer frameData` stores URP and custom frame-local context items.

Do not retain either object beyond the recording call. A `TextureHandle` obtained here belongs to this frame and graph.

#### `AddRasterRenderPass`

```csharp
using IRasterRenderGraphBuilder builder =
    renderGraph.AddRasterRenderPass(
        _settings.TextureName,
        out PassData passData,
        profilingSampler);
```

This creates a raster-pass node and returns:

- a builder used to declare pass behavior
- a `PassData` instance for execution data

The `using` statement is significant. Disposing the builder ends pass recording. Do not call `AddRasterRenderPass`, `AddComputePass`, or another graph pass-creation API while a previous builder is still recording; close the first builder's scope before adding another pass.

If no pass node is added, Render Graph has nothing to schedule. If a builder is created but no render function is assigned, graph validation can fail or the pass has no executable work.

Use a raster pass when drawing renderer lists or fullscreen raster geometry into attachments. Use a compute pass for compute workloads, and use built-in copy/blit helpers for straightforward copies when their contracts are sufficient.

#### Create pass data before execution

`InitPassData` fills the renderer-list and keyword fields. Texture IDs and texel size are copied into `PassData` after the destination descriptor is known.

This explicit copy freezes the values needed by the eventual execute function.

#### `RendererListHandle.IsValid`

```csharp
if (!passData.RendererListHandle.IsValid())
    return;
```

`RendererListHandle` is a Render Graph handle, not the list of renderers itself. `IsValid` guards against recording a draw that references an invalid graph resource.

In the normal path, `CreateRendererList` returns a valid handle. The check is defensive.

#### Registering with `FrameTextureRegistry`

```csharp
FrameTextureRegistry textureRegistry =
    FrameTextureRegistry.GetOrCreate(frameData);

textureRegistry.SetTexture(
    passData.TexturePropertyId,
    destination,
    passData.TexelSize);
```

The registry is a custom `ContextItem` that maps a shader property ID to:

- the frame-local `TextureHandle`
- `(1 / width, 1 / height, width, height)`

Why the registry exists even though the texture is also global:

- C# consumers can obtain the real `TextureHandle`
- consumers can declare `UseTexture` dependencies explicitly
- multiple named textures can coexist without relying on "the last texture"
- texel-size metadata travels with the handle

If registration is removed, global-shader consumers may still see the texture after `SetGlobalTextureAfterPass`, but `FrameTextureResolver`, debug passes, JFA, halo, outline, and other handle-based consumers cannot resolve it through the package contract.

#### `UseRendererList`

```csharp
builder.UseRendererList(passData.RendererListHandle);
```

This tells Render Graph that the pass consumes the renderer-list resource.

The declaration lets Render Graph validate and schedule the list correctly. Calling `DrawRendererList` during execution without declaring the handle breaks the graph contract and can cause validation errors or incorrect pass analysis.

#### `SetRenderAttachment`

```csharp
builder.SetRenderAttachment(destination, 0);
```

This declares `destination` as color attachment index `0`.

It is the Render Graph equivalent of selecting the color render target for the pass. It also tells Render Graph that this pass writes the texture.

Without a color attachment, the object shaders have no declared color target. Draw calls cannot produce the expected output texture.

All color/depth attachments in one native raster pass must have compatible dimensions, sample counts, and slice counts. This is why a scaled color texture cannot share the full-resolution camera depth attachment.

#### `SetRenderAttachmentDepth`

```csharp
builder.SetRenderAttachmentDepth(
    resourceData.activeDepthTexture,
    depthAccess);
```

This conditionally attaches URP's active camera depth texture.

`AccessFlags.Read` means depth testing may read existing depth but this pass declares no depth writes. `AccessFlags.ReadWrite` is used when `WriteDepth` is enabled.

If read/write intent is declared incorrectly, Render Graph can make unsafe scheduling or load/store decisions. Always request the narrowest correct access.

The pass first verifies matching width, height, and volume depth. Attachment mismatch is a graph compilation error, not a harmless scaling operation.

#### `SetGlobalTextureAfterPass`

```csharp
builder.SetGlobalTextureAfterPass(
    destination,
    passData.TexturePropertyId);
```

When `Texture Exposure` is either global-texture mode, Unity binds the destination to the configured global shader property after this pass executes.

This makes the texture available to ordinary shaders that know only the property name. It also establishes the correct timing: the global binding is applied after the producer has written the texture.

If this call is omitted, handle-based consumers using `FrameTextureRegistry` still work, but scene materials and shaders sampling the global name do not receive this frame's texture through this feature.

Prefer direct handle dependencies for C# consumers, and publish globally only when shader-level access is part of the feature contract. `SetGlobalTextureAfterPass` is Render Graph's explicit global-texture API; it does not by itself require `AllowGlobalStateModification(true)`. The separate global texel-size command does.

#### `AllowGlobalStateModification(true)`

```csharp
builder.AllowGlobalStateModification(true);
```

The pass enables this declaration only when it will publish a global texel-size vector or apply at least one enabled, named global keyword change. Render Graph requires the pass to declare those command-buffer side effects.

Without this declaration, changing global state inside a raster pass violates the graph contract and can trigger validation problems. It also prevents Render Graph from reasoning incorrectly that the pass has no external side effects.

Allowing global state creates a Render Graph synchronization point, prevents later passes from moving before this pass, and disables pass culling. Avoid it when a local material property or explicit texture dependency can express the same behavior. Global keywords affect all shaders and can make pass ordering more fragile.

Do not expose this Render Graph declaration as a manual setting. The implementation derives it from `Texture Exposure` and the configured keyword actions so the declaration cannot disagree with the commands executed by the pass.

#### `SetRenderFunc`

```csharp
builder.SetRenderFunc(
    (PassData data, RasterGraphContext context) =>
        ExecutePass(data, context));
```

`SetRenderFunc` assigns the function Render Graph calls when executing the pass.

The lambda forwards only `PassData` and `RasterGraphContext`; it does not capture local variables. This pattern keeps execution data explicit and avoids closure allocations.

Without `SetRenderFunc`, all resource declarations exist but no clear, keyword, or draw commands are generated.

### `InitPassData(...)` And Object Selection

The destination texture does not decide which objects are drawn. Selection is built into a renderer list.

#### `ContextContainer.Get<T>()`

```csharp
UniversalRenderingData universalRenderingData =
    frameData.Get<UniversalRenderingData>();
UniversalCameraData cameraData =
    frameData.Get<UniversalCameraData>();
UniversalLightData lightData =
    frameData.Get<UniversalLightData>();
```

`ContextContainer` is typed frame-local storage populated by URP.

- `UniversalRenderingData` provides culling results and general rendering state.
- `UniversalCameraData` provides active-camera data and its target descriptor.
- `UniversalLightData` provides lighting state used while building draw settings.
- `UniversalResourceData` provides active frame textures such as camera color and depth.

Calling `Get<T>` assumes the item exists at this injection point. Use `Contains<T>` or a resolver pattern for optional custom context items.

Do not cache these objects across frames. Their contents describe the active camera's current frame.

#### Camera `CullingResults`

```csharp
ref universalRenderingData.cullResults
```

The feature reuses URP's existing culling results. It does not cull the scene again.

Consequences:

- only renderers visible to the active camera's culling process are candidates
- layer, render-queue, rendering-layer, and shader-pass filters further reduce that set
- the feature cannot capture objects excluded by the camera culling mask or outside its frustum without a separate culling/camera strategy

Reusing culling is efficient and ensures alignment with the active camera. Use a second camera or custom culling only when the desired view genuinely differs.

#### `RenderingUtils.CreateDrawingSettings`

```csharp
DrawingSettings drawingSettings =
    RenderingUtils.CreateDrawingSettings(
        _settings.LightModeShaderTags,
        universalRenderingData,
        cameraData,
        lightData,
        _settings.SortingCriteria);
```

`DrawingSettings` answers **how eligible renderers should be drawn**:

- accepted shader `LightMode` tags
- sorting rules
- per-object data and lighting state derived from URP frame data
- optional override material

The tag list does not contain shader names. It contains pass tags such as `UniversalForward` or `SRPDefaultUnlit`.

If none of a renderer's shader passes match the configured tags, that renderer is skipped even when its GameObject layer and render queue match.

#### `overrideMaterial`

```csharp
drawingSettings.overrideMaterial = _settings.Material;
```

When non-null, Unity draws eligible renderers with this material instead of their original materials.

Use it for masks and encoded data because every selected object can write a consistent value. Leave it null when the output should preserve the objects' original shading.

An override material changes shading, not object selection. Filtering still comes from culling, queues, layers, rendering layers, and shader tags.

#### `overrideMaterialPassIndex`

```csharp
drawingSettings.overrideMaterialPassIndex =
    _settings.MaterialPassIndex;
```

This selects the pass from the override material. Pass indexes are zero-based and refer to ShaderLab `Pass` blocks after shader compilation.

Use an explicit index when the material has more than one pass. A wrong index can produce no draw, the wrong output, or extra work. The project's `-1` default follows the intended all-pass behavior for this capture workflow, but a production mask material should normally contain one dedicated pass or use an explicit index such as `0`.

This setting is ignored when `overrideMaterial` is null.

#### `FilteringSettings`

```csharp
FilteringSettings filteringSettings = new(
    _settings.RenderQueueRange,
    _settings.LayerMask,
    (uint)_settings.RenderLayerMask);
```

`FilteringSettings` answers **which already-culled renderers are eligible**:

- material render queue must be inside `RenderQueueRange`
- GameObject layer must intersect `LayerMask`
- renderer rendering-layer mask must intersect `RenderLayerMask`

All filters are combined. Matching one does not compensate for failing another.

The code passes constructor arguments explicitly. `new FilteringSettings()` with no useful values can produce zero-valued filters and unexpectedly reject all objects.

#### GameObject layers versus rendering layers

These are independent systems:

- `LayerMask` uses `GameObject.layer` and also participates in cameras, physics, and many gameplay systems.
- `RenderingLayerMask` uses `Renderer.renderingLayerMask` and is rendering-specific in SRP.

Use rendering layers when effect membership should not force gameplay-layer changes. Use GameObject layers when the same grouping is already an intentional project-wide category.

#### `RendererListParams` And `CreateRendererList`

`RenderingHelpers.CreateRendererListWithRenderStateBlock` combines:

- camera culling results
- drawing settings
- filtering settings
- the optional render-state block

into `RendererListParams`, then calls:

```csharp
renderGraph.CreateRendererList(parameters);
```

The result is a `RendererListHandle` that Render Graph tracks as a resource.

Why a renderer list is preferable to manually iterating scene renderers:

- it uses the pipeline's culling results
- it preserves SRP batching and renderer sorting
- it lets Render Graph understand the draw dependency
- it avoids expensive scene searches

The helper uses temporary `NativeArray` values because `RendererListParams` expects arrays mapping tag values to state blocks. `Allocator.Temp` is appropriate for short-lived recording data and must not be retained beyond its permitted lifetime.

### Destination Texture Creation

#### `cameraTargetDescriptor`

```csharp
RenderTextureDescriptor descriptor =
    cameraData.cameraTargetDescriptor;
```

Starting from the camera descriptor preserves important platform/camera characteristics such as texture dimension, volume depth, and XR-related configuration.

Creating a descriptor from only width and height risks losing those properties.

The pass then deliberately overrides fields that differ from the camera target.

#### `colorFormat`

```csharp
descriptor.colorFormat = _settings.ColorFormat;
```

This controls channel count, precision, and memory representation.

Use `R8` for a simple scalar mask when supported. Use a multi-channel format only when the producer encodes multiple values or actual color.

A format that is too small loses data; a format that is unnecessarily large costs memory and bandwidth. Platform support should be validated for unusual formats.

#### `depthBufferBits = 0`

```csharp
descriptor.depthBufferBits = 0;
```

The generated resource is a color texture without its own depth buffer.

Depth, when requested, comes from the separately attached active camera depth texture. Keeping depth out of the color descriptor avoids allocating an unused private depth surface.

If this were left copied from a camera descriptor with depth, the feature might allocate unnecessary depth memory or create a resource that does not match its intended role.

#### `msaaSamples = 1`

```csharp
descriptor.msaaSamples = 1;
```

The output is single-sampled even when the camera uses MSAA.

This is appropriate for a texture that later passes sample directly. Multisampled textures require resolve behavior and increase memory cost.

The tradeoff is that geometric mask edges do not receive the camera target's MSAA representation. Use resolution, bilinear sampling, morphology, or a distance-field technique when a consumer needs smoother boundaries.

#### Camera and custom size modes

`ApplyTextureSize` either scales camera dimensions or replaces them with explicit custom dimensions.

Every result is clamped to at least `1x1`, preventing invalid texture allocation when a multiplier or custom value is very small.

Custom size disables dynamic-scaling flags because the requested dimensions are explicit. A scaled camera-size output also cannot share the full-resolution camera depth attachment unless the final dimensions still match exactly.

#### `UniversalRenderer.CreateRenderGraphTexture`

```csharp
TextureHandle destination =
    UniversalRenderer.CreateRenderGraphTexture(
        renderGraph,
        descriptor,
        _settings.TextureName,
        false,
        _settings.FilterMode,
        _settings.WrapMode);
```

This URP helper converts the `RenderTextureDescriptor` into a transient Render Graph texture and returns a `TextureHandle`.

Arguments used here:

- `renderGraph`: owner of the frame-local resource
- `descriptor`: size, format, dimension, sample count, and related properties
- `TextureName`: diagnostic resource name
- `false`: do not ask the creation helper to clear it automatically
- `FilterMode`: sampling filter used by consumers
- `WrapMode`: sampling behavior outside UV range

The pass clears the attachment explicitly in `ExecutePass`, so creation-time clearing is disabled.

The returned handle is not a persistent `RenderTexture` asset and must not be stored for the next frame. Render Graph determines allocation, aliasing, and lifetime from declared dependencies.

### Depth Attachment Compatibility

`CanUseCameraDepthAttachment` requires:

```text
Texture Size Mode == Camera
output width == camera width
output height == camera height
output volume depth == camera volume depth
```

Raster attachments must agree in dimensions. When the color output is half resolution and camera depth is full resolution, attaching both produces a Render Graph fragment-dimension mismatch.

When compatibility fails, the pass logs one warning and continues without depth. It does not resize or copy camera depth because that would add a separate operation and introduce choices about reduction/filtering semantics.

Use full camera size when accurate camera-depth testing is mandatory. Disable `Depth` for scaled data-only masks, or build a dedicated depth-resampling solution when lower-resolution depth is truly required.

### `ExecutePass(...)` In Detail

`ExecutePass` receives `RasterGraphContext`. Its `cmd` is a `RasterCommandBuffer` scoped to raster commands legal for this pass.

#### Global keywords

```csharp
cmd.EnableShaderKeyword(keyword.Name);
cmd.DisableShaderKeyword(keyword.Name);
```

These commands modify global shader keyword state before and after drawing.

Use them only when a shader variant genuinely depends on global state. Prefer local material keywords when possible.

If an enabled keyword is not restored after the draw, later passes or cameras can render with the wrong variant. The before/after configuration exists so the feature can explicitly establish and restore state.

#### Global texel-size vector

```csharp
context.cmd.SetGlobalVector(
    data.TexelSizePropertyId,
    data.TexelSize);
```

When `Frame Registry + Global Texture + Texel Size` is selected, texture `_ObjectMask` publishes `_ObjectMask_TexelSize` as:

```text
(1 / width, 1 / height, width, height)
```

Shaders use `.xy` to move by one pixel in UV space and `.zw` to know actual dimensions.

If omitted, handle-based C# consumers still receive texel metadata from `FrameTextureRegistry`, but ordinary shaders expecting Unity's texel-size naming convention may calculate incorrect offsets.

#### `ClearRenderTarget`

```csharp
context.cmd.ClearRenderTarget(
    RTClearFlags.Color,
    Color.black,
    0,
    0);
```

This clears only the color attachment to zero before objects draw.

Why explicit clearing is essential for masks:

- pixels with no selected object must have a known value
- transient Render Graph memory may contain unrelated previous data
- relying on allocation contents causes flicker and undefined masks

The depth and stencil clear values are present because of the API signature but are not applied when flags contain only `Color`.

Do not remove the clear unless every output pixel is guaranteed to be overwritten and the optimization has been measured.

#### `DrawRendererList`

```csharp
context.cmd.DrawRendererList(data.RendererListHandle);
```

This records draws for the renderer list created earlier.

It respects the list's culling, filtering, shader tags, sorting, override material, pass index, and render-state block.

Without this call, the texture remains at its clear color.

### `RenderTexturePass.Settings` Serialization Model

```csharp
[Serializable]
public class Settings
```

The nested class is marked serializable so Unity can store each instance inside the renderer feature asset's list.

It is a plain serializable C# object, not a `ScriptableObject`. Its lifetime and ownership come from `ObjectsToRenderTextureFeature`.

#### Inspector attributes

| Attribute | What Unity does | Why it is used | What happens without it |
| --- | --- | --- | --- |
| `[Serializable]` | Allows fields of `Settings` to be serialized inline | Stores list entries in the renderer asset | Unity cannot persist the nested settings object correctly |
| `[Tooltip(...)]` | Shows explanatory text on hover | Makes non-obvious rendering choices discoverable | Rendering is unchanged; inspector guidance is lost |
| `[Range(min,max)]` | Draws a slider and constrains normal inspector editing | Prevents obviously invalid/common out-of-range values | Scripts can still clamp, but the inspector allows poor values more easily |
| `[Header(...)]` | Adds an inspector section label | Groups debug fields | Rendering is unchanged |
| `[Flags]` | Presents an enum as a bit-mask combination | Allows several LightMode choices | The value could still be a bit field, but inspector editing becomes misleading |
| `[NonSerialized]` | Excludes a runtime cache from Unity serialization | Rebuilds derived shader-tag caches after reload | Unity may serialize stale implementation details or fail on unsupported cache fields |

`Range` is an editor affordance, not a security boundary. Runtime scripts and serialized YAML can still provide unexpected values, so the pass clamps dimensions and multipliers where invalid data would be dangerous.

### LightMode And Shader-Tag Cache

`LightMode` is a ShaderLab pass tag, for example:

```shaderlab
Pass
{
    Tags { "LightMode" = "UniversalForward" }
}
```

`ShaderTagId` converts that tag string into the identifier used by drawing settings. It does not refer to the ShaderLab pass `Name` and does not select a shader asset by name.

`LightModeShaderTags` lazily builds a `List<ShaderTagId>` from:

- selected built-in `LightModeTags` flags
- non-empty custom `ShaderTags` strings

The cache stores the previous flag value and a copy of custom strings. On access, `IsShaderTagCacheValid` compares current configuration with the cached configuration.

Why cache it:

- `InitPassData` runs per camera
- rebuilding lists and IDs every frame creates unnecessary work and garbage
- settings usually change rarely

Why copy custom strings rather than only compare list references:

- a serialized list can keep the same object reference while an element changes
- element-by-element comparison detects that edit

If no configured tag matches an object's shader, the object does not render into the output. Add a custom tag only for shaders that intentionally use a non-standard `LightMode`.

### Settings-To-API Mapping

| Inspector setting | Unity/URP API receiving it | Effect if omitted or wrong |
| --- | --- | --- |
| `TextureName` | `Shader.PropertyToID`, registry key, texture name, global texture property | Consumers cannot agree on the texture; empty names create unusable contracts |
| `TextureExposure` | `FrameTextureRegistry`, optional `SetGlobalTextureAfterPass`, optional global texel-size update | Registry-only is unavailable directly to scene shaders; texture-only avoids global command state; texel-size mode adds scheduling constraints |
| `Material` | `DrawingSettings.overrideMaterial` | Original object materials render instead of controlled data |
| `MaterialPassIndex` | `DrawingSettings.overrideMaterialPassIndex` | Wrong shader pass or no useful draw |
| `RenderPassEvent` | `ScriptableRenderPass.renderPassEvent` | Producer may execute too early or after its consumer |
| `RenderPassInput` | `ScriptableRenderPass.ConfigureInput` | Needed camera resources may not be generated; excessive inputs add cost |
| queue bounds | `RenderQueueRange` inside `FilteringSettings` | Materials outside the inclusive range disappear |
| `ColorFormat` | `RenderTextureDescriptor.colorFormat` | Precision/channel count may be insufficient or unnecessarily expensive |
| size settings | `RenderTextureDescriptor.width/height` | Screen alignment, quality, bandwidth, and depth compatibility change |
| `FilterMode` | `CreateRenderGraphTexture` | Later sampling is crisp or interpolated |
| `WrapMode` | `CreateRenderGraphTexture` | Out-of-range UVs clamp or repeat |
| `SortingCriteria` | `RenderingUtils.CreateDrawingSettings` | Opaque/transparent ordering may be inefficient or visually incorrect |
| `LayerMask` | `FilteringSettings.layerMask` | Wrong GameObject categories are included/excluded |
| `RenderLayerMask` | `FilteringSettings.renderingLayerMask` | Rendering-specific membership is wrong |
| `LightMode` / `ShaderTags` | `DrawingSettings` shader pass list | Matching objects with nonmatching passes are skipped |
| global keywords | raster command buffer and global-state declaration | Wrong variants may render or leak into later passes |
| depth fields | `RenderStateBlock`, `DepthState`, depth attachment access | Occlusion or camera depth writes behave incorrectly |
| debug fields | debug pass timing/material/shader pass | Production texture is unchanged; only visualization changes |

### Required, Conditional, And Diagnostic Calls

| Call | Category | If removed |
| --- | --- | --- |
| `renderer.EnqueuePass` | Required | Pass is never recorded or executed |
| `renderGraph.AddRasterRenderPass` | Required | No Render Graph node exists |
| `builder.UseRendererList` | Required for renderer-list draw | Graph does not know the renderer-list dependency |
| `builder.SetRenderAttachment` | Required | No declared color output target |
| `builder.SetRenderFunc` | Required | No commands are generated |
| `cmd.DrawRendererList` | Required | Texture contains only clear color |
| `cmd.ClearRenderTarget` | Required for predictable sparse masks | Unwritten pixels can contain undefined transient memory |
| `FrameTextureRegistry.SetTexture` | Required by package consumers | Handle-based consumers cannot resolve output |
| `SetGlobalTextureAfterPass` | Required only for global shader access | Registry consumers work, global shader sampling contract does not |
| `SetRenderAttachmentDepth` | Conditional | No depth testing/writing against camera depth |
| `ConfigureInput` | Conditional | Requested camera resources may not exist |
| `AllowGlobalStateModification` | Automatically required for shader-global texel size or active global keyword changes | Global state commands violate the graph declaration; enabling it unnecessarily disables culling and restricts scheduling |
| profiling sampler | Diagnostic | Pixels unchanged; profiling labels degrade |
| debug pass | Diagnostic | Production output unchanged; no fullscreen/overlay inspection |

### Unity Type Glossary

#### `TextureHandle`

A lightweight Render Graph reference to a texture resource. It is valid only in the current graph/frame. It is not a persistent Unity `Texture` or `RenderTexture` asset.

#### `RenderTextureDescriptor`

A value describing how a render texture should be created: dimensions, format, depth bits, MSAA, dynamic scaling, texture dimension, and related flags.

#### `DrawingSettings`

Describes shader-pass selection, sorting, override material, and draw behavior. Think **how to draw**.

#### `FilteringSettings`

Describes queue, GameObject layer, rendering layer, sorting layer, and motion filtering. Think **which visible renderers qualify**.

#### `RendererListHandle`

A Render Graph handle representing the combination of culling, drawing, filtering, and optional render-state overrides.

#### `RenderStateBlock`

Optional overrides for GPU state such as depth, blend, stencil, or raster state. Its `mask` controls which categories are actually overridden.

#### `ContextContainer`

Typed frame-local storage shared while recording one camera's Render Graph. URP stores camera/resource/light data there; this package stores `FrameTextureRegistry` there.

#### `AccessFlags`

The declared read/write intent for a graph resource. Correct access declarations let Render Graph schedule passes and select load/store behavior safely.

#### `ProfilingSampler`

A named profiling marker associated with a render pass. It improves observability but does not alter visual output.

### Remaining Unity API Quick Reference

The following APIs appear in the three core scripts or their immediate helper/debug paths. They are smaller than the lifecycle APIs above, but each still carries an important contract.

#### Configuration value types

| API/type | Meaning in this feature | When to use it | What goes wrong when misused |
| --- | --- | --- | --- |
| `RenderPassEvent` | URP injection point for producer/debug passes | Order producers and consumers relative to opaque, transparent, and post-processing stages | A consumer can run first, or required camera state may not exist yet |
| `ScriptableRenderPassInput` | Bit mask of camera resources requested from URP | Request depth, normals, motion, or color only when sampled | Missing resources produce invalid/fallback sampling; excess requests add passes or memory |
| `RenderTextureFormat` | Requested output channel layout and precision | Match the data encoded by the producer | Wasted bandwidth or lost precision/channels |
| `FilterMode` | Hardware sampling interpolation | `Point` for discrete masks/ids, `Bilinear` for soft continuous data | Hard data can become fractional, or soft data can look blocky |
| `TextureWrapMode` | UV behavior outside `0..1` | Usually `Clamp` for screen-space textures | `Repeat` can pull data from the opposite screen edge |
| `SortingCriteria` | Renderer ordering rules | `CommonOpaque` for opaque queues and `CommonTransparent` for transparent queues | Transparent blending can be wrong; opaque overdraw/state changes can increase |
| `LayerMask` | Bit mask over `GameObject.layer` | Reuse a project-wide object category | A zero mask selects nothing; an overly broad mask captures unrelated objects |
| `RenderingLayerMask` | SRP-specific renderer membership bits | Keep rendering effects independent of physics/gameplay layers | Renderer membership must be configured separately or objects disappear |
| `CompareFunction` | Depth-test comparison | `LessEqual` for normal visible-surface behavior, `Always` for through-wall masks | Wrong comparison rejects expected fragments or reveals occluded objects |
| `Color` / `Color.black` | Clear value and debug tint | Zero masks require a black background | Nonzero clears make empty pixels appear selected |
| `Vector2Int` | Explicit integer texture dimensions | Custom-size outputs | Zero/negative values must be clamped before allocation |
| `Vector4` | Texel-size package `(1/w, 1/h, w, h)` | Pixel-based shader offsets and metadata | Swapped or stale values create resolution-dependent widths |

#### Math and validation APIs

| API | Why this code uses it | If omitted |
| --- | --- | --- |
| `Mathf.Clamp` | Restricts camera multiplier to the supported range even when data comes from code/YAML rather than the inspector slider | Invalid or extreme scales can reach descriptor calculation |
| `Mathf.Approximately` | Treats a multiplier effectively equal to `1` as unchanged | Tiny floating-point differences can trigger unnecessary width/height recomputation |
| `Mathf.RoundToInt` | Converts scaled floating dimensions to integer pixels | Implicit/incorrect conversion can bias dimensions or fail to compile |
| `Mathf.Max(1, value)` | Guarantees legal nonzero dimensions | Render Graph texture creation can receive invalid dimensions |
| `string.IsNullOrWhiteSpace` | Produces a readable fallback profiling label for empty texture names | Diagnostics can contain empty/unclear names; texture names should still be configured explicitly |
| `StringComparison.Ordinal` | Compares cached shader-tag strings as exact identifiers | Culture-sensitive comparison is inappropriate for programmatic shader tag names |

`Mathf` guards are still required when fields have `[Range]`. Inspector widgets constrain normal editing, but runtime code, migration, and direct YAML edits are not guaranteed to obey the slider.

#### Raster command APIs

| API | What it records | Why this command belongs in execution rather than setup |
| --- | --- | --- |
| `RasterCommandBuffer.SetGlobalVector` | A global shader vector update | GPU command ordering must match pass execution order |
| `RasterCommandBuffer.EnableShaderKeyword` | Enables a global keyword for subsequent draws | Variant state must be active exactly when renderer-list draws occur |
| `RasterCommandBuffer.DisableShaderKeyword` | Disables/restores a global keyword | Prevents variant state leaking into later work |
| `RasterCommandBuffer.ClearRenderTarget` | Attachment clear command | The destination is only bound when this graph pass executes |
| `RasterCommandBuffer.DrawRendererList` | All renderer draws represented by the handle | Render Graph has already established attachments and dependencies at that point |

Calling equivalent immediate global APIs during `RecordRenderGraph` would mutate CPU/global state while the graph is only being described. Command-buffer calls preserve GPU execution ordering.

#### Debug-pass APIs

`RenderTextureDebugPass` uses several additional APIs:

| API | Purpose | If removed or declared incorrectly |
| --- | --- | --- |
| `UniversalResourceData.activeColorTexture` | Current camera color target at the debug injection point | Debug output has no camera destination |
| `builder.UseTexture(source, AccessFlags.Read)` | Declares the generated texture as an input | Graph cannot track the sample dependency |
| `builder.SetRenderAttachment(activeColor, 0, AccessFlags.ReadWrite)` | Writes debug pixels into camera color while preserving read/blend semantics | Overlay blending or graph access analysis can be incorrect |
| `Material.SetColor` | Updates the debug shader's tint | Overlay mode uses stale/default tint |
| `Blitter.BlitTexture` | Draws a fullscreen triangle using the source texture and selected material pass | No fullscreen/overlay visualization is produced |

`Blitter.BlitTexture` binds its source using URP's `_BlitTexture` contract. It is appropriate for fullscreen raster operations. It should not be used to read and write the exact same texture in one pass; use a separate destination when processing a texture.

#### Renderer-list helper APIs

`RenderingHelpers.CreateRendererListWithRenderStateBlock` uses lower-level Core RP types:

| API | Purpose | Important rule |
| --- | --- | --- |
| `NativeArray<T>` | Supplies contiguous tag/state-block arrays to `RendererListParams` | Respect allocator lifetime; do not store temporary arrays |
| `Allocator.Temp` | Marks allocations as very short lived | Never retain them across frames or long-running jobs |
| `ShaderTagId.none` | Wildcard/default tag value for applying the state block in this helper arrangement | This is not the same as an empty LightMode list |
| `RendererListParams` | Bundles culling, drawing, filtering, tag, and state-block data | Tag and state-block arrays must correspond by index |
| `renderGraph.CreateRendererList` | Imports the renderer-list description into Render Graph | The returned handle must be declared with `UseRendererList` before drawing |

The helper mirrors an optimized Core/URP pattern because the convenient engine helper with this exact render-state-block behavior is internal in the package version used by the project.

### When To Use This Architecture

Use `ObjectsToRenderTextureFeature` when all of these are true:

- the active camera's view and culling are appropriate
- a subset of visible renderers must become a temporary screen-space texture
- selection can be expressed with queues, layers, rendering layers, and shader tags
- a later pass or shader consumes the result in the same frame

Do not use it when:

- the texture must persist across frames without a history/persistent-texture system
- the view must come from another camera position
- objects outside active-camera culling must appear
- a single object shader can produce the complete effect without an intermediate texture
- the desired result is ordinary camera color post-processing with no object subset
- CPU gameplay code needs readable pixels immediately; GPU readback is a different asynchronous workflow

### Official Unity References

The implementation follows Unity's Render Graph renderer-feature model. These references provide the engine-level definitions behind the explanations above:

- [URP Scriptable Renderer Feature API reference](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/renderer-features/scriptable-renderer-features/scriptable-renderer-feature-reference.html)
- [Write a Render Graph render pass in URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-write-render-pass.html)
- [Access current-frame data in URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/accessing-frame-data.html)
- [Work with textures in URP Render Graph](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/working-with-textures.html)
- [Create a global texture in URP](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/render-graph-create-global-texture.html)
- [URP ShaderLab Pass tags and LightMode values](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/urp-shaders/urp-shaderlab-pass-tags.html)
- [Declare shader keywords](https://docs.unity3d.com/6000.0/Documentation/Manual/SL-MultipleProgramVariants-declare.html)
- [Strip shader variants](https://docs.unity3d.com/6000.0/Documentation/Manual/shader-variant-stripping.html)
- [CommandBuffer global keyword commands](https://docs.unity3d.com/ScriptReference/Rendering.CommandBuffer.html)
- [Shader.PropertyToID](https://docs.unity3d.com/ScriptReference/Shader.PropertyToID.html)
- [FilteringSettings](https://docs.unity3d.com/ScriptReference/Rendering.FilteringSettings.html)
- [SortingCriteria](https://docs.unity3d.com/ScriptReference/Rendering.SortingCriteria.html)
- [RenderStateBlock](https://docs.unity3d.com/ScriptReference/Rendering.RenderStateBlock.html)
- [DepthState](https://docs.unity3d.com/ScriptReference/Rendering.DepthState.html)
- [RenderTextureDescriptor](https://docs.unity3d.com/ScriptReference/RenderTextureDescriptor.html)
- [Renderer.renderingLayerMask](https://docs.unity3d.com/ScriptReference/Renderer-renderingLayerMask.html)
- [Unity serialization rules](https://docs.unity3d.com/Manual/script-serialization-how-unity-uses.html)

## RenderTexturePass Internals

### `FrameTextureRegistry`

`FrameTextureRegistry` is a Render Graph `ContextItem` shared by texture producers and consumers.

It is frame-local storage for generated texture handles.

It stores:

- a map from texture property id to a `TextureHandle` and its texel size

Every lookup is keyed. There is no ambiguous "last generated texture" in the production API, so the registry can safely hold multiple masks, distance fields, and internal color snapshots in one frame.

Texture producers and consumers use `FrameTextureRegistry` directly. Use `FrameTextureResolver` when a pass only needs to resolve one configured texture name.

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
- optional global texture export
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
