using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

internal sealed class TextureExposureProofPass : ScriptableRenderPass
{
    public const string RequiredTextureName = "_TextureExposureProofMask";

    private static readonly int RequiredTextureId = Shader.PropertyToID(RequiredTextureName);
    private static readonly int ProofColorId = Shader.PropertyToID("_ProofColor");

    private readonly string _ownerName;
    private Material _material;
    private Color _proofColor;
    private string _lastFailure;

    private sealed class PassData
    {
        public Material Material;
        public Color ProofColor;
    }

    public TextureExposureProofPass(
        string ownerName,
        string profilingName)
    {
        _ownerName = ownerName;
        profilingSampler = new ProfilingSampler(profilingName);
    }

    public void Setup(
        RenderPassEvent injectionPoint,
        Material material,
        Color proofColor)
    {
        renderPassEvent = injectionPoint;
        _material = material;
        _proofColor = proofColor;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (!TryValidate(frameData))
        {
            return;
        }

        _lastFailure = null;
        var resourceData = frameData.Get<UniversalResourceData>();

        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            _ownerName,
            out PassData passData,
            profilingSampler);

        passData.Material = _material;
        passData.ProofColor = _proofColor;

        // This is intentionally the only texture dependency. There is no registry
        // TextureHandle fallback, so the proof genuinely consumes the shader global.
        builder.UseGlobalTexture(RequiredTextureId, AccessFlags.Read);
        builder.SetRenderAttachment(
            resourceData.activeColorTexture,
            0,
            AccessFlags.ReadWrite);
        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            ExecutePass(data, context));
    }

    private bool TryValidate(ContextContainer frameData)
    {
        if (_material == null)
        {
            LogFailure(
                "has no Proof Material. Assign the material included beside the feature script.");
            return false;
        }

        if (_material.passCount == 0)
        {
            LogFailure("uses a material with no shader passes.");
            return false;
        }

        if (!FrameTextureRegistry.TryGet(frameData, out FrameTextureRegistry registry) ||
            !registry.TryGetTexture(RequiredTextureId, out _, out _))
        {
            LogFailure(
                $"did not find '{RequiredTextureName}'. Add an enabled " +
                $"{nameof(ObjectsToRenderTextureFeature)} output with that exact Texture Name, " +
                "place it before this feature, and use an earlier Render Pass Event.");
            return false;
        }

        return true;
    }

    private void LogFailure(string reason)
    {
        string message = $"{_ownerName} {reason}";
        if (_lastFailure == message)
        {
            return;
        }

        _lastFailure = message;
        Debug.LogError(message);
    }

    private static void ExecutePass(PassData data, RasterGraphContext context)
    {
        data.Material.SetColor(ProofColorId, data.ProofColor);
        Blitter.BlitTexture(
            context.cmd,
            new Vector4(1, 1, 0, 0),
            data.Material,
            0);
    }
}

internal sealed class TextureExposureProofTexelSizeResetPass : ScriptableRenderPass
{
    private static readonly int TexelSizeId = Shader.PropertyToID(
        $"{TextureExposureProofPass.RequiredTextureName}_TexelSize");

    private sealed class PassData
    {
        public int TexelSizePropertyId;
    }

    public TextureExposureProofTexelSizeResetPass()
    {
        renderPassEvent = RenderPassEvent.BeforeRendering;
        profilingSampler = new ProfilingSampler("Global Texel Size Proof Reset");
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            "Global Texel Size Proof Reset",
            out PassData passData,
            profilingSampler);

        passData.TexelSizePropertyId = TexelSizeId;
        builder.AllowGlobalStateModification(true);
        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            context.cmd.SetGlobalVector(data.TexelSizePropertyId, Vector4.zero));
    }
}
