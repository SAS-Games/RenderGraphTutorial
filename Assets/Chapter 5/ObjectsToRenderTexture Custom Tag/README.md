# Chapter 5 Subchapter - Capturing the Player Outline

Chapter 5 now demonstrates the same player-only outline through two render paths:

1. `Chapter5.unity` draws a yellow outline directly into the camera color target
   with `CustomTagRendererFeature`.
2. `CustomTagCapture.unity` draws it into `_Chapter5CustomOutlineMask` with
   `ObjectsToRenderTextureFeature`, then shows that texture with the green debug
   overlay.

Both renderer assets filter Unity layer 16 (`Player`), so the environment is not
included. Both use pass 1 (`CustomOutlinePass`) from
`Custom/URPCustomTagMultiPass` as an override material pass. The player's normal
URP materials remain assigned and render its regular appearance.

## Why the player uses an override material

The original Chapter 5 capsule used the multipass material directly, so the
feature could select its `CustomOutlineTag` pass from the object's own material.
The player is assembled from several renderers with regular URP materials; those
materials do not declare `CustomOutlineTag`. Selecting only that tag would
therefore draw nothing.

For the player examples, the renderer list selects the standard URP forward
passes to find all player renderers, and then replaces the selected pass with the
outline material's custom pass. This keeps the custom outline shader while
preserving all original player materials.

## Render-to-texture settings

```text
Layer Mask:    Player
Light Mode:    Standard
Material:      CustomTagCapture
Material Pass: 1
Texture:       _Chapter5CustomOutlineMask
Exposure:      Frame Registry Only
```

The capture material outputs white into the R8 mask. The capture runs after
opaque rendering and reads camera depth without writing to it. Player pixels in
the center reject the expanded back faces, while the visible outer shell becomes
the outline mask. The debug overlay then displays that mask in green after
transparent rendering.

Use the direct Chapter 5 renderer when the outline only needs to reach the final
camera once. Use the render-to-texture version when later effects need to sample,
process, share, or independently inspect the player mask.
