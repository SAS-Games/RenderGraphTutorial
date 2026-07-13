using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class GlobalTextureAndTexelSizeProofFeature : ScriptableRendererFeature
{
    [Tooltip("Material using Hidden/RenderTextureFeature/TextureExposureProof/GlobalTextureAndTexelSize. Assign the included GlobalTextureAndTexelSizeProof material.")]
    public Material ProofMaterial;

    [Tooltip("Color used for the one-texel edge derived from the global texture and its global texel-size vector.")]
    public Color ProofColor = new(1.0f, 0.85f, 0.0f, 1.0f);

    [Tooltip("When the proof edge runs. This must be later than the mask producer's Render Pass Event.")]
    public RenderPassEvent InjectionPoint = RenderPassEvent.AfterRenderingTransparents;

    private TextureExposureProofTexelSizeResetPass _resetPass;
    private TextureExposureProofPass _pass;

    public override void Create()
    {
        _resetPass = new TextureExposureProofTexelSizeResetPass();
        _pass = new TextureExposureProofPass(
            nameof(GlobalTextureAndTexelSizeProofFeature),
            "Global Texture And Texel Size Proof");
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        _resetPass ??= new TextureExposureProofTexelSizeResetPass();
        _pass ??= new TextureExposureProofPass(
            nameof(GlobalTextureAndTexelSizeProofFeature),
            "Global Texture And Texel Size Proof");
        renderer.EnqueuePass(_resetPass);
        _pass.Setup(InjectionPoint, ProofMaterial, ProofColor);
        renderer.EnqueuePass(_pass);
    }
}
