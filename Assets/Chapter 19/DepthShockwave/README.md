# Chapter 19 - Depth-Aware Shockwave

This example reconstructs the visible scene position from the camera depth texture and intersects it with an expanding world-space sphere. The resulting shockwave crosses floors, walls, characters, and props according to their actual distance from the impact point.

## Try the demo

Open `DepthShockwave.unity` and enter Play mode. A shockwave emits from the character automatically every 2.25 seconds. Press **Space** to emit additional waves; up to eight may overlap.

The camera uses renderer index 22 (`DepthShockwaveRenderer`). The renderer contains one `DepthShockwaveFeature`, which requests camera depth and runs after transparents.

## Gameplay API

Emit from any gameplay system without creating a component:

```csharp
DepthShockwave.Emit(hitPoint, maxRadius: 12f, duration: 1.35f);
```

`DepthShockwaveEmitter` is an optional scene component that exposes the same operation, supports an offset transform, and provides automatic or keyboard triggering for demonstrations.

The event clock uses `Time.unscaledTimeAsDouble`, so pausing or changing gameplay time scale does not stall or stretch the visual. Change the runtime API to scaled time if the effect should obey slow motion.

## Render pipeline

1. `FrameColorSnapshotPass` captures the camera color before distortion.
2. `DepthShockwavePass` requests and reads URP's camera depth texture.
3. The shader reconstructs a world position for every visible pixel with `UNITY_MATRIX_I_VP`.
4. Each active event measures the distance from that position to its impact center.
5. Pixels near the expanding spherical radius receive signed refraction, chromatic separation, and an emissive ring.

Sky pixels are rejected because they have no finite scene surface. Occluded geometry is naturally excluded because only the nearest visible depth is reconstructed.

## Important settings

- **Ring Width** and **Edge Softness** are world-space distances, not pixels. Their apparent width therefore changes naturally with perspective.
- **Secondary Ring Offset** places a weaker trailing ring behind the main wave. Set its strength to zero for one clean shell.
- **Distortion Pixels** controls signed push/pull refraction at the ring edges.
- **Chromatic Pixels** offsets red and blue samples around the refracted source.
- **Wave Color** is HDR. Bloom can make the emissive edge much brighter when post-processing is enabled.
- **Maximum Simultaneous Shockwaves** caps shader loop cost between one and eight.
- **Intensity** scales the complete presentation without changing event radius or duration.

## Depth limitations

- Transparent materials usually do not write camera depth, so their visible color distorts according to the opaque surface behind them.
- The wave uses the current visible depth only; it cannot reveal hidden geometry behind walls.
- Overlay cameras are skipped. Camera-stack support requires choosing which camera owns the color snapshot and depth texture.
- The current world-position reconstruction is designed for standard URP cameras. XR projection and single-pass stereo should be validated and adjusted for the target device.

## Debugging

- If nothing appears, confirm the camera uses `DepthShockwaveRenderer` and that `DepthShockwaveEmitter` is enabled in Play mode.
- Set **Emission Intensity** high and **Distortion Pixels** to zero to isolate world-position reconstruction from refraction.
- Set **Ring Width** to `1` temporarily if a small or distant impact is hard to see.
- Use the Frame Debugger or RenderGraph viewer to confirm `Depth Shockwave Source` and `Depth Shockwave` execute while an event is active.
