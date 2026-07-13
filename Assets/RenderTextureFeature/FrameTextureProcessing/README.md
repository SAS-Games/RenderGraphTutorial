# Frame Texture Processing

`FrameTextureProcessingFeature` performs optional one-pass material operations on textures published through `FrameTextureRegistry`.

It is a consumer and producer:

```text
ObjectsToRenderTextureFeature or another producer
  -> registered input texture
  -> one fullscreen material pass
  -> registered and globally published output texture
  -> outline, halo, JFA, blur, or another consumer
```

The utility is intended for thresholding, inversion, channel remapping, color conversion, simple distortion, and other operations that need one shader pass. Blur, Jump Flood, morphology chains, and other multi-pass algorithms should keep dedicated features.

## Setup

1. Add `ObjectsToRenderTextureFeature` or another producer to the URP renderer.
2. Configure the producer's texture name, such as `_SelectionOutlineMask`.
3. Add `FrameTextureProcessingFeature` after that producer in the renderer-feature list.
4. Add an entry to `Processing Settings`.
5. Set `Input Texture Name` to the producer's exact registered name.
6. Set `Output Texture Name` to a new name, such as `_ThresholdedOutlineMask`.
7. Assign a fullscreen processing material.
8. Set `Material Pass Index` to the one shader pass to execute, normally `0`.
9. Configure the later effect to read `Output Texture Name`.
10. Ensure the processing entry's `Render Pass Event` is after the producer and no later than its consumer.

When features share the same `Render Pass Event`, renderer-feature order controls their order. The producer must appear first, followed by this utility, followed by the consumer.

## Settings

- `Enabled`: skips the entry without deleting its configuration. A skipped entry enqueues no render pass.
- `Name`: label used by the inspector, Render Graph Viewer, and profiler.
- `Render Pass Event`: point in the URP frame where the material operation runs.
- `Input Texture Name`: exact registry key created by an earlier pass.
- `Output Texture Name`: registry key and global shader property receiving the result.
- `Processing Material`: fullscreen material used for the operation.
- `Material Pass Index`: one explicit shader pass. This utility never interprets `-1` as all passes.
- `Output Scale`: output resolution relative to the input resolution.
- `Output Filter Mode`: filter mode used when later passes sample the result.
- `Output Wrap Mode`: wrap mode used when later passes sample the result.

The utility preserves the input color format and texture dimension. It removes depth, MSAA, mip maps, dynamic scaling, and random-write flags from the output because the result is a sampled color texture. Depth textures are rejected.

## Replacing An Existing Name

`Output Texture Name` may equal `Input Texture Name`. The pass resolves the old handle first, writes into a separate destination, and then replaces the registry entry for later consumers:

```text
_CharacterMask -> threshold -> _CharacterMask
```

The source and destination are still different Render Graph resources, so this is not an in-place GPU read/write operation.

Use a new output name when another pass also needs the original texture.

## Chaining

Entries using the same event run in list order and can form a chain:

```text
_RawMask -> threshold -> _HardMask -> invert -> _InverseMask
```

Each enabled entry costs one fullscreen pass and one temporary output texture. Do not split operations that one consumer shader can perform more cheaply in its existing pass.

## Material Contract

Processing shaders must use URP's fullscreen blit contract. Include `Blit.hlsl` and sample `_BlitTexture`:

```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

half4 Frag(Varyings input) : SV_Target
{
    half4 source = SAMPLE_TEXTURE2D_X(
        _BlitTexture,
        sampler_LinearClamp,
        input.texcoord);
    return source;
}
```

Use `Vert` from `Blit.hlsl`, and configure the pass with `ZTest Always`, `ZWrite Off`, and `Cull Off`.

`_BlitTexture_TexelSize` is set to `(1 / width, 1 / height, width, height)` for shaders that need pixel-sized offsets.

The included `MaskThreshold` material demonstrates the contract. It exposes:

- `Threshold`: coverage cutoff.
- `Softness`: width of the transition around the cutoff.
- `Invert`: swaps black and white output.

## Output Access

Later C# passes should resolve the result with `FrameTextureResolver` or `FrameTextureRegistry`. Shaders can sample the global property named by `Output Texture Name`. The utility also publishes:

```text
<Output Texture Name>_TexelSize
```

with the standard value `(1 / width, 1 / height, width, height)`.

## Performance Guidance

Use this utility when the processed texture is shared, when processing must happen before several consumers, or when keeping the operation independent improves reuse.

Keep a simple operation inside an existing consumer shader when only that consumer needs it. That avoids the additional fullscreen pass and texture bandwidth.

Lowering `Output Scale` reduces pixel cost and memory traffic. Use `1` for hard masks or pixel-accurate data, and consider `0.5` for soft effects that tolerate lower resolution.
