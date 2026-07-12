using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

internal sealed class MaskOutlineMaterialSet : IDisposable
{
    public MaskOutlineMaterialSet(Material source)
    {
        Horizontal = new MaskedEffectMaterialInstance(source);
        Vertical = new MaskedEffectMaterialInstance(source);
        Composite = new MaskedEffectMaterialInstance(source);
    }

    public MaskedEffectMaterialInstance Horizontal { get; private set; }
    public MaskedEffectMaterialInstance Vertical { get; private set; }
    public MaskedEffectMaterialInstance Composite { get; private set; }

    public bool IsValid =>
        Horizontal != null && Horizontal.IsValid &&
        Vertical != null && Vertical.IsValid &&
        Composite != null && Composite.IsValid;

    public void Dispose()
    {
        Horizontal?.Dispose();
        Vertical?.Dispose();
        Composite?.Dispose();
        Horizontal = null;
        Vertical = null;
        Composite = null;
    }
}

internal sealed class MaskOutlinePass : ScriptableRenderPass
{
    private const int HorizontalMorphologyPass = 0;
    private const int VerticalMorphologyPass = 1;
    private const int CompositePass = 2;

    private static readonly int MorphologyTextureId = Shader.PropertyToID("_MorphologyTexture");
    private static readonly int MaskTexelSizeId = Shader.PropertyToID("_MaskTexelSize");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineIntensityId = Shader.PropertyToID("_OutlineIntensity");
    private static readonly int OutlineModeId = Shader.PropertyToID("_OutlineMode");

    private string _profilingName;
    private MaskOutlineFeature.Settings _settings;
    private MaskOutlineMaterialSet _materials;
    private readonly MaskedTextureResolver _maskResolver = new(nameof(MaskOutlineFeature));

    private sealed class MorphologyPassData
    {
        public TextureHandle Source;
        public Material Material;
        public Vector4 MaskTexelSize;
        public float OutlineWidth;
        public float MaskThreshold;
        public int MaterialPass;
    }

    private sealed class CompositePassData
    {
        public TextureHandle MaskTexture;
        public TextureHandle MorphologyTexture;
        public Material Material;
        public Color OutlineColor;
        public float OutlineIntensity;
        public float MaskThreshold;
        public float OutlineMode;
    }

    public void Setup(
        string profilingName,
        MaskOutlineFeature.Settings settings,
        MaskOutlineMaterialSet materials)
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
        RenderTextureDescriptor descriptor = CreateMorphologyDescriptor(
            cameraData.cameraTargetDescriptor,
            maskTexelSize);
        TextureHandle horizontal = CreateMorphologyTexture(renderGraph, descriptor, "Horizontal");
        TextureHandle morphology = CreateMorphologyTexture(renderGraph, descriptor, "Result");

        AddMorphologyPass(
            renderGraph,
            $"{_profilingName} Horizontal",
            maskTexture,
            horizontal,
            _materials.Horizontal.Material,
            maskTexelSize,
            HorizontalMorphologyPass);

        AddMorphologyPass(
            renderGraph,
            $"{_profilingName} Vertical",
            horizontal,
            morphology,
            _materials.Vertical.Material,
            maskTexelSize,
            VerticalMorphologyPass);

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        AddCompositePass(
            renderGraph,
            resourceData.activeColorTexture,
            maskTexture,
            morphology);
    }

    private void AddMorphologyPass(
        RenderGraph renderGraph,
        string passName,
        TextureHandle source,
        TextureHandle destination,
        Material material,
        Vector4 maskTexelSize,
        int materialPass)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            passName,
            out MorphologyPassData passData,
            profilingSampler);

        passData.Source = source;
        passData.Material = material;
        passData.MaskTexelSize = maskTexelSize;
        passData.OutlineWidth = Mathf.Clamp(_settings.OutlineWidth, 1.0f, 16.0f);
        passData.MaskThreshold = Mathf.Clamp01(_settings.MaskThreshold);
        passData.MaterialPass = materialPass;

        builder.UseTexture(source, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0);
        builder.SetRenderFunc((MorphologyPassData data, RasterGraphContext context) => ExecuteMorphologyPass(data, context));
    }

    private void AddCompositePass(
        RenderGraph renderGraph,
        TextureHandle activeColor,
        TextureHandle maskTexture,
        TextureHandle morphologyTexture)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            $"{_profilingName} Composite",
            out CompositePassData passData,
            profilingSampler);

        passData.MaskTexture = maskTexture;
        passData.MorphologyTexture = morphologyTexture;
        passData.Material = _materials.Composite.Material;
        passData.OutlineColor = _settings.OutlineColor;
        passData.OutlineIntensity = Mathf.Max(0.0f, _settings.OutlineIntensity);
        passData.MaskThreshold = Mathf.Clamp01(_settings.MaskThreshold);
        passData.OutlineMode = (float)_settings.Mode;

        builder.UseTexture(maskTexture, AccessFlags.Read);
        builder.UseTexture(morphologyTexture, AccessFlags.Read);
        builder.SetRenderAttachment(activeColor, 0, AccessFlags.ReadWrite);
        builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) => ExecuteCompositePass(data, context));
    }

    private TextureHandle CreateMorphologyTexture(
        RenderGraph renderGraph,
        RenderTextureDescriptor descriptor,
        string suffix)
    {
        return UniversalRenderer.CreateRenderGraphTexture(
            renderGraph,
            descriptor,
            $"{_profilingName} {suffix}",
            false,
            FilterMode.Point,
            TextureWrapMode.Clamp);
    }

    private static RenderTextureDescriptor CreateMorphologyDescriptor(
        RenderTextureDescriptor descriptor,
        Vector4 maskTexelSize)
    {
        descriptor.width = Mathf.Max(1, Mathf.RoundToInt(maskTexelSize.z));
        descriptor.height = Mathf.Max(1, Mathf.RoundToInt(maskTexelSize.w));
        descriptor.graphicsFormat = GraphicsFormat.R8G8_UNorm;
        descriptor.depthBufferBits = 0;
        descriptor.msaaSamples = 1;
        descriptor.useDynamicScale = false;
        descriptor.useDynamicScaleExplicit = false;
        return descriptor;
    }

    private static void ExecuteMorphologyPass(MorphologyPassData data, RasterGraphContext context)
    {
        data.Material.SetVector(MaskTexelSizeId, data.MaskTexelSize);
        data.Material.SetFloat(OutlineWidthId, data.OutlineWidth);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);

        Blitter.BlitTexture(
            context.cmd,
            data.Source,
            new Vector4(1, 1, 0, 0),
            data.Material,
            data.MaterialPass);
    }

    private static void ExecuteCompositePass(CompositePassData data, RasterGraphContext context)
    {
        data.Material.SetTexture(MorphologyTextureId, data.MorphologyTexture);
        data.Material.SetColor(OutlineColorId, data.OutlineColor);
        data.Material.SetFloat(OutlineIntensityId, data.OutlineIntensity);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(OutlineModeId, data.OutlineMode);

        Blitter.BlitTexture(
            context.cmd,
            data.MaskTexture,
            new Vector4(1, 1, 0, 0),
            data.Material,
            CompositePass);
    }
}
