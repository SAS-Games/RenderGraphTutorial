using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

internal sealed class SelectiveMotionBlurMaterialSet : IDisposable
{
    public SelectiveMotionBlurMaterialSet(Material source)
    {
        TileMax = new MaskedEffectMaterialInstance(source);
        NeighborMax = new MaskedEffectMaterialInstance(source);
        Composite = new MaskedEffectMaterialInstance(source);
    }

    public MaskedEffectMaterialInstance TileMax { get; private set; }
    public MaskedEffectMaterialInstance NeighborMax { get; private set; }
    public MaskedEffectMaterialInstance Composite { get; private set; }
    public bool IsValid => TileMax != null && TileMax.IsValid && NeighborMax != null && NeighborMax.IsValid && Composite != null && Composite.IsValid;

    public void Dispose()
    {
        TileMax?.Dispose();
        NeighborMax?.Dispose();
        Composite?.Dispose();
        TileMax = null;
        NeighborMax = null;
        Composite = null;
    }
}

internal sealed class SelectiveMotionBlurPass : ScriptableRenderPass
{
    private const int TileMaxPassIndex = 0;
    private const int NeighborMaxPassIndex = 1;
    private const int CompositePassIndex = 2;

    private static readonly int MotionTextureId = Shader.PropertyToID("_SelectiveMotionTexture");
    private static readonly int SourceTextureId = Shader.PropertyToID("_SelectiveMotionSourceTexture");
    private static readonly int VelocityTextureId = Shader.PropertyToID("_SelectiveMotionVelocityTexture");
    private static readonly int SourceTexelSizeId = Shader.PropertyToID("_SourceTexelSize");
    private static readonly int TileTexelSizeId = Shader.PropertyToID("_TileTexelSize");
    private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");
    private static readonly int ExposureScaleId = Shader.PropertyToID("_ExposureScale");
    private static readonly int MaxBlurPixelsId = Shader.PropertyToID("_MaxBlurPixels");
    private static readonly int SampleCountId = Shader.PropertyToID("_SampleCount");
    private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");
    private static readonly int MaskSoftnessId = Shader.PropertyToID("_MaskSoftness");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private readonly MaskedTextureResolver maskResolver = new(nameof(SelectiveMotionBlurFeature));

    private string profilingName;
    private SelectiveMotionBlurFeature.Settings settings;
    private SelectiveMotionBlurMaterialSet materials;
    private int sourceTextureId;

    private sealed class TileMaxPassData
    {
        public TextureHandle Mask;
        public TextureHandle Motion;
        public Material Material;
        public Vector4 SourceTexelSize;
        public int TileSize;
        public float MaskThreshold;
        public float MaskSoftness;
    }

    private sealed class NeighborMaxPassData
    {
        public TextureHandle Velocity;
        public Material Material;
        public Vector4 TileTexelSize;
    }

    private sealed class CompositePassData
    {
        public TextureHandle Source;
        public TextureHandle Mask;
        public TextureHandle Velocity;
        public Material Material;
        public Vector4 SourceTexelSize;
        public float ExposureScale;
        public float MaxBlurPixels;
        public int SampleCount;
        public float MaskThreshold;
        public float MaskSoftness;
        public float Intensity;
    }

    public void Setup(string passName, SelectiveMotionBlurFeature.Settings passSettings, SelectiveMotionBlurMaterialSet passMaterials, int snapshotTextureId)
    {
        settings = passSettings;
        materials = passMaterials;
        sourceTextureId = snapshotTextureId;
        maskResolver.SetTextureName(settings.MaskTextureName);

        ConfigureInput(ScriptableRenderPassInput.Motion);
        renderPassEvent = settings.RenderPassEvent;
        profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(passName, ref profilingName, profilingSampler);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (settings == null || materials == null || !materials.IsValid)
            return;

        if (!maskResolver.TryResolve(frameData, out TextureHandle mask, out _))
            return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        TextureHandle source = ResolveSourceTexture(frameData, resourceData, cameraData, out Vector4 sourceTexelSize);
        TextureHandle motion = resourceData.motionVectorColor;

        if (!source.IsValid() || !mask.IsValid() || !motion.IsValid())
            return;

        int tileSize = Mathf.Clamp(settings.TileSize, 4, 16);
        RenderTextureDescriptor tileDescriptor = CreateTileDescriptor(cameraData.cameraTargetDescriptor, tileSize);
        Vector4 tileTexelSize = MaskedEffectRenderGraphUtility.CreateTexelSize(tileDescriptor.width, tileDescriptor.height);

        TextureHandle tileMax = UniversalRenderer.CreateRenderGraphTexture(renderGraph, tileDescriptor, $"{profilingName} Tile Max", false, FilterMode.Point, TextureWrapMode.Clamp);
        TextureHandle neighborMax = UniversalRenderer.CreateRenderGraphTexture(renderGraph, tileDescriptor, $"{profilingName} Neighbor Max", false, FilterMode.Point, TextureWrapMode.Clamp);

        RecordTileMax(renderGraph, mask, motion, tileMax, sourceTexelSize, tileSize);
        RecordNeighborMax(renderGraph, tileMax, neighborMax, tileTexelSize);
        RecordComposite(renderGraph, resourceData.activeColorTexture, source, mask, neighborMax, sourceTexelSize);
    }

    private void RecordTileMax(RenderGraph renderGraph, TextureHandle mask, TextureHandle motion, TextureHandle destination, Vector4 sourceTexelSize, int tileSize)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass($"{profilingName} Tile Max", out TileMaxPassData passData, profilingSampler);

        passData.Mask = mask;
        passData.Motion = motion;
        passData.Material = materials.TileMax.Material;
        passData.SourceTexelSize = sourceTexelSize;
        passData.TileSize = tileSize;
        passData.MaskThreshold = Mathf.Clamp01(settings.MaskThreshold);
        passData.MaskSoftness = Mathf.Clamp(settings.MaskSoftness, 0.0001f, 0.5f);

        builder.UseTexture(mask, AccessFlags.Read);
        builder.UseTexture(motion, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
        builder.SetRenderFunc((TileMaxPassData data, RasterGraphContext context) => ExecuteTileMax(data, context));
    }

    private void RecordNeighborMax(RenderGraph renderGraph, TextureHandle source, TextureHandle destination, Vector4 tileTexelSize)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass($"{profilingName} Neighbor Max", out NeighborMaxPassData passData, profilingSampler);

        passData.Velocity = source;
        passData.Material = materials.NeighborMax.Material;
        passData.TileTexelSize = tileTexelSize;

        builder.UseTexture(source, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
        builder.SetRenderFunc((NeighborMaxPassData data, RasterGraphContext context) => ExecuteNeighborMax(data, context));
    }

    private void RecordComposite(RenderGraph renderGraph, TextureHandle destination, TextureHandle source, TextureHandle mask, TextureHandle velocity, Vector4 sourceTexelSize)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass($"{profilingName} Composite", out CompositePassData passData, profilingSampler);

        float deltaTime = Time.unscaledDeltaTime;
        float exposureScale = deltaTime > 0.00001f ? Mathf.Clamp(settings.ShutterDuration / deltaTime * Mathf.Clamp(settings.MotionVectorScale, 0f, 4f), 0f, 8f) : 0f;

        passData.Source = source;
        passData.Mask = mask;
        passData.Velocity = velocity;
        passData.Material = materials.Composite.Material;
        passData.SourceTexelSize = sourceTexelSize;
        passData.ExposureScale = exposureScale;
        passData.MaxBlurPixels = Mathf.Clamp(settings.MaxBlurPixels, 0f, 96f);
        passData.SampleCount = Mathf.Clamp(settings.SampleCount, 4, 24);
        passData.MaskThreshold = Mathf.Clamp01(settings.MaskThreshold);
        passData.MaskSoftness = Mathf.Clamp(settings.MaskSoftness, 0.0001f, 0.5f);
        passData.Intensity = Mathf.Clamp01(settings.Intensity);

        builder.UseTexture(source, AccessFlags.Read);
        builder.UseTexture(mask, AccessFlags.Read);
        builder.UseTexture(velocity, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
        builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) => ExecuteComposite(data, context));
    }

    private TextureHandle ResolveSourceTexture(ContextContainer frameData, UniversalResourceData resourceData, UniversalCameraData cameraData, out Vector4 sourceTexelSize)
    {
        if (FrameTextureRegistry.TryGet(frameData, out FrameTextureRegistry registry) && registry.TryGetTexture(sourceTextureId, out TextureHandle source, out sourceTexelSize))
        {
            return source;
        }

        RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
        sourceTexelSize = MaskedEffectRenderGraphUtility.CreateTexelSize(descriptor.width, descriptor.height);
        return resourceData.activeColorTexture;
    }

    private static RenderTextureDescriptor CreateTileDescriptor(RenderTextureDescriptor cameraDescriptor, int tileSize)
    {
        cameraDescriptor.width = Mathf.Max(1, (cameraDescriptor.width + tileSize - 1) / tileSize);
        cameraDescriptor.height = Mathf.Max(1, (cameraDescriptor.height + tileSize - 1) / tileSize);
        cameraDescriptor.depthBufferBits = 0;
        cameraDescriptor.depthStencilFormat = GraphicsFormat.None;
        cameraDescriptor.msaaSamples = 1;
        cameraDescriptor.bindMS = false;
        cameraDescriptor.useMipMap = false;
        cameraDescriptor.autoGenerateMips = false;
        cameraDescriptor.enableRandomWrite = false;
        cameraDescriptor.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render) ? GraphicsFormat.R16G16B16A16_SFloat : GraphicsFormat.R32G32B32A32_SFloat;
        return cameraDescriptor;
    }

    private static void ExecuteTileMax(TileMaxPassData data, RasterGraphContext context)
    {
        data.Material.SetTexture(MotionTextureId, data.Motion);
        data.Material.SetVector(SourceTexelSizeId, data.SourceTexelSize);
        data.Material.SetInt(TileSizeId, data.TileSize);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(MaskSoftnessId, data.MaskSoftness);

        Blitter.BlitTexture(context.cmd, data.Mask, new Vector4(1f, 1f, 0f, 0f), data.Material, TileMaxPassIndex);
    }

    private static void ExecuteNeighborMax(NeighborMaxPassData data, RasterGraphContext context)
    {
        data.Material.SetVector(TileTexelSizeId, data.TileTexelSize);
        Blitter.BlitTexture(context.cmd, data.Velocity, new Vector4(1f, 1f, 0f, 0f), data.Material, NeighborMaxPassIndex);
    }

    private static void ExecuteComposite(CompositePassData data, RasterGraphContext context)
    {
        data.Material.SetTexture(SourceTextureId, data.Source);
        data.Material.SetTexture(VelocityTextureId, data.Velocity);
        data.Material.SetVector(SourceTexelSizeId, data.SourceTexelSize);
        data.Material.SetFloat(ExposureScaleId, data.ExposureScale);
        data.Material.SetFloat(MaxBlurPixelsId, data.MaxBlurPixels);
        data.Material.SetInt(SampleCountId, data.SampleCount);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(MaskSoftnessId, data.MaskSoftness);
        data.Material.SetFloat(IntensityId, data.Intensity);

        Blitter.BlitTexture(context.cmd, data.Mask, new Vector4(1f, 1f, 0f, 0f), data.Material, CompositePassIndex);
    }
}
