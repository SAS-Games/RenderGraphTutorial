# Chapter 15 - Target Scanner

This chapter composes the existing object-mask, halo, and outline effects into an enemy scanner that remains visible through level geometry.

## Reused Pipeline

1. `TargetScannerDemoController` adds the shared `Effect Mask Primary` rendering-layer bit to the assigned target renderers while scanner vision is active.
2. `RenderObjectsToTextureFeature` captures the target into `_TargetScannerMask` with an `Always` depth comparison.
3. `MaskHaloFeature` adds an orange warning glow through the occluding wall.
4. `MaskOutlineFeature` adds a readable outer silhouette.

```text
Target behind wall
    -> depth-independent target mask
    -> warning halo + silhouette outline
    -> camera color
```

## Controls

| Input | Action |
| --- | --- |
| `WASD` | Move the player relative to the camera |
| `Q` | Toggle scanner vision |

## Setup Contract

The renderer is registered as index `12` in `PC_RPAsset`, and the scene camera uses index `12`. The mask pass accepts every GameObject layer but filters on the shared `Effect Mask Primary` rendering layer. This keeps gameplay layers such as `Player` independent from render-effect selection.

Assign the target root directly on `TargetScannerDemoController`. The controller preserves every renderer's original rendering-layer mask, adds the scanner bit while active, and restores the original value when scanning is disabled.

The example disables camera occlusion culling so the target remains available to the mask renderer even when completely hidden behind the demonstration wall.

## Reused Assets

- Chapter 12 environment
- Two references to the same Huscarl character prefab
- FSM-driven Player prefab and the Chapter 13 follow-camera script
- Shared `ObjectMask` material
- Existing halo and outline features and materials

No shader, HLSL, material, or character prefab is duplicated. In a production game, call `SetScannerActive(bool)` from the player ability or detection system.
