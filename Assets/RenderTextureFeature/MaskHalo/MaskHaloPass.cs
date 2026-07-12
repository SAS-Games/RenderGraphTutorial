using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

internal sealed class MaskHaloMaterialSet : IDisposable
{
    private readonly List<MaskedEffectMaterialInstance> _blurMaterials = new();

    public MaskHaloMaterialSet(Material source)
    {
        GlowComposite = new MaskedEffectMaterialInstance(source);
        RimComposite = new MaskedEffectMaterialInstance(source);
    }

    public MaskedEffectMaterialInstance GlowComposite { get; private set; }
    public MaskedEffectMaterialInstance RimComposite { get; private set; }
    public bool IsValid =>
        GlowComposite != null && GlowComposite.IsValid &&
        RimComposite != null && RimComposite.IsValid;

    public Material GetBlurMaterial(int index)
    {
        return _blurMaterials[index].Material;
    }

    public void EnsureBlurMaterialCount(int count, Material source)
    {
        while (_blurMaterials.Count < count)
        {
            _blurMaterials.Add(new MaskedEffectMaterialInstance(source));
        }

        while (_blurMaterials.Count > count)
        {
            int lastIndex = _blurMaterials.Count - 1;
            _blurMaterials[lastIndex].Dispose();
            _blurMaterials.RemoveAt(lastIndex);
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _blurMaterials.Count; i++)
        {
            _blurMaterials[i].Dispose();
        }

        _blurMaterials.Clear();
        GlowComposite?.Dispose();
        RimComposite?.Dispose();
        GlowComposite = null;
        RimComposite = null;
    }
}

internal sealed class MaskHaloPass : ScriptableRenderPass
{
    private const int BlurShaderPass = 0;
    private const int GlowCompositeShaderPass = 1;
    private const int RimCompositeShaderPass = 2;

    private static readonly int BlurTexelSizeId = Shader.PropertyToID("_BlurTexelSize");
    private static readonly int BlurOffsetId = Shader.PropertyToID("_BlurOffset");
    private static readonly int HaloBlurTextureId = Shader.PropertyToID("_HaloBlurTexture");
    private static readonly int MaskTexelSizeId = Shader.PropertyToID("_MaskTexelSize");
    private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");
    private static readonly int MaskSoftnessId = Shader.PropertyToID("_MaskSoftness");
    private static readonly int OuterGlowColorId = Shader.PropertyToID("_OuterGlowColor");
    private static readonly int OuterGlowIntensityId = Shader.PropertyToID("_OuterGlowIntensity");
    private static readonly int OuterGlowFalloffId = Shader.PropertyToID("_OuterGlowFalloff");
    private static readonly int InnerGlowColorId = Shader.PropertyToID("_InnerGlowColor");
    private static readonly int InnerGlowIntensityId = Shader.PropertyToID("_InnerGlowIntensity");
    private static readonly int InnerGlowTightnessId = Shader.PropertyToID("_InnerGlowTightness");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
    private static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    private string _profilingName;
    private MaskHaloFeature.Settings _settings;
    private MaskHaloMaterialSet _materials;
    private readonly MaskedTextureResolver _maskResolver = new(nameof(MaskHaloFeature));

    private sealed class BlurPassData
    {
        public TextureHandle Source;
        public Material Material;
        public Vector4 TexelSize;
        public float Offset;
    }

    private sealed class GlowCompositePassData
    {
        public TextureHandle MaskTexture;
        public TextureHandle BlurTexture;
        public Material Material;
        public Vector4 MaskTexelSize;
        public float MaskThreshold;
        public float MaskSoftness;
        public Color OuterGlowColor;
        public float OuterGlowIntensity;
        public float OuterGlowFalloff;
        public Color InnerGlowColor;
        public float InnerGlowIntensity;
        public float InnerGlowTightness;
        public float Opacity;
    }

    private sealed class RimCompositePassData
    {
        public TextureHandle MaskTexture;
        public Material Material;
        public Vector4 MaskTexelSize;
        public float MaskThreshold;
        public float MaskSoftness;
        public Color RimColor;
        public float RimWidth;
        public float RimIntensity;
        public float Opacity;
    }

    public void Setup(
        string profilingName,
        MaskHaloFeature.Settings settings,
        MaskHaloMaterialSet materials)
    {
        _settings = settings;
        _materials = materials;
        _maskResolver.SetTextureName(settings.MaskTextureName);
        renderPassEvent = settings.RenderPassEvent;
        profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(
            profilingName,
            ref _profilingName,
            profilingSampler);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_settings == null || _materials == null || !_materials.IsValid)
        {
            return;
        }

        if (!_maskResolver.TryResolve(
                frameData,
                out TextureHandle maskTexture,
                out Vector4 maskTexelSize))
        {
            return;
        }

        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        RenderTextureDescriptor blurDescriptor = CreateBlurDescriptor(cameraData.cameraTargetDescriptor);
        Vector4 blurTexelSize = MaskedEffectRenderGraphUtility.CreateTexelSize(
            blurDescriptor.width,
            blurDescriptor.height);

        TextureHandle ping = CreateBlurTexture(renderGraph, blurDescriptor, "Ping");
        TextureHandle pong = CreateBlurTexture(renderGraph, blurDescriptor, "Pong");
        TextureHandle currentSource = maskTexture;
        int iterations = Mathf.Clamp(_settings.BlurIterations, 1, 6);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            TextureHandle destination = iteration % 2 == 0 ? ping : pong;
            float offset = Mathf.Clamp(_settings.BlurRadius, 0.5f, 6.0f) *
                           (1.0f + iteration * 0.5f);

            AddBlurPass(
                renderGraph,
                $"{_profilingName} Blur {iteration + 1}",
                currentSource,
                destination,
                _materials.GetBlurMaterial(iteration),
                blurTexelSize,
                offset);

            currentSource = destination;
        }

        AddGlowCompositePass(
            renderGraph,
            resourceData.activeColorTexture,
            maskTexture,
            currentSource,
            maskTexelSize);

        AddRimCompositePass(
            renderGraph,
            resourceData.activeColorTexture,
            maskTexture,
            maskTexelSize);
    }

    private TextureHandle CreateBlurTexture(
        RenderGraph renderGraph,
        RenderTextureDescriptor descriptor,
        string suffix)
    {
        return UniversalRenderer.CreateRenderGraphTexture(
            renderGraph,
            descriptor,
            $"{_profilingName} {suffix}",
            false,
            FilterMode.Bilinear,
            TextureWrapMode.Clamp);
    }

    private void AddBlurPass(
        RenderGraph renderGraph,
        string passName,
        TextureHandle source,
        TextureHandle destination,
        Material material,
        Vector4 texelSize,
        float offset)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            passName,
            out BlurPassData passData,
            profilingSampler);

        passData.Source = source;
        passData.Material = material;
        passData.TexelSize = texelSize;
        passData.Offset = offset;

        builder.UseTexture(source, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0);
        builder.SetRenderFunc((BlurPassData data, RasterGraphContext context) => ExecuteBlurPass(data, context));
    }

    private void AddGlowCompositePass(
        RenderGraph renderGraph,
        TextureHandle activeColor,
        TextureHandle maskTexture,
        TextureHandle blurTexture,
        Vector4 maskTexelSize)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            $"{_profilingName} Glow Composite",
            out GlowCompositePassData passData,
            profilingSampler);

        passData.MaskTexture = maskTexture;
        passData.BlurTexture = blurTexture;
        passData.Material = _materials.GlowComposite.Material;
        passData.MaskTexelSize = maskTexelSize;
        passData.MaskThreshold = Mathf.Clamp01(_settings.MaskThreshold);
        passData.MaskSoftness = Mathf.Clamp(_settings.MaskSoftness, 0.0f, 0.5f);
        passData.OuterGlowColor = _settings.OuterGlowColor;
        passData.OuterGlowIntensity = Mathf.Max(0.0f, _settings.OuterGlowIntensity);
        passData.OuterGlowFalloff = Mathf.Clamp(_settings.OuterGlowFalloff, 0.25f, 2.0f);
        passData.InnerGlowColor = _settings.InnerGlowColor;
        passData.InnerGlowIntensity = Mathf.Max(0.0f, _settings.InnerGlowIntensity);
        passData.InnerGlowTightness = Mathf.Clamp(_settings.InnerGlowTightness, 1.0f, 6.0f);
        passData.Opacity = Mathf.Clamp01(_settings.Opacity);

        builder.UseTexture(maskTexture, AccessFlags.Read);
        builder.UseTexture(blurTexture, AccessFlags.Read);
        builder.SetRenderAttachment(activeColor, 0, AccessFlags.ReadWrite);
        builder.SetRenderFunc((GlowCompositePassData data, RasterGraphContext context) => ExecuteGlowCompositePass(data, context));
    }

    private void AddRimCompositePass(
        RenderGraph renderGraph,
        TextureHandle activeColor,
        TextureHandle maskTexture,
        Vector4 maskTexelSize)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            $"{_profilingName} Rim Composite",
            out RimCompositePassData passData,
            profilingSampler);

        passData.MaskTexture = maskTexture;
        passData.Material = _materials.RimComposite.Material;
        passData.MaskTexelSize = maskTexelSize;
        passData.MaskThreshold = Mathf.Clamp01(_settings.MaskThreshold);
        passData.MaskSoftness = Mathf.Clamp(_settings.MaskSoftness, 0.0f, 0.5f);
        passData.RimColor = _settings.RimColor;
        passData.RimWidth = Mathf.Clamp(_settings.RimWidth, 0.5f, 8.0f);
        passData.RimIntensity = Mathf.Max(0.0f, _settings.RimIntensity);
        passData.Opacity = Mathf.Clamp01(_settings.Opacity);

        builder.UseTexture(maskTexture, AccessFlags.Read);
        builder.SetRenderAttachment(activeColor, 0, AccessFlags.ReadWrite);
        builder.SetRenderFunc((RimCompositePassData data, RasterGraphContext context) => ExecuteRimCompositePass(data, context));
    }

    private RenderTextureDescriptor CreateBlurDescriptor(RenderTextureDescriptor descriptor)
    {
        int downsample = Mathf.Clamp(_settings.Downsample, 1, 4);
        descriptor.depthBufferBits = 0;
        descriptor.msaaSamples = 1;
        descriptor.width = Mathf.Max(1, descriptor.width / downsample);
        descriptor.height = Mathf.Max(1, descriptor.height / downsample);
        descriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
        descriptor.useDynamicScale = false;
        descriptor.useDynamicScaleExplicit = false;
        return descriptor;
    }

    private static void ExecuteBlurPass(BlurPassData data, RasterGraphContext context)
    {
        data.Material.SetVector(BlurTexelSizeId, data.TexelSize);
        data.Material.SetFloat(BlurOffsetId, data.Offset);

        Blitter.BlitTexture(
            context.cmd,
            data.Source,
            new Vector4(1, 1, 0, 0),
            data.Material,
            BlurShaderPass);
    }

    private static void ExecuteGlowCompositePass(GlowCompositePassData data, RasterGraphContext context)
    {
        data.Material.SetTexture(HaloBlurTextureId, data.BlurTexture);
        data.Material.SetVector(MaskTexelSizeId, data.MaskTexelSize);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(MaskSoftnessId, data.MaskSoftness);
        data.Material.SetColor(OuterGlowColorId, data.OuterGlowColor);
        data.Material.SetFloat(OuterGlowIntensityId, data.OuterGlowIntensity);
        data.Material.SetFloat(OuterGlowFalloffId, data.OuterGlowFalloff);
        data.Material.SetColor(InnerGlowColorId, data.InnerGlowColor);
        data.Material.SetFloat(InnerGlowIntensityId, data.InnerGlowIntensity);
        data.Material.SetFloat(InnerGlowTightnessId, data.InnerGlowTightness);
        data.Material.SetFloat(OpacityId, data.Opacity);

        Blitter.BlitTexture(
            context.cmd,
            data.MaskTexture,
            new Vector4(1, 1, 0, 0),
            data.Material,
            GlowCompositeShaderPass);
    }

    private static void ExecuteRimCompositePass(RimCompositePassData data, RasterGraphContext context)
    {
        data.Material.SetVector(MaskTexelSizeId, data.MaskTexelSize);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(MaskSoftnessId, data.MaskSoftness);
        data.Material.SetColor(RimColorId, data.RimColor);
        data.Material.SetFloat(RimWidthId, data.RimWidth);
        data.Material.SetFloat(RimIntensityId, data.RimIntensity);
        data.Material.SetFloat(OpacityId, data.Opacity);

        Blitter.BlitTexture(
            context.cmd,
            data.MaskTexture,
            new Vector4(1, 1, 0, 0),
            data.Material,
            RimCompositeShaderPass);
    }
}
