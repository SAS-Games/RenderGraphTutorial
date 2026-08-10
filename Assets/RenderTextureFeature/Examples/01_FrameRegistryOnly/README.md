# 01 - Frame Registry Only

This example draws a blue overlay by resolving a `TextureHandle` from
`FrameTextureRegistry`. It never publishes or reads a global shader texture.

Open `Example.unity` for the ready-to-run setup. Its camera uses
`FrameRegistryOnlyExampleRenderer`, which already contains the producer followed
by `FrameRegistryOnlyExampleFeature`.

## Producer setup

In `ObjectsToRenderTextureFeature`, add an output with:

```text
Texture Name:     _FrameRegistryOnlyExampleTexture
Texture Exposure: Frame Registry Only
Render Pass Event: After Rendering Opaques
```

Then add `FrameRegistryOnlyExampleFeature` below the producer in Renderer Data.
The shader is loaded automatically; there is no material field to configure.

## What it demonstrates

The pass calls `FrameTextureResolver.TryResolve`, receives the real Render Graph
`TextureHandle`, declares `UseTexture`, and passes that handle to `Blitter`.
The dependency and lifetime are explicit to Render Graph.

## Why and when to use it

Prefer this mode when the next consumer is another C# Render Graph pass. It keeps
the texture frame-local, avoids a global shader binding, and still gives C# both
the texture handle and texel-size vector. Typical uses are mask processing,
multi-pass effects, copies, compute preparation, and intermediate textures that
ordinary scene shaders never need to see.

The example also works with either broader exposure mode because every mode writes
to the registry, but `Frame Registry Only` is the smallest sufficient option.
