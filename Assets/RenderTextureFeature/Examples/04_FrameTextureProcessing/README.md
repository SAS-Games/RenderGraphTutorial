# Frame Texture Processing Example

This scene demonstrates a complete three-stage Render Graph workflow:

```text
ObjectsToRenderTextureFeature
  captures the capsule as a vertical grayscale gradient
  -> _FrameProcessingRawMask (Frame Registry Only)

FrameTextureProcessingFeature
  applies a 0.55 threshold in one fullscreen pass
  -> _FrameProcessingResult (registry + global texture)

FrameTextureProcessingExampleFeature
  declares and samples the globally published _FrameProcessingResult
  -> draws the surviving region as a purple overlay
```

Open `Example.unity` and enter Play mode. The camera already uses
`FrameTextureProcessingExampleRenderer`, so no setup is required. The capsule's
upper portion is purple because only gradient values at or above the threshold
survive.

## What each asset does

- `WorldHeightGradientMask.shader` and its material are the capture override.
  They convert the capsule's world-space height into grayscale mask data.
- `ThresholdMask.mat` uses the reusable `MaskThreshold` processing shader. Its
  threshold is `0.55`, softness is `0`, and inversion is disabled.
- `FrameTextureProcessingFeature` is the reusable processing stage. It knows
  only the input name, output name, and material operation.
- `FrameTextureProcessingExampleFeature` is a deliberately small downstream
  shader-global consumer. Its Render Graph pass declares `UseGlobalTexture`, so
  the graph knows it depends on the processing result.

## Why and when to use this pattern

Use `FrameTextureProcessingFeature` when a generated texture needs one reusable
fullscreen operation before another feature consumes it, for example thresholding
a soft selection mask, inverting visibility, remapping channels, or converting
data into a shared format.

It is especially useful when several later effects need the same processed
result. Processing once avoids repeating the operation in each consumer.

Do not add this stage when only one existing consumer needs a trivial operation;
putting that math in the consumer shader avoids one fullscreen pass and one
temporary texture.

## Things to try

- Change `Threshold` on `ThresholdMask.mat` to move the cutoff up or down.
- Enable `Invert` to retain the lower portion instead.
- Add softness to make the transition gradual.
- Set `Output Scale` to `0.5` to demonstrate a lower-cost, half-resolution result.
- Change `Output Texture Name`, then update
  `FrameTextureProcessingExampleFeature.RequiredTextureName` to the same name.

Unity automatically exposes `_BlitTexture_TexelSize` while the processing shader
reads its input, and `_FrameProcessingResult_TexelSize` after the output is
globally published. No manual texel-size vector is needed.
