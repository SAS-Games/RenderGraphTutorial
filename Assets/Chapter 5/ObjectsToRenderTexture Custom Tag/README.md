# Chapter 5 Subchapter - Capturing a Custom Shader Tag

This subchapter reuses Chapter 5's existing
`Custom/URPCustomTagMultiPass` shader with `ObjectsToRenderTextureFeature`.
The original Chapter 5 scene, renderer, and `CustomTagRendererFeature` are not
modified.

Open `CustomTagCapture.unity` and enter Play mode. The green ring is the captured
outline geometry drawn by the feature's debug overlay.

## The connection

Chapter 5's second shader pass declares:

```shaderlab
Tags { "LightMode" = "CustomOutlineTag" }
```

The renderer asset configures:

```text
Light Mode:  None
Shader Tags: CustomOutlineTag
Material:    None
Texture:     _Chapter5CustomOutlineMask
Exposure:    Frame Registry Only
```

`ObjectsToRenderTextureFeature` converts `CustomOutlineTag` into a `ShaderTagId`
and asks URP to draw only matching passes. The regular `UniversalForward` pass
still renders the capsule normally, while the expanded, front-face-culled outline
pass is drawn into the R8 mask texture.

No override material is used. The object's assigned material and its custom pass
provide the capture output. This subchapter uses a white `_OutlineColor` so the
R8 mask contains visible coverage.

The capture and debug preview run after prepasses but before opaque rendering.
The captured expanded geometry is a filled silhouette; the later normal capsule
draw covers its center on the camera, leaving the preview visible as a ring. A
later texture consumer still receives the filled silhouette mask.

## Comparison with the original Chapter 5 feature

The original `CustomTagRendererFeature` draws `CustomOutlineTag` directly into
the camera color target. This version draws the same tagged pass into a named
texture first. That is useful when the result must be processed, shared by
several later effects, sampled by a shader, or inspected independently.

Use the original direct renderer when the pass only needs to draw its final
pixels once. Use `ObjectsToRenderTextureFeature` when the tagged pass is producing
intermediate data for a wider effect pipeline.
