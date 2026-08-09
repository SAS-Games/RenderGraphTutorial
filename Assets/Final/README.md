# Final Chapter - Render Feature Trials

`CustomRenderFeatureDemo` turns the individual tutorial effects into a playable arena. The Final scene keeps one active renderer stack at a time and switches the Main Camera to the chapter renderer required by the current trial.

## Gameplay Structure

- Early stations explain halo, custom shader tags, inverted-hull outlines, mask outlines, layer blur, Jump Flood distance fields, frosted glass, distortion, and portal composition.
- `C` activates the stealth crossing.
- `E` raises the energy shield in the hazard lane.
- `Q` activates the through-wall target scanner behind cover.
- `T` activates thermal vision in the heat-source maze.
- Holding `Left Shift` activates temporal trails through the rush gates.
- `F` is the universal activation key at every station. `Escape` returns to the default renderer.

## Scene Construction

The authored scene now contains a `78 x 66` arena floor. `FinalShowcaseDirector` expands it at runtime with:

- Collision boundaries around the expanded ground.
- Fourteen colored feature stations.
- Cover walls, thermal maze walls, sentry markers, shield hazards, and rush gates.
- Seven enemy placeholders cloned from the current Player prefab.
- Invisible mask proxies for blur, distortion, portal, frosted glass, and shield effects.
- A lightweight HUD that explains what each feature contributes to gameplay.

Enemy clones are created below an inactive hierarchy, stripped of gameplay/input behaviour, then enabled as static animated targets. Replace `Enemy Prefab` later without changing the trials.

## Renderer Strategy

The PC pipeline already registers the chapter renderers at indices `1` through `14`. The director calls `UniversalAdditionalCameraData.SetRenderer` when a trial activates and returns to renderer `0` afterward. This avoids running every fullscreen feature simultaneously and keeps each chapter's original mask names, materials, and ordering intact.

The shared `Effect Mask Primary` rendering-layer bit selects enemies, the Player, or an invisible proxy according to the active trial. Layer Blur additionally uses `Effect Mask Secondary` for its heavier blur volume.
