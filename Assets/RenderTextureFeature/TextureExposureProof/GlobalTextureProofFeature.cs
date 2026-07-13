using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class GlobalTextureProofFeature : ScriptableRendererFeature
{
    [Tooltip("Material using Hidden/RenderTextureFeature/TextureExposureProof/GlobalTexture. Assign the included GlobalTextureProof material.")]
    public Material ProofMaterial;

    [Tooltip("Color drawn over pixels covered by _TextureExposureProofMask.")]
    public Color ProofColor = new(0.0f, 1.0f, 0.2f, 0.65f);

    [Tooltip("When the proof overlay runs. This must be later than the mask producer's Render Pass Event.")]
    public RenderPassEvent InjectionPoint = RenderPassEvent.AfterRenderingTransparents;

    private TextureExposureProofPass _pass;

    public override void Create()
    {
        _pass = new TextureExposureProofPass(
            nameof(GlobalTextureProofFeature),
            "Global Texture Proof");
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        _pass ??= new TextureExposureProofPass(
            nameof(GlobalTextureProofFeature),
            "Global Texture Proof");
        _pass.Setup(InjectionPoint, ProofMaterial, ProofColor);
        renderer.EnqueuePass(_pass);
    }
}
