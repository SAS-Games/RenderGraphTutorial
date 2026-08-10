# Texture Exposure Mode Examples

The first two examples demonstrate the supported texture exposure modes. Example
03 covers custom shader-pass selection and temporary global keyword state, while
the processing example demonstrates a reusable one-pass stage between a producer
and consumer. Each example has its own renderer asset, ready-to-run scene, and
setup notes.

## At a glance

| Example | Producer `Texture Exposure` | Consumer technique | Use it when |
| --- | --- | --- | --- |
| `01_FrameRegistryOnly` | `Frame Registry Only` | Resolve a `TextureHandle` and texel size from `FrameTextureRegistry` | Another C# Render Graph pass consumes the texture |
| `02_GlobalTexture` | `Frame Registry + Global Texture` | Declare `UseGlobalTexture` and sample the named shader property | A shader needs the generated texture |
| `03_GlobalKeywordAndShaderTags` | `Frame Registry Only` | Select a custom `LightMode` pass and temporarily enable its global keyword variant | An object's own shader provides a specialized capture pass |
| `04_FrameTextureProcessing` | Producer uses `Frame Registry Only`; processor publishes its result | Capture through the registry, threshold, then sample the global result | A generated texture needs one reusable processing operation before one or more consumers |
| `05_DepthSettings` | `Frame Registry Only` | Compare visible and occluded masks, then demonstrate an intentional camera-depth write | A capture must respect, classify, or deliberately update camera depth |

Both producer modes register the texture in `FrameTextureRegistry`. Global Texture
additionally publishes it for shaders, and Unity automatically supplies the
conventional `<TextureName>_TexelSize` value with that binding.

## Common setup

The included `Example.unity` scenes are already configured. Open one and enter
Play mode to see its capsule visualization. Each scene camera selects its dedicated
renderer from the active `PC_RPAsset`:

```text
01 -> FrameRegistryOnlyExampleRenderer
02 -> GlobalTextureExampleRenderer
03 -> GlobalKeywordAndShaderTagsExampleRenderer
04 -> FrameTextureProcessingExampleRenderer
05 -> DepthSettingsExampleRenderer
```

To reproduce the setup manually:

1. Add `ObjectsToRenderTextureFeature` to the URP Renderer Data asset.
2. Add one output for the example you want to run.
3. Copy the exact texture name from that example's README.
4. Select the documented `Texture Exposure` mode.
5. Choose the test object's layer in `Layer Mask` and, for the clearest result,
   use a material whose red channel is white or assign a flat white override material.
6. Use `After Rendering Opaques` for ordinary masks that test populated camera
   depth. A pass that intentionally writes depth for later opaque rendering must
   run earlier, as demonstrated by example 05.
7. Add the matching example feature below the producer. For example 04, add
   `FrameTextureProcessingFeature` between the producer and example consumer.
   The processing stage runs after opaques and the consumer runs after
   transparents.
8. Disable the producer's `Debug View`; the example draws its own overlay.

Use one example at a time for the clearest result. The runtime-created materials
load shaders from the sample's `Resources` folder, which also keeps the shaders in
player builds.
