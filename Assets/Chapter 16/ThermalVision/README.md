# Chapter 16 - Thermal Vision

This chapter turns the camera into a simulated heat sensor. The environment is remapped to a cool blue palette while selected living targets are rendered as animated red, orange, yellow, and white heat signatures.

## Behaviour

- `Q` or PlayStation `Triangle` toggles thermal vision with a short fade.
- `R` switches between physically plausible visibility and the optional game-style through-wall mode.
- Only the demonstration target is configured as a heat source; the Player is preserved by the player-exclusion mask.
- The target behind the wall is hidden in the default physical mode and revealed in through-wall mode.

Real thermal cameras normally measure infrared energy arriving from a visible surface; they do not provide X-ray vision through ordinary solid walls. The through-wall option is intentionally labelled as a stylized gameplay mode.

## Reused Pipeline

1. `ThermalVisionDemoController` adds `Effect Mask Primary` to configured heat-source Renderers without changing their GameObject layers. In the included scene, only `Thermal Target` is configured; the playable `Player` keeps its original appearance.
2. One `RenderObjectsToTextureFeature` creates two heat masks with the shared `ThermalSource` material:
   - `_ThermalVisibleMask` uses `LessEqual` depth testing.
   - `_ThermalThroughWallMask` uses `Always` depth testing.
3. The same feature creates `_ThermalPlayerMask` from Unity layer 16 (`Player`) with the shared flat `ObjectMask` material.
4. `ThermalVisionFeature` applies the cool thermal palette to the environment and the hot palette to the selected heat mask, then restores the original camera color inside `_ThermalPlayerMask`.
4. The existing `MaskHaloFeature` adds a soft visible-surface heat bloom; no blur implementation is duplicated.

```text
Heat-source Renderers
    -> visible heat mask ---------+
    -> through-wall heat mask ----+-> thermal palette composite -> camera color
                                   +-> shared halo blur
```

## Setup Contract

- The renderer is registered as index `13` in `PC_RPAsset`, and the scene camera uses index `13`.
- Both mask outputs accept every GameObject layer and filter only on `Effect Mask Primary`.
- Assign heat-source roots and normalized heat values on `ThermalVisionDemoController`.
- The controller preserves and restores original Rendering Layer Masks and MaterialPropertyBlocks.
- `Through Walls On Start` controls Play mode. The demo keeps `Editor Preview Intensity` at zero because heat-source rendering-layer membership is owned by the runtime controller.

The heat values are simulated art direction, not temperatures measured from the source materials. In production, gameplay status, damage, invisibility, or environmental exposure can drive each source's `_ThermalHeat` value.
