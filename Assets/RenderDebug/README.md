# Shader & Render Debugger

This module is a passive, development-only observer for intentionally published render stages. Production rendering owns every input and pass; the debugger only registers metadata and, when requested, copies a stage into transient debugger-owned storage.

## Architecture

```text
Rendering effect
    -> IRenderDebugContext
        -> RenderDebugRegistry (metadata, requests, latest frame data)
        -> RenderDebugTextureCaptureService (owned live/captured copies)
            -> RenderDebuggerWindow (Editor only)
```

`RenderDebugService` is the one process-local bridge. A shared bridge is necessary because unrelated renderer features and an EditorWindow have no common Unity object lifecycle. It owns no production resource and performs no GPU work without an active viewer request.

## Layout

```text
RenderDebug/
  Runtime/
    Core/       public contracts and immutable descriptors
    Capture/    registry, request state, and owned texture copies
    Rendering/  context, session, and RenderGraph/RTHandle adapters
  Editor/       window, sequence cards, preview, comparison, pixel probe
  Shaders/      internal channel/exposure/difference preview shader
  Tests/        registry edit-mode tests
```

Runtime and Editor code use separate assembly definitions. Player assemblies never reference `UnityEditor`.

## Enabling effect integration

Add `RENDER_DEBUG` to **Project Settings > Player > Scripting Define Symbols** for the Editor configuration that needs capture. `RenderDebugSourceMarker.Publish(...)` uses `Conditional("RENDER_DEBUG")`, so publication calls and their argument evaluation are removed from non-debug builds automatically. Game rendering code does not need preprocessor blocks.

Open **Window > Analysis > Shader & Render Debugger**. When the window is closed, the session releases all debug textures and no stage is requested.

## Recommended profiler-style API

Most effects should use `RenderDebugSourceMarker`. It automatically obtains the current context, registers the source and stages, survives session recreation, checks requests, and routes each resource to the correct copy path.

```csharp
using SAS.RenderDebugging;

private readonly RenderDebugSourceMarker renderDebug = new(
    "my-effect",
    "My Effect");
```

Publish a RenderGraph texture in one line. Registration and the request check are automatic:

```csharp
renderDebug.Publish(renderGraph, "Raw Mask", mask, maskDescriptor, camera);
```

Named stages are created lazily and ordered by their first publication. Call `renderDebug.Dispose()` from the renderer feature's existing `Dispose` method. Ordinary publications do not need a manual request check.

Use an explicit `RenderDebugStage` only when advanced metadata such as a stable custom order, description, group, or channel labels is useful. The same `Publish(...)` overloads accept either a stage name or a descriptor.

The lower-level `IRenderDebugSource` and `IRenderDebugContext` contracts remain available for frameworks that need custom registration or publication control.

## RenderGraph publication and ownership

Never store a `TextureHandle`. Publish it while recording the graph and supply the descriptor:

```csharp
renderDebug.Publish(
    renderGraph,
    "Raw Mask",
    transientMask,
    maskDescriptor,
    cameraData.camera);
```

The context first checks `IsStageRequested`. It then imports a reusable debugger-owned `RTHandle` and records a non-culled copy pass from the transient handle. Only the external copy is placed in frame data. Live and captured resources use different ownership slots. Resizing reallocates that slot; returning to live releases captured slots; closing the viewer, source removal, play-mode changes, domain reload, and Unity shutdown release all slots.

For non-RenderGraph rendering, the marker's command-buffer/RTHandle `Publish(...)` overload records the equivalent owned copy. Its Texture `Publish(...)` overload is intended only for caller-owned persistent textures; capture mode still makes an owned frozen copy.

## Lifecycle and duplicates

- Sources explicitly register and unregister. Destroyed `UnityEngine.Object` sources are also pruned.
- Named stages register independently from pixel capture and follow first-publication order. Explicit descriptors are sorted by `Order`, then stable ID.
- Re-registering the same owner/descriptor is idempotent.
- A different owner with the same source ID, or different metadata with the same stage ID, is warned once and ignored.
- Missing or non-executed stages show **No data this frame** and never affect rendering.
- Camera instance ID/name and source frame are stored with every publication.

## Mask outline example

With `RENDER_DEBUG`, Chapter 7 registers these stages without changing its production algorithm:

```text
Raw Character Mask
  -> Horizontal Morphology (R solid expansion, G weighted feather, B erosion)
  -> Vertical Morphology (R final solid expansion, G final weighted feather, B final erosion)
  -> Final Result
```

All four publications reference textures already produced by the effect. Mask Outline contains no custom debug shader or debug-only rendering pass.

## Viewer features

- Ordered source/stage sequence with thumbnails
- RGB, R, G, B, and A grayscale views
- Exposure from -8 to +8 stops
- Fit and 1:1 preview modes
- Explicit one-frame capture and return to live
- A/B side-by-side and absolute difference
- One-pixel asynchronous GPU readback with pixel, UV, and floating-point RGBA
- Effect-provided group and channel labels

## Known limitations

- V1 previews 2D color textures. Texture arrays, cube maps, XR slices, and raw depth attachments require an explicit color visualization stage.
- The viewer shows the latest camera publication and records camera identity, but does not yet provide a camera selector.
- Capture freezes stages that execute after the request. A conditional stage that does not execute is reported as missing rather than retaining a transient handle.
- Pixel probing reports Stage A in side-by-side mode and may be unsupported by a platform/format that cannot convert a one-pixel readback to RGBA float.
- Source IDs are project-global. Multiple instances of the outline example need distinct IDs before both can be viewed simultaneously.

## Recommended next steps

Add camera selection, XR slice selection, explicit depth/normal visualizers, min/max range remapping, capture export/import for future remote transport, and small render tests for platform-specific copy formats. These extend the generic contracts; they should not add effect-specific branches to the debugger core.
