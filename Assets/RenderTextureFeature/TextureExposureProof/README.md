# Texture Exposure Proof Features

This folder contains two intentionally strict diagnostic renderer features. They demonstrate the difference between publishing a frame texture only to C# Render Graph consumers, publishing it as a Render Graph global texture, and additionally publishing its texel-size vector as mutable shader-global state.

These features are test tools. Normal package effects should prefer `FrameTextureRegistry` because it provides an explicit `TextureHandle` and texel size without shader-global state.

## What The Two Features Prove

### `GlobalTextureProofFeature`

This feature samples `_TextureExposureProofMask` directly as a shader-global texture. It draws a green overlay inside the mask.

Minimum producer capability:

```text
Frame Registry + Global Texture
```

`Frame Registry + Global Texture + Texel Size` also works because it includes the global-texture capability.

The pass declares the dependency with:

```csharp
builder.UseGlobalTexture(texturePropertyId, AccessFlags.Read);
```

It does not retrieve the mask `TextureHandle` from `FrameTextureRegistry`.

### `GlobalTextureAndTexelSizeProofFeature`

This feature samples the same global texture and reads:

```hlsl
float4 _TextureExposureProofMask_TexelSize;
```

It uses the reciprocal width and height in `.xy` to compare the four neighboring texels and draw a one-texel yellow edge around the mask.

Required producer capability:

```text
Frame Registry + Global Texture + Texel Size
```

The backing C# enum value is `FrameRegistryAndShaderGlobals`.

`Frame Registry + Global Texture` is insufficient because it intentionally does not call `SetGlobalVector` for the texel-size property.

## Scene Setup

1. Open the URP Renderer Data asset used by the camera.
2. Add `ObjectsToRenderTextureFeature` if it is not already present.
3. Add one output to `Render Texture Output Settings`.
4. Set `Texture Name` to `_TextureExposureProofMask` exactly, including capitalization and the leading underscore.
5. Assign the layer containing the test object to `Layer Mask`.
6. Assign a flat white mask material, or use the object's material if its red channel produces a useful mask.
7. Set the producer `Render Pass Event` to `After Rendering Opaques`.
8. Disable the producer's `Debug View`; the proof features provide their own overlays.
9. Add `GlobalTextureProofFeature` below the producer.
10. Assign `GlobalTextureProof.mat` to its `Proof Material` field.
11. Leave its `Injection Point` at `After Rendering Transparents`.
12. Add `GlobalTextureAndTexelSizeProofFeature` below it.
13. Assign `GlobalTextureAndTexelSizeProof.mat` to its `Proof Material` field.
14. Leave its `Injection Point` at `After Rendering Transparents`.

The producer must run before both consumers. Using an earlier producer event and placing the producer above the proof features makes that ordering explicit.

## Test Matrix

Change only the producer's `Texture Exposure` field and observe the Game view and Console:

| Producer mode | Green mask fill | Yellow one-texel edge | Expected errors |
| --- | --- | --- | --- |
| `Frame Registry Only` | No | No | `UseGlobalTexture` reports that the global texture was not published |
| `Frame Registry + Global Texture` | Yes | No; the screen becomes magenta | Texel-size proof shows its failure color |
| `Frame Registry + Global Texture + Texel Size` | Yes | Yes | None |

This is a capability hierarchy. The full shader-global mode satisfies the texture-only feature as well as the texel-size feature. Requiring the texture-only feature to reject the fuller mode would be artificial because the resource it needs is genuinely available.

## How Failure Works

Both proof consumers call `UseGlobalTexture`. Unity Render Graph resolves the current global slot to the texture published by the earlier producer and declares the resource dependency. If the producer uses `Frame Registry Only`, no earlier pass has published that slot, so Unity reports a Render Graph error instead of silently using the registry texture.

The texel-size feature also enqueues a small reset pass at `BeforeRendering`. It sets `_TextureExposureProofMask_TexelSize` to zero before the mask producer runs. The full shader-global mode overwrites that value with valid dimensions; the global-texture-only mode does not. The shader renders solid magenta when the value remains zero.

Resetting the vector avoids false success caused by stale shader-global values left by an earlier frame or by changing the Inspector mode. This diagnostic behavior is fully contained in the proof feature and does not require publication metadata in `RenderTexturePass` or `FrameTextureRegistry`.

## Why The Texel Edge Is Useful

Sampling the center mask does not need texture dimensions. Sampling exactly one neighboring pixel does. The second proof therefore turns texel-size publication into visible behavior:

```hlsl
float2 texel = _TextureExposureProofMask_TexelSize.xy;
sample(uv + float2(texel.x, 0));
sample(uv - float2(texel.x, 0));
sample(uv + float2(0, texel.y));
sample(uv - float2(0, texel.y));
```

The vector layout is:

```text
x = 1 / texture width
y = 1 / texture height
z = texture width
w = texture height
```

This same data is useful for outlines, erosion/dilation, blur kernels, edge detection, distortion offsets measured in pixels, and neighborhood filters.

## Troubleshooting

### Both features report that the texture was not found

- Verify the exact name `_TextureExposureProofMask`.
- Verify the producer feature is enabled and the output has a non-empty layer mask.
- Put `ObjectsToRenderTextureFeature` above both proof features in Renderer Data.
- Ensure the producer event is earlier than the consumer injection points.

### The green fill appears but the yellow edge does not

This is expected in `Frame Registry + Global Texture` mode. The texel-size proof renders magenta to make the missing vector obvious. Select `Frame Registry + Global Texture + Texel Size` for the producer.

If the full mode is already selected, verify that `GlobalTextureAndTexelSizeProof.mat` is assigned to the second feature.

### The overlays cover unexpected objects

The proof features only visualize the producer output. Correct the producer's `Layer Mask`, render queue, shader tags, or override mask material.

### Nothing appears and there are no errors

- Confirm both proof features are enabled in Renderer Data.
- Confirm the selected camera uses that Renderer Data asset.
- Confirm the mask contains nonzero red-channel values.
- Temporarily enable the producer's `Debug View` to inspect the generated mask, then disable it again.

## Removing The Proof

After verifying the modes, remove or disable both proof renderer features. Each adds a fullscreen raster pass, and the texel-size proof also adds a tiny global-vector reset pass. They are not required by `ObjectsToRenderTextureFeature` or by registry-based effects.
