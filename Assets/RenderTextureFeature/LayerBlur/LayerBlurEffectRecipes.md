# Layer Blur Effect Recipes

This document gives practical setup steps and starting values for effects built with `LayerBlurFeature`.

The values are not magic constants. They are production-friendly starting points. Tune them after checking the mask with `Debug View`.

## Common Renderer Setup

Use this setup for every recipe unless the recipe says otherwise.

Renderer feature order:

```text
1. ObjectsToRenderTextureFeature
2. LayerBlurFeature
```

If you also use mask outline:

```text
1. ObjectsToRenderTextureFeature
2. MaskOutlineFeature
3. LayerBlurFeature
```

## Common Mask Output Values

For each blur effect, add one `RenderTextureOutputSettings` entry in `ObjectsToRenderTextureFeature`.

Use these default values:

| Setting | Value |
| --- | --- |
| `Material` | `LayerBlurMask` |
| `Material Pass Index` | `-1` |
| `Render Pass Event` | `AfterRenderingOpaques` |
| `Render Pass Input` | `Depth` |
| `Render Queue Lower Bound` | `0` |
| `Render Queue Upper Bound` | `2499` for opaque masks, `5000` for transparent mask objects |
| `Color Format` | `ARGB32` |
| `Texture Size Mode` | `Camera` |
| `Camera Size Multiplier` | `1` |
| `Filter Mode` | `Bilinear` |
| `Wrap Mode` | `Clamp` |
| `Sorting Criteria` | `CommonOpaque` |
| `Layer Mask` | The Unity layer for this effect |
| `Render Layer Mask` | Default, unless you intentionally filter with URP rendering layers |
| `Texture Name` | Unique name from the recipe |
| `Light Mode` | `Standard` |
| `Depth` | Enabled |
| `Write Depth` | Disabled |
| `Depth Compare` | `LessEqual` |
| `Debug View` | Enable only while testing |

Use a dedicated Unity layer for each effect or strength bucket. Example layer names:

```text
FrostedGlassBlur
ShieldBlur
CloakBlur
PortalBlur
NearBlur
FarBlur
```

The blur entry in `LayerBlurFeature` must use the exact same texture name as the matching mask output.

## Common Layer Blur Values

For each blur effect, add one `Blur Layer Settings` entry in `LayerBlurFeature`.

Use these default values when starting:

| Setting | Value |
| --- | --- |
| `Enabled` | Enabled |
| `Render Pass Event` | `AfterRenderingTransparents` |
| `Mask Threshold` | `0.5` |
| `Mask Softness` | `0.05` |
| `Opacity` | `1` unless the recipe says otherwise |

What the most important values do:

| Setting | Meaning |
| --- | --- |
| `Downsample` | Higher is cheaper and softer. `1` is sharp/full-res, `2` is a good default, `4` is cheap and soft. |
| `Iterations` | More passes create smoother blur and cost more. |
| `Blur Radius` | Larger radius spreads the blur farther. |
| `Mask Threshold` | Mask brightness where blur starts. Pure black always stays unblurred. |
| `Mask Softness` | Soft transition width above the threshold. |
| `Opacity` | Blend strength between original scene and blurred scene. |

## Debug Checklist

Before tuning the blur:

1. Enable `Debug View` on the matching mask output.
2. Confirm the target object is white.
3. Confirm the background is black.
4. Confirm the mask output `Texture Name` exactly matches the blur entry `Mask Texture Name`.
5. Disable `Debug View`.
6. Tune the blur values.

If blur appears on the wrong object, the issue is usually the mask output layer/filter setup, not the blur entry.

## Recipe 1: Frosted Glass

Use for windows, glass panels, frosted doors, cockpit glass, or blurred UI glass in world space.

The easiest way to understand this setup is to split the window into two jobs:

| Object | What the player sees | What the blur system sees |
| --- | --- | --- |
| Visible window mesh | The actual transparent glass/window art. | Usually ignored by the blur mask. |
| Blur proxy mesh | Invisible in the final camera. | Rendered white into `_FrostedGlassBlurMask`. |

The blur proxy is just a shape. It says: "blur the screen area covered by this shape."

When the window is closed:

```text
Blur proxy enabled -> mask is white over the window -> blur visible through the window
```

When the window is open:

```text
Blur proxy disabled or moved away with the window -> mask is black -> view is normal
```

Do not assign `LayerBlurMask` directly to the visible window mesh. `LayerBlurMask` is the override material used by `ObjectsToRenderTextureFeature` when it creates the offscreen mask. The visible window should keep its normal glass material.

Setup steps:

1. Create a Unity layer named `FrostedGlassBlur`.
2. Keep your visible window mesh on its normal layer and keep its normal transparent glass material.
3. Create a second mesh or quad that matches the glass shape. Name it `FrostedGlassBlurProxy`.
4. Put `FrostedGlassBlurProxy` on the `FrostedGlassBlur` layer.
5. Assign `LayerBlurInvisibleProxy` to the proxy mesh renderer.
6. Make sure the main camera culling mask includes the `FrostedGlassBlur` layer. The proxy material is invisible, so the player still will not see it.
7. Add a mask output in `ObjectsToRenderTextureFeature`.
8. Add one blur entry in `LayerBlurFeature`.

Scene object setup:

| GameObject | Layer | Mesh Renderer Material | Enabled when window is closed | Enabled when window is open |
| --- | --- | --- | --- | --- |
| `Window_Glass` | Your normal window layer | Your normal glass material | Yes | Usually yes, or animated open |
| `FrostedGlassBlurProxy` | `FrostedGlassBlur` | `LayerBlurInvisibleProxy` | Yes | No, or animated open with the window |

If the window rotates or slides open, make the proxy a child of the moving window object. Then either let it move with the window, or disable the proxy when the window reaches the open state.

Mask output values:

| Setting | Value |
| --- | --- |
| `Texture Name` | `_FrostedGlassBlurMask` |
| `Layer Mask` | `FrostedGlassBlur` |
| `Material` | `LayerBlurMask` |
| `Render Queue Upper Bound` | `5000` |
| `Sorting Criteria` | `CommonTransparent` |
| `Filter Mode` | `Bilinear` |
| `Depth` | Enabled |

Blur entry values:

| Setting | Value |
| --- | --- |
| `Name` | `Frosted Glass` |
| `Mask Texture Name` | `_FrostedGlassBlurMask` |
| `Downsample` | `2` |
| `Iterations` | `2` |
| `Blur Radius` | `3` |
| `Mask Threshold` | `0.5` |
| `Mask Softness` | `0.08` |
| `Opacity` | `0.85` |

Testing:

1. Enable `Debug View` on the `_FrostedGlassBlurMask` output.
2. Close the window.
3. You should see a white shape exactly where the glass/proxy is.
4. Open the window or disable `FrostedGlassBlurProxy`.
5. The debug mask should become black where the window used to be.
6. Disable `Debug View`.
7. Look through the closed window. The scene behind it should blur.
8. Open the window. The scene should look normal.

Tuning:

- Increase `Blur Radius` to `4-5` for stronger frosted glass.
- Lower `Opacity` to `0.6-0.75` if the glass should stay clearer.
- If the proxy itself becomes visible in the camera, check that its Mesh Renderer material is `LayerBlurInvisibleProxy`, not `LayerBlurMask`.
- If no blur appears, check that `_FrostedGlassBlurMask` is used both as the mask output `Texture Name` and the blur entry `Mask Texture Name`.

## Recipe 2: Magic Shield

Use for energy bubbles, protection domes, force fields, and magic barriers.

Setup steps:

1. Create a Unity layer named `ShieldBlur`.
2. Put the shield mesh or shield proxy mesh on `ShieldBlur`.
3. Add a mask output for `_ShieldBlurMask`.
4. Add one blur entry for the shield.

Mask output values:

| Setting | Value |
| --- | --- |
| `Texture Name` | `_ShieldBlurMask` |
| `Layer Mask` | `ShieldBlur` |
| `Render Queue Upper Bound` | `5000` |
| `Filter Mode` | `Bilinear` |
| `Depth Compare` | `LessEqual` |

Blur entry values:

| Setting | Value |
| --- | --- |
| `Name` | `Magic Shield` |
| `Mask Texture Name` | `_ShieldBlurMask` |
| `Downsample` | `2` |
| `Iterations` | `3` |
| `Blur Radius` | `4.5` |
| `Mask Threshold` | `0.35` |
| `Mask Softness` | `0.15` |
| `Opacity` | `0.7` |

Tuning:

- Increase `Mask Softness` for a softer shield edge.
- Lower `Opacity` for a subtle energy shimmer.
- Combine with `MaskOutlineFeature` for a strong shield rim.

## Recipe 3: Stealth Cloak

Use for cloaked characters, invisible enemies, stealth pickups, or camouflage effects.

Setup steps:

1. Create a Unity layer named `CloakBlur`.
2. Put the cloaked character renderer or a silhouette proxy on `CloakBlur`.
3. Add a mask output for `_CloakBlurMask`.
4. Add one subtle blur entry.

Mask output values:

| Setting | Value |
| --- | --- |
| `Texture Name` | `_CloakBlurMask` |
| `Layer Mask` | `CloakBlur` |
| `Render Queue Upper Bound` | `2499` for opaque character meshes, `5000` for transparent cloak meshes |
| `Filter Mode` | `Bilinear` |

Blur entry values:

| Setting | Value |
| --- | --- |
| `Name` | `Stealth Cloak` |
| `Mask Texture Name` | `_CloakBlurMask` |
| `Downsample` | `2` |
| `Iterations` | `1` |
| `Blur Radius` | `1.5` |
| `Mask Threshold` | `0.45` |
| `Mask Softness` | `0.08` |
| `Opacity` | `0.35` |

Tuning:

- Keep opacity low. Cloak effects look better when the blur is hinted rather than heavy.
- Use a slightly larger proxy mesh if you want the distortion to extend outside the character.

## Recipe 4: Portal Surface

Use for portal planes, magic doorways, dimensional windows, and scene transition surfaces.

Setup steps:

1. Create a Unity layer named `PortalBlur`.
2. Put the portal surface mesh on `PortalBlur`.
3. Add a mask output for `_PortalBlurMask`.
4. Add a stronger blur entry.

Mask output values:

| Setting | Value |
| --- | --- |
| `Texture Name` | `_PortalBlurMask` |
| `Layer Mask` | `PortalBlur` |
| `Render Queue Upper Bound` | `5000` |
| `Filter Mode` | `Bilinear` |

Blur entry values:

| Setting | Value |
| --- | --- |
| `Name` | `Portal` |
| `Mask Texture Name` | `_PortalBlurMask` |
| `Downsample` | `2` |
| `Iterations` | `3` |
| `Blur Radius` | `5.5` |
| `Mask Threshold` | `0.45` |
| `Mask Softness` | `0.04` |
| `Opacity` | `1` |

Tuning:

- Use `Downsample = 1` if the portal edge needs high-quality detail.
- Use `Downsample = 4` for a dreamier, cheaper portal.

## Recipe 5: Dream Or Memory Zone

Use for soft zones, memory areas, dream transitions, hallucination volumes, or screen-space mood areas.

Setup steps:

1. Create a Unity layer named `DreamBlur`.
2. Add a large proxy mesh that covers the world area or screen region.
3. Put that proxy on `DreamBlur`.
4. Add a mask output for `_DreamBlurMask`.
5. Add a broad, soft blur entry.

Mask output values:

| Setting | Value |
| --- | --- |
| `Texture Name` | `_DreamBlurMask` |
| `Layer Mask` | `DreamBlur` |
| `Render Queue Upper Bound` | `5000` |
| `Filter Mode` | `Bilinear` |

Blur entry values:

| Setting | Value |
| --- | --- |
| `Name` | `Dream Zone` |
| `Mask Texture Name` | `_DreamBlurMask` |
| `Downsample` | `4` |
| `Iterations` | `3` |
| `Blur Radius` | `6` |
| `Mask Threshold` | `0.25` |
| `Mask Softness` | `0.25` |
| `Opacity` | `0.6` |

Tuning:

- Increase `Mask Softness` for a smoother zone boundary.
- Lower `Opacity` if the player needs clear visibility.

## Recipe 6: Layer-Based Fake Depth Of Field

Use when you want art-directed foreground/background blur without a full depth-of-field post effect.

Setup steps:

1. Create layers named `NearBlur`, `MidBlur`, and `FarBlur`.
2. Assign objects or proxy shapes to those layers.
3. Add three mask outputs.
4. Add three blur entries in `LayerBlurFeature`.
5. Put the strongest blur last in the list if masks overlap.

Mask output values:

| Layer | Texture Name |
| --- | --- |
| `NearBlur` | `_NearBlurMask` |
| `MidBlur` | `_MidBlurMask` |
| `FarBlur` | `_FarBlurMask` |

Blur entry values:

| Name | Mask Texture Name | Downsample | Iterations | Blur Radius | Mask Threshold | Mask Softness | Opacity |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `Near Blur` | `_NearBlurMask` | `2` | `2` | `2` | `0.5` | `0.05` | `0.5` |
| `Mid Blur` | `_MidBlurMask` | `2` | `2` | `3.5` | `0.5` | `0.06` | `0.65` |
| `Far Blur` | `_FarBlurMask` | `4` | `3` | `6` | `0.5` | `0.08` | `0.8` |

Tuning:

- Use proxy shapes if changing real object layers would affect gameplay or physics.
- This is art-directed blur, not physically accurate camera depth of field.

## Recipe 7: Censor Or Privacy Blur

Use for hidden faces, secret text, spoilers, puzzle clues, signs, or sensitive areas.

Setup steps:

1. Create a Unity layer named `PrivacyBlur`.
2. Put the object to hide, or a proxy shape over it, on `PrivacyBlur`.
3. Add a mask output for `_PrivacyBlurMask`.
4. Add a strong blur entry.

Mask output values:

| Setting | Value |
| --- | --- |
| `Texture Name` | `_PrivacyBlurMask` |
| `Layer Mask` | `PrivacyBlur` |
| `Filter Mode` | `Bilinear` |

Blur entry values:

| Setting | Value |
| --- | --- |
| `Name` | `Privacy Blur` |
| `Mask Texture Name` | `_PrivacyBlurMask` |
| `Downsample` | `2` |
| `Iterations` | `4` |
| `Blur Radius` | `7.5` |
| `Mask Threshold` | `0.5` |
| `Mask Softness` | `0.02` |
| `Opacity` | `1` |

Tuning:

- Increase `Blur Radius` to `8` for stronger hiding.
- Keep `Mask Softness` low if the hidden area should have a sharp edge.

## Recipe 8: Water Or Glass Blur Base

Use for water sheets, thick glass, underwater windows, and soft refraction-style surfaces.

This recipe creates blur only. Animated distortion can be added later on top of the same mask system.

Setup steps:

1. Create a Unity layer named `WaterGlassBlur`.
2. Put the water or glass proxy mesh on that layer.
3. Add a mask output for `_WaterGlassBlurMask`.
4. Add a medium blur entry.

Mask output values:

| Setting | Value |
| --- | --- |
| `Texture Name` | `_WaterGlassBlurMask` |
| `Layer Mask` | `WaterGlassBlur` |
| `Render Queue Upper Bound` | `5000` |
| `Filter Mode` | `Bilinear` |

Blur entry values:

| Setting | Value |
| --- | --- |
| `Name` | `Water Glass` |
| `Mask Texture Name` | `_WaterGlassBlurMask` |
| `Downsample` | `2` |
| `Iterations` | `2` |
| `Blur Radius` | `2.5` |
| `Mask Threshold` | `0.35` |
| `Mask Softness` | `0.12` |
| `Opacity` | `0.55` |

Tuning:

- Use lower opacity for clear glass.
- Use higher opacity for underwater or thick glass.

## Recipe 9: Damage Or Status Aura

Use for poison haze, freeze aura, psychic effect, slow field, debuff zones, and character status effects.

Setup steps:

1. Create a Unity layer named `AuraBlur`.
2. Add a slightly larger proxy mesh around the character or zone.
3. Put the proxy on `AuraBlur`.
4. Add a mask output for `_AuraBlurMask`.
5. Add a medium-soft blur entry.

Mask output values:

| Setting | Value |
| --- | --- |
| `Texture Name` | `_AuraBlurMask` |
| `Layer Mask` | `AuraBlur` |
| `Render Queue Upper Bound` | `5000` |
| `Filter Mode` | `Bilinear` |

Blur entry values:

| Setting | Value |
| --- | --- |
| `Name` | `Status Aura` |
| `Mask Texture Name` | `_AuraBlurMask` |
| `Downsample` | `2` |
| `Iterations` | `2` |
| `Blur Radius` | `3.5` |
| `Mask Threshold` | `0.3` |
| `Mask Softness` | `0.2` |
| `Opacity` | `0.45` |

Tuning:

- Lower `Opacity` for gameplay readability.
- Combine with particle effects or outline for a stronger status read.

## Recipe 10: World-Space UI Background Blur

Use for in-world screens, dialogue panels, hologram panels, inventory boards, or diegetic UI.

Setup steps:

1. Create a Unity layer named `WorldUIBlur`.
2. Put the panel background mesh or a matching proxy quad on `WorldUIBlur`.
3. Add a mask output for `_WorldUIBlurMask`.
4. Add a blur entry for the panel background.

Mask output values:

| Setting | Value |
| --- | --- |
| `Texture Name` | `_WorldUIBlurMask` |
| `Layer Mask` | `WorldUIBlur` |
| `Render Queue Upper Bound` | `5000` |
| `Filter Mode` | `Bilinear` |

Blur entry values:

| Setting | Value |
| --- | --- |
| `Name` | `World UI Background` |
| `Mask Texture Name` | `_WorldUIBlurMask` |
| `Downsample` | `2` |
| `Iterations` | `2` |
| `Blur Radius` | `4` |
| `Mask Threshold` | `0.5` |
| `Mask Softness` | `0.05` |
| `Opacity` | `0.9` |

Tuning:

- Use `Downsample = 1` for crisp high-end UI panels.
- Lower `Opacity` if text readability drops because the background becomes too milky.

## Recipe 11: Heat Haze Base

Use for fire areas, exhaust vents, lava edges, engine heat, or hot desert air.

This recipe gives a soft heat blur base. Real heat shimmer needs animated distortion added later.

Setup steps:

1. Create a Unity layer named `HeatBlur`.
2. Put a plane, cone, or soft proxy shape over the heat area.
3. Add a mask output for `_HeatBlurMask`.
4. Add a cheap subtle blur entry.

Mask output values:

| Setting | Value |
| --- | --- |
| `Texture Name` | `_HeatBlurMask` |
| `Layer Mask` | `HeatBlur` |
| `Render Queue Upper Bound` | `5000` |
| `Filter Mode` | `Bilinear` |

Blur entry values:

| Setting | Value |
| --- | --- |
| `Name` | `Heat Haze Base` |
| `Mask Texture Name` | `_HeatBlurMask` |
| `Downsample` | `4` |
| `Iterations` | `1` |
| `Blur Radius` | `1.5` |
| `Mask Threshold` | `0.25` |
| `Mask Softness` | `0.3` |
| `Opacity` | `0.35` |

Tuning:

- Keep it subtle. Strong heat haze blur can look like a mistake without animated distortion.
- Use a soft proxy shape so the haze fades naturally.

## Recipe 12: Cinematic Layer Blur

Use for cutscenes, screenshots, photo mode, gameplay focus, or stylized scene composition.

Setup steps:

1. Create layers named `CinematicSoftBlur` and `CinematicHeavyBlur`.
2. Put non-important objects or proxy regions on those layers.
3. Add two mask outputs.
4. Add two blur entries.
5. Keep the main gameplay subject out of all blur layers.

Mask output values:

| Layer | Texture Name |
| --- | --- |
| `CinematicSoftBlur` | `_CinematicSoftBlurMask` |
| `CinematicHeavyBlur` | `_CinematicHeavyBlurMask` |

Blur entry values:

| Name | Mask Texture Name | Downsample | Iterations | Blur Radius | Mask Threshold | Mask Softness | Opacity |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `Cinematic Soft` | `_CinematicSoftBlurMask` | `2` | `2` | `2.5` | `0.5` | `0.08` | `0.55` |
| `Cinematic Heavy` | `_CinematicHeavyBlurMask` | `4` | `3` | `6.5` | `0.5` | `0.12` | `0.75` |

Tuning:

- Use fewer layers for gameplay.
- Use more aggressive blur only for cutscenes, screenshots, or photo mode.

## Recipe 13: Side-Scroller Background Depth Blur

Use for side-scroller or 2.5D scenes where the far background should be soft, the midground should be slightly soft, and the foreground/gameplay layer should stay sharp.

The idea is:

```text
Background layer -> heavy blur mask -> strong blur
Midground layer  -> light blur mask -> soft blur
Foreground layer -> opacity 0 entry -> sharp restore
```

Important: the blur is screen-space. It blurs the screen area covered by masks. If a broad background mask sits behind the whole level, it can cover the same screen pixels as midground or foreground objects. Put later/front entries later in the list so they own overlapping pixels.

Setup steps:

1. Create Unity layers named `BackgroundHeavyBlur`, `MidgroundLightBlur`, and `ForegroundSharp`.
2. Put far background objects, sprites, tilemaps, or proxy shapes on `BackgroundHeavyBlur`.
3. Put midground objects, sprites, tilemaps, or proxy shapes on `MidgroundLightBlur`.
4. Put gameplay characters, pickups, enemies, and front props on `ForegroundSharp`.
5. Add three mask outputs in `ObjectsToRenderTextureFeature`.
6. Add three blur entries in `LayerBlurFeature`.
7. Set the foreground entry `Opacity` to `0` so it restores the original sharp scene inside that mask.

Mask output values:

| Purpose | Layer Mask | Texture Name | Render Queue Upper Bound |
| --- | --- | --- | --- |
| Heavy background blur | `BackgroundHeavyBlur` | `_BackgroundHeavyBlurMask` | `2499` for opaque 3D, `5000` for sprites/transparent objects |
| Light midground blur | `MidgroundLightBlur` | `_MidgroundLightBlurMask` | `2499` for opaque 3D, `5000` for sprites/transparent objects |
| Sharp foreground blocker | `ForegroundSharp` | `_ForegroundSharpMask` | `2499` for opaque 3D, `5000` for sprites/transparent objects |

Use these values for both mask outputs:

| Setting | Value |
| --- | --- |
| `Material` | `LayerBlurMask` |
| `Render Pass Event` | `AfterRenderingOpaques` for depth-writing 3D, or a later event if your sprites are only available later |
| `Render Pass Input` | `Depth` |
| `Texture Size Mode` | `Camera` |
| `Camera Size Multiplier` | `1` |
| `Filter Mode` | `Bilinear` |
| `Wrap Mode` | `Clamp` |
| `Depth` | Enabled when using depth-writing 3D/2.5D objects |
| `Write Depth` | Disabled |
| `Depth Compare` | `LessEqual` |

Blur entry values:

| Name | Mask Texture Name | Downsample | Iterations | Blur Radius | Mask Threshold | Mask Softness | Opacity |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `Background Heavy Blur` | `_BackgroundHeavyBlurMask` | `4` | `3` | `6` | `0.5` | `0.08` | `0.85` |
| `Midground Light Blur` | `_MidgroundLightBlurMask` | `2` | `1` | `2` | `0.5` | `0.05` | `0.55` |
| `Foreground Sharp Restore` | `_ForegroundSharpMask` | `1` | `1` | `0` | `0.5` | `0.03` | `0` |

Recommended `Blur Layer Settings` order:

```text
1. Background Heavy Blur
2. Midground Light Blur
3. Foreground Sharp Restore
```

This lets the lighter midground blur win over the heavy background blur, and lets the foreground restore the original sharp scene where its mask exists.

List-order composition makes the layers exclusive where their masks are fully opaque:

```text
Background blur is removed wherever midground or foreground exists.
Midground blur is removed wherever foreground exists.
Foreground opacity is 0, so it restores the original sharp scene.
```

For 3D or 2.5D objects:

- Keep `Depth` enabled on the mask outputs.
- Make sure foreground objects write depth if they should block background blur.
- Keep `Camera Size Multiplier = 1` when using depth.

For pure 2D transparent sprites:

- Transparent sprites often do not write depth, so a background mask can still cover screen pixels behind a foreground sprite.
- Add `Foreground Sharp Restore` as the final entry with `Opacity = 0` so foreground screen pixels restore the original scene.
- Another safe setup is to render foreground/gameplay sprites after `LayerBlurFeature`, or use a separate foreground camera/layer drawn after the blur.
- If a mask output is empty for sprites, add the sprite shader pass tag to `Shader Tags`, commonly `Universal2D` for URP 2D projects.
- Use `Debug View` on each mask and confirm foreground character areas are black if they must stay sharp.

Tuning:

- Increase background `Blur Radius` to `7-8` for very distant painted backgrounds.
- Lower background `Opacity` to `0.6-0.7` if the game becomes hard to read.
- Increase midground `Blur Radius` to `3` if it still feels too sharp.
- Keep foreground as the final `Opacity = 0` entry when it should stay sharp.

## Multiple Effects At The Same Time

You can run multiple recipes together.

Rules:

1. Every mask output needs a unique `Texture Name`.
2. Every blur entry must use the matching `Mask Texture Name`.
3. Keep all blur entries enabled only when needed.
4. Order entries from lower priority/background to higher priority/foreground.
5. Entries with `Opacity = 0` act as sharp restore layers. They skip blur work but still run one composite pass.

Suggested list order when combining effects:

```text
1. World UI background blur
2. Frosted glass
3. Water/glass blur
4. Stealth cloak
5. Magic shield
6. Portal
7. Censor/privacy blur
```

This keeps practical UI/glass blur lower in the stack and high-priority effects later.

## Quick Tuning Guide

If the blur leaks outside the object:

- Enable mask `Debug View`.
- Confirm background is black.
- Increase `Mask Threshold`.
- Decrease `Mask Softness`.
- Check the mask output `Layer Mask`.

If blur is too expensive:

- Increase `Downsample`.
- Decrease `Iterations`.
- Decrease `Blur Radius`.
- Disable unused blur entries.

If blur looks blocky:

- Lower `Downsample`.
- Increase `Iterations`.
- Use `Filter Mode = Bilinear` on the mask output.

If blur does not respect walls:

- Enable `Depth` on the mask output.
- Use `Depth Compare = LessEqual`.
- Keep `Texture Size Mode = Camera`.
- Keep `Camera Size Multiplier = 1`.
