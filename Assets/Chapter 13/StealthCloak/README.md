# Chapter 13 - Stealth Cloak

This chapter creates a playable character-shaped cloaking shimmer by composing existing masked effects. It reuses the Chapter 12 environment, the Huscarl character prefab, and all existing effect shaders and materials.

## Reused Pipeline

1. `StealthCloakDemoController` switches every renderer under the animated Huscarl to the shared `InvisibleMaskProxy` material and adds the `Effect Mask Primary` rendering-layer bit.
2. `RenderObjectsToTextureFeature` renders those animated meshes into `_StealthCloakMask` with the shared `ObjectMask` material.
3. `MaskDistortionFeature` samples the background with fast, high-frequency offsets inside the character silhouette.
4. `MaskHaloFeature` adds a restrained cyan aura and crisp rim that reveals the cloaked shape.

```text
Invisible animated character meshes
    -> _StealthCloakMask
    -> animated background distortion
    -> subtle aura and rim
    -> camera color
```

## Included Assets

- `StealthCloak.unity`: playable cloaking example built in the Chapter 12 environment.
- `StealthCloakRenderer.asset`: renderer containing the mask, distortion, and halo stack.
- `Scripts/StealthCloakDemoController.cs`: Input System movement and runtime cloak toggle.
- `Scripts/StealthCloakFollowCamera.cs`: fixed follow camera for the playable demo.
- `Assets/Huscarl/Prefabs/Huscarl.prefab`: reused character, rig, animator, and `CharacterController`.
- `Assets/RenderTextureFeature/Core/MaskedEffect/ObjectMask.mat`: shared mask override.
- `Assets/RenderTextureFeature/Core/MaskedEffect/InvisibleMaskProxy.mat`: shared invisible proxy material.

## Setup Contract

The renderer is registered as index `10` in `PC_RPAsset`, and the scene camera uses index `10`. While cloaked, the controller preserves each renderer's existing mask and adds `Effect Mask Primary`, which the `_StealthCloakMask` output captures. The character's GameObject layer is unchanged. The prefab's legacy-input combat controller is disabled only in this scene because the project uses Unity's new Input System.

## Controls

| Input | Action |
| --- | --- |
| `WASD` | Move the character relative to the camera |
| `C` | Toggle the stealth cloak |

## Starting Values

| Setting | Value |
| --- | --- |
| Distortion strength | `5 px` |
| Frequency | `32` |
| Speed | `1.2` |
| Chromatic separation | `0.6 px` |
| Distortion opacity | `0.7` |
| Halo iterations | `3` |
| Halo radius | `1.5` |
| Rim width | `2 px` |

For a quieter cloak, reduce distortion opacity and rim intensity. For a damaged or unstable cloak, increase distortion strength, speed, and chromatic separation.

For a production game, call `SetCloaked(bool)` from the ability, energy, or enemy-detection system instead of relying on the demo `C` key.
