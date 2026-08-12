# Chapter 18 - Selective Motion Blur

This example applies motion blur only to renderers selected by a Rendering Layer Mask. It keeps the background sharp while fast characters or props receive a directional blur.

## Try the demo

Open `SelectiveMotionBlur.unity` and enter Play mode. Move the character and press **Q** to toggle the character's blur rendering layer.

The camera uses renderer index 21 (`SelectiveMotionBlurRenderer`). Its features must remain in this order:

1. **Selective Motion Blur Mask** renders objects on Rendering Layer bit 2 into `_SelectiveMotionBlurMask`.
2. **Selective Motion Blur** consumes that mask, the camera motion-vector texture, and a snapshot of the frame color.

In Play mode, the demo controller adds Rendering Layer bit 2 without replacing any other rendering-layer bits already used by the character, and restores the original masks when the scene stops.

## How it works

The effect uses three RenderGraph passes:

1. **Tile maximum** finds the strongest selected-object velocity in each screen tile.
2. **Neighbor maximum** expands that velocity to adjacent tiles so the blur may extend outside the moving silhouette.
3. **Composite** gathers the captured frame color along the dominant velocity and blends it through the selection mask.

This is a compact reconstruction blur rather than a temporal accumulation effect. It therefore avoids the long-lived afterimages used by Chapter 17.

## Important settings

- **Shutter Duration** is measured in seconds. The motion-vector scale is derived from `Shutter Duration / unscaledDeltaTime`, so the apparent blur length remains much more consistent between low- and high-refresh-rate devices. `1/30` second is the deliberately strong demo default.
- **Motion Vector Scale** calibrates Unity's UV-space motion vectors after shutter compensation. The demo uses `2`, matching the scale used by URP's object-motion-blur path.
- **Max Blur Pixels** caps the blur radius and protects both image quality and performance during motion spikes.
- **Sample Count** controls the full-resolution gather cost. Start around 8-12 on mobile and 12-16 on desktop.
- **Tile Size** trades precision for the cost of the two velocity-reduction passes. Eight pixels is a practical default.
- **Mask Threshold / Softness** reject compression or filtering noise around the mask edge.
- **Intensity** blends between the original frame and the reconstructed blur.

## Motion-vector requirements

The renderer and material shader must generate valid motion vectors. While the demo toggle is active, its controller forces selected renderers to `MotionVectorGenerationMode.Object` and restores their original modes afterward. Skinned meshes and shaders without a supported MotionVectors pass can still produce weak or missing object blur. Transparent materials also commonly lack useful motion vectors, so begin with opaque objects when diagnosing the effect.

Camera motion is still present in the motion-vector texture, but the selection mask prevents it from blurring the whole screen.

The demo camera is intentionally fixed. A tracking camera keeps the character nearly stationary on screen and therefore removes most of its translational motion vector, even though the character is moving through the world.

## Debugging

- Enable **Debug View** on the mask feature to confirm that only the intended object is white.
- If the mask is correct but there is no blur, inspect the camera motion-vector texture and the object's motion-vector settings.
- If the blur reaches too far into nearby geometry, reduce **Tile Size**, **Max Blur Pixels**, or **Intensity**.
- Keep the mask pass before the blur pass and keep both texture names set to `_SelectiveMotionBlurMask`.

The effect runs on base cameras and is disabled in Scene view by default. Camera stacking and transparent motion vectors require project-specific handling.
