# 02 - Frame Registry + Global Texture

This example draws a red overlay by sampling the producer output through a named
global shader property. It deliberately does not fetch a `TextureHandle` from
`FrameTextureRegistry`.

Open `Example.unity` for the ready-to-run setup. Its camera uses
`GlobalTextureExampleRenderer`, which already contains the producer followed by
`GlobalTextureExampleFeature`.

## Producer setup

In `ObjectsToRenderTextureFeature`, add an output with:

```text
Texture Name:     _GlobalTextureExampleTexture
Texture Exposure: Frame Registry + Global Texture
Render Pass Event: After Rendering Opaques
```

Then add `GlobalTextureExampleFeature` below the producer in Renderer Data. The
shader is loaded automatically; there is no material field to configure.

## What it demonstrates

The producer calls Render Graph's tracked global-texture publication path. The
consumer declares `UseGlobalTexture`, and its shader samples the exact property
name `_GlobalTextureExampleTexture`.

Unity automatically makes `_GlobalTextureExampleTexture_TexelSize` available with
the texture binding; the producer does not need to set that vector manually.

## Why and when to use it

Use this mode whenever an ordinary shader needs to sample the generated texture.
Examples include lookup masks, tinting, visibility tests, outlines, and filters
that use pixel-sized offsets through the automatic `_TexelSize` vector. Choose
registry-only when all consumers are C# Render Graph passes.

Using `Frame Registry Only` with this example is intentionally invalid:
`UseGlobalTexture` has no published global resource to resolve.
