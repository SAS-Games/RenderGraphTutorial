# Chapter 17 - Selective Temporal Trails

This chapter adds a motion-compensated afterimage to selected objects without ghosting the rest of the scene. The Player is the trail source, so locomotion and combat animations immediately expose how temporal accumulation behaves.

## Behaviour

- Move and attack normally to generate cyan-blue afterimages from the Player.
- `Q` toggles whether the Player contributes new samples to the history.
- Turning the source off does not abruptly erase the existing trail; stored samples decay naturally.
- Large camera jumps, resolution changes, and skipped frames invalidate history instead of smearing stale pixels across the screen.

## Pipeline

1. `TemporalTrailDemoController` adds `Effect Mask Primary` to the Player's Renderers without changing GameObject layers.
2. `RenderObjectsToTextureFeature` draws `_TemporalTrailMask` using the shared object-mask material.
3. `FrameColorSnapshotPass` preserves the unmodified camera color.
4. `TemporalTrailPass` requests `TemporalTrailHistory` from URP's per-camera history manager.
5. `TemporalTrailHistory : CameraHistoryItem` allocates a two-frame `BufferedRTHandleSystem` history. URP rotates its current/previous textures and owns their lifetime.
6. The accumulation pass reprojects the previous history with URP motion vectors, applies time-based decay, and writes current masked object color.
7. The composite pass adds the stored trail over the preserved camera image while suppressing it beneath the object's current silhouette.

```text
Selected Player Renderers -> _TemporalTrailMask --------------------+
                                                                   |
Camera color -> frame snapshot -> accumulation -> history A/B -----+-> composite -> camera color
                                  ^             |
                                  + motion vectors
```

Only selected object color and coverage are stored in history. This is why the environment remains stable even though the effect uses previous frames.

## Renderer Setup

`TemporalTrailRenderer` contains these features in order:

1. Screen Space Ambient Occlusion
2. Render Objects To Texture (`_TemporalTrailMask`)
3. Temporal Trail

The renderer is registered as index `14` in `PC_RPAsset`, and the demo camera selects index `14`.

The temporal pass runs at `AfterRenderingTransparents`. It requests motion vectors only when `Motion Compensation` is enabled. The mask producer must run before that point.

## Important Settings

- **History Resolution Scale** controls the two persistent history buffers. `0.5` is a useful quality/performance compromise.
- **Half Life** is frame-rate independent: it specifies how long a trail takes to lose half its strength.
- **Capture Interval** controls snapshot cadence. `0` records continuously; `0.1` records ten discrete silhouettes per second.
- **Motion Vector Scale** controls reprojection. `1` uses URP motion vectors directly; `0` leaves history in screen space.
- **Camera Cut Distance/Angle** reject history after discontinuous camera motion.
- **Suppress Current Frame** keeps the effect behind the live object instead of tinting it.

## Render Graph Lessons

- Frame-local `TextureHandle` resources cannot be retained for the next frame.
- `CameraHistoryItem` stores persistent resources outside Render Graph while `UniversalCameraHistory` scopes them to each camera.
- `RequestAccess<T>()` keeps a history type alive; `GetHistoryForWrite<T>()` creates/marks it for the current frame.
- `BufferedRTHandleSystem` rotates current/previous textures before rendering, removing manual ping-pong bookkeeping.
- Persistent `RTHandle` resources still must be imported into Render Graph every frame.
- A pass cannot safely sample and render into the same texture, so the implementation ping-pongs between two history targets.
- The accumulation and composite passes explicitly declare all source, mask, motion, history, and attachment access.
- Each camera's `UniversalAdditionalCameraData` owns its history manager, preventing temporal data from leaking between cameras.

## Limitations and Extensions

This tutorial stores visible camera color inside the selected silhouette. It does not reconstruct newly exposed surfaces, and transparent trail sources depend on how they appear in the camera snapshot and mask.

Useful extensions include neighborhood clamping, depth rejection, separate trail colors per source, a history debug view, or replacing additive composition with alpha/screen blend modes.
