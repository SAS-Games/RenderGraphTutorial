using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class RenderTextureDebugPass : ScriptableRenderPass
{
    private static readonly int DebugColorId = Shader.PropertyToID("_DebugColor");

    private RenderTexturePass.Settings _settings;
    private Material _material;
    private int _sourceTexturePropertyId;

    private class PassData
    {
        public TextureHandle Source;
        public Material Material;
        public Color DebugColor;
        public int PassIndex;
    }

    public void Setup(string profilingName, RenderTexturePass.Settings settings, Material material)
    {
        _settings = settings;
        _material = material;
        _sourceTexturePropertyId = Shader.PropertyToID(settings.TextureName);
        renderPassEvent = settings.DebugRenderPassEvent;
        profilingSampler = new ProfilingSampler(profilingName);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_material == null || !frameData.Contains<RenderTexturePass.CustomTextureData>())
        {
            return;
        }

        RenderTexturePass.CustomTextureData textureData = frameData.Get<RenderTexturePass.CustomTextureData>();
        if (!textureData.TryGetTexture(_sourceTexturePropertyId, out TextureHandle source, out _))
        {
            return;
        }

        var resourceData = frameData.Get<UniversalResourceData>();
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            $"{_settings.TextureName} Debug View",
            out PassData passData,
            profilingSampler);

        passData.Source = source;
        passData.Material = _material;
        passData.DebugColor = _settings.DebugColor;
        passData.PassIndex = (int)_settings.DebugDisplayMode;

        builder.UseTexture(source, AccessFlags.Read);
        builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);

        builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
    }

    private static void ExecutePass(PassData data, RasterGraphContext context)
    {
        data.Material.SetColor(DebugColorId, data.DebugColor);
        Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1, 1, 0, 0), data.Material, data.PassIndex);
    }
}
