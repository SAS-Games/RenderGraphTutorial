# Chapter 20 - Damage Freeze / Time Echo

This example captures the character's current animated pose as independent world-space geometry. The live character continues moving while the frozen copy holds briefly, recolors, distorts, rises, and dissolves.

It is deliberately not a RenderGraph history effect. A frozen geometry snapshot keeps correct perspective and depth when the camera moves, which is the defining difference from motion blur and temporal trails.

## Try the demo

Open `DamageFreezeTimeEcho.unity` and enter Play mode. An echo captures automatically every 1.25 seconds. Move or attack between captures to make the frozen poses obvious. Press **F** to capture additional echoes manually.

The scene uses renderer index 0 because no custom renderer feature is required.

## Gameplay integration

Add `DamageFreezeEchoEmitter` to a gameplay object, assign the character root and `DamageFreezeEcho.mat`, then invoke:

```csharp
echoEmitter.Capture();
```

Call it from a damage callback, parry event, teleport ability, death transition, or animation event. `Capture()` returns the spawned `DamageFreezeEchoInstance`, allowing gameplay code to remove it early with `DisposeNow()`.

For production gameplay, disable **Capture On Start** and **Auto Repeat**. Those options exist only to make the chapter scene self-demonstrating.

## Capture process

1. Every currently visible `SkinnedMeshRenderer` beneath the source is baked into a new static mesh at its exact animated pose.
2. Every visible `MeshRenderer` is copied using its existing shared mesh, which avoids duplicating rigid equipment geometry.
3. Snapshot parts preserve their world transforms, Unity layer, rendering-layer mask, and submesh count.
4. They receive one shared echo material through every material slot.
5. A `MaterialPropertyBlock` supplies per-echo color, dissolve state, distortion, drift, and random seed without cloning the material.
6. Generated skinned meshes are destroyed when the echo expires. Original shared rigid meshes are never destroyed.

Shadows, reflection probes, light probes, and motion vectors are disabled on echoes to keep them stable and inexpensive.

## Important settings

- **Hold Duration** controls how long the captured pose remains completely frozen before dissolving.
- **Dissolve Duration** controls cleanup time after the hold.
- **Echo Tint** and **Edge Color** are HDR and may feed bloom when post-processing is enabled.
- **Dissolve Edge Width** controls the bright boundary between visible and removed fragments.
- **Noise Scale** changes the size of dissolving chunks in stable world space.
- **Distortion Strength / Frequency** push vertices along their frozen normals as the dissolve progresses.
- **Vertical Drift** moves shader vertices without changing the echo GameObject transform.
- **Surface Offset** slightly expands the echo and prevents z-fighting while it initially overlaps the live character.
- **Maximum Active Echoes** removes the oldest echo before creating another when the cap is reached.

All lifetime animation uses unscaled time, so the echo can continue during hit-stop. Replace `Time.unscaledTime` with scaled time if it should pause with gameplay.

## Performance and limitations

- Baking a skinned mesh is a CPU operation and allocates a new mesh. Trigger on meaningful events, not every frame.
- Several body parts mean several generated renderers and draw calls per echo. Pool snapshot objects for combat scenarios that emit frequently.
- The supplied material intentionally renders a stylized solid recolor rather than reproducing each original surface texture.
- Blend shapes and the current skinned pose are included by `BakeMesh`; particles, trails, decals, cloth simulation state, and renderer-specific material properties are not captured.
- Strong non-uniform transform scaling can require a project-specific matrix bake instead of the convenient `lossyScale` reconstruction used here.

## Comparison

- **Motion blur** samples the current frame along velocity and lasts one rendered frame.
- **Temporal trail** continuously accumulates recent screen pixels and fades them.
- **Damage Freeze / Time Echo** captures one intentional gameplay moment as independently controlled world-space geometry.
