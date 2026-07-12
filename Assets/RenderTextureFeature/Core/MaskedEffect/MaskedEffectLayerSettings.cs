using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[Serializable]
public class MaskedEffectLayerSettings
{
    [Tooltip("Disables this layer without removing it from the list.")]
    public bool Enabled = true;

    [Tooltip("Human-readable name for this layer. Used for profiling and pass labels.")]
    public string Name = "Masked Layer";

    [Tooltip("When this layer runs. It must run after the mask texture has been produced.")]
    public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

    [Tooltip("Texture name produced by ObjectsToRenderTextureFeature.")]
    public string MaskTextureName = "_MaskTexture";

    [Tooltip("Mask value where the effect starts. Pure black remains unaffected.")]
    [Range(0.0f, 1.0f)]
    public float MaskThreshold = 0.5f;

    [Tooltip("Overall blend strength for this layer.")]
    [Range(0.0f, 1.0f)]
    public float Opacity = 1.0f;
}
