using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

internal sealed class ThermalVisionPass : ScriptableRenderPass
{
    private static readonly int SourceTextureId = Shader.PropertyToID("_ThermalSourceTexture");
    private static readonly int ThroughWallMaskId = Shader.PropertyToID("_ThermalThroughWallMask");
    private static readonly int PlayerMaskId = Shader.PropertyToID("_ThermalPlayerMask");
    private static readonly int SourceTexelSizeId = Shader.PropertyToID("_SourceTexelSize");
    private static readonly int CoolShadowId = Shader.PropertyToID("_CoolShadow");
    private static readonly int CoolMidId = Shader.PropertyToID("_CoolMid");
    private static readonly int CoolHighlightId = Shader.PropertyToID("_CoolHighlight");
    private static readonly int ColdHeatId = Shader.PropertyToID("_ColdHeat");
    private static readonly int WarmHeatId = Shader.PropertyToID("_WarmHeat");
    private static readonly int HotHeatId = Shader.PropertyToID("_HotHeat");
    private static readonly int CoreHeatId = Shader.PropertyToID("_CoreHeat");
    private static readonly int EnvironmentContrastId = Shader.PropertyToID("_EnvironmentContrast");
    private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");
    private static readonly int MaskSoftnessId = Shader.PropertyToID("_MaskSoftness");
    private static readonly int SurfaceDetailId = Shader.PropertyToID("_SurfaceDetail");
    private static readonly int EdgeIntensityId = Shader.PropertyToID("_EdgeIntensity");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int ScanlineStrengthId = Shader.PropertyToID("_ScanlineStrength");
    private static readonly int ScanlineFrequencyId = Shader.PropertyToID("_ScanlineFrequency");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int ActivationId = Shader.PropertyToID("_Activation");
    private static readonly int ThroughWallsId = Shader.PropertyToID("_ThroughWalls");
    private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");

    private string profilingName;
    private ThermalVisionFeature.Settings settings;
    private Material material;
    private int sourceTextureId;
    private float activation;
    private bool throughWalls;
    private readonly MaskedTextureResolver visibleMaskResolver = new(nameof(ThermalVisionFeature));
    private readonly MaskedTextureResolver throughWallMaskResolver = new(nameof(ThermalVisionFeature));
    private readonly MaskedTextureResolver playerMaskResolver = new(nameof(ThermalVisionFeature));

    private sealed class PassData
    {
        public TextureHandle SourceTexture;
        public TextureHandle VisibleMask;
        public TextureHandle ThroughWallMask;
        public TextureHandle PlayerMask;
        public Material Material;
        public Vector4 SourceTexelSize;
        public Color CoolShadow;
        public Color CoolMid;
        public Color CoolHighlight;
        public Color ColdHeat;
        public Color WarmHeat;
        public Color HotHeat;
        public Color CoreHeat;
        public float EnvironmentContrast;
        public float MaskThreshold;
        public float MaskSoftness;
        public float SurfaceDetail;
        public float EdgeIntensity;
        public float NoiseStrength;
        public float ScanlineStrength;
        public float ScanlineFrequency;
        public float Opacity;
        public float Activation;
        public float ThroughWalls;
        public float TimeOffset;
    }

    public void Setup(
        string passName,
        ThermalVisionFeature.Settings passSettings,
        Material passMaterial,
        int snapshotTextureId,
        float intensity,
        bool useThroughWalls)
    {
        settings = passSettings;
        material = passMaterial;
        sourceTextureId = snapshotTextureId;
        activation = Mathf.Clamp01(intensity);
        throughWalls = useThroughWalls;
        visibleMaskResolver.SetTextureName(settings.VisibleMaskTextureName);
        throughWallMaskResolver.SetTextureName(settings.ThroughWallMaskTextureName);
        playerMaskResolver.SetTextureName(settings.PlayerMaskTextureName);

        renderPassEvent = settings.RenderPassEvent;
        profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(
            passName,
            ref profilingName,
            profilingSampler);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (settings == null || material == null || activation <= 0.0001f)
            return;

        if (!visibleMaskResolver.TryResolve(frameData, out TextureHandle visibleMask, out _) ||
            !throughWallMaskResolver.TryResolve(frameData, out TextureHandle throughWallMask, out _) ||
            !playerMaskResolver.TryResolve(frameData, out TextureHandle playerMask, out _))
        {
            return;
        }

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        TextureHandle sourceTexture = ResolveSourceTexture(frameData, resourceData, out Vector4 sourceTexelSize);

        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            profilingName,
            out PassData passData,
            profilingSampler);

        passData.SourceTexture = sourceTexture;
        passData.VisibleMask = visibleMask;
        passData.ThroughWallMask = throughWallMask;
        passData.PlayerMask = playerMask;
        passData.Material = material;
        passData.SourceTexelSize = sourceTexelSize;
        passData.CoolShadow = settings.CoolShadow;
        passData.CoolMid = settings.CoolMid;
        passData.CoolHighlight = settings.CoolHighlight;
        passData.ColdHeat = settings.ColdHeat;
        passData.WarmHeat = settings.WarmHeat;
        passData.HotHeat = settings.HotHeat;
        passData.CoreHeat = settings.CoreHeat;
        passData.EnvironmentContrast = Mathf.Clamp(settings.EnvironmentContrast, 0.25f, 3f);
        passData.MaskThreshold = Mathf.Clamp01(settings.MaskThreshold);
        passData.MaskSoftness = Mathf.Clamp(settings.MaskSoftness, 0f, 0.5f);
        passData.SurfaceDetail = Mathf.Clamp01(settings.SurfaceDetail);
        passData.EdgeIntensity = Mathf.Clamp(settings.EdgeIntensity, 0f, 3f);
        passData.NoiseStrength = Mathf.Clamp(settings.NoiseStrength, 0f, 0.25f);
        passData.ScanlineStrength = Mathf.Clamp(settings.ScanlineStrength, 0f, 0.25f);
        passData.ScanlineFrequency = Mathf.Clamp(settings.ScanlineFrequency, 20f, 600f);
        passData.Opacity = Mathf.Clamp01(settings.Opacity);
        passData.Activation = activation;
        passData.ThroughWalls = throughWalls ? 1f : 0f;
        passData.TimeOffset = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;

        builder.UseTexture(sourceTexture, AccessFlags.Read);
        builder.UseTexture(visibleMask, AccessFlags.Read);
        builder.UseTexture(throughWallMask, AccessFlags.Read);
        builder.UseTexture(playerMask, AccessFlags.Read);
        builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
        builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
    }

    private TextureHandle ResolveSourceTexture(
        ContextContainer frameData,
        UniversalResourceData resourceData,
        out Vector4 sourceTexelSize)
    {
        if (FrameTextureRegistry.TryGet(frameData, out FrameTextureRegistry registry) &&
            registry.TryGetTexture(sourceTextureId, out TextureHandle sourceTexture, out sourceTexelSize))
        {
            return sourceTexture;
        }

        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
        sourceTexelSize = MaskedEffectRenderGraphUtility.CreateTexelSize(descriptor.width, descriptor.height);
        return resourceData.activeColorTexture;
    }

    private static void ExecutePass(PassData data, RasterGraphContext context)
    {
        data.Material.SetTexture(SourceTextureId, data.SourceTexture);
        data.Material.SetTexture(ThroughWallMaskId, data.ThroughWallMask);
        data.Material.SetTexture(PlayerMaskId, data.PlayerMask);
        data.Material.SetVector(SourceTexelSizeId, data.SourceTexelSize);
        data.Material.SetColor(CoolShadowId, data.CoolShadow);
        data.Material.SetColor(CoolMidId, data.CoolMid);
        data.Material.SetColor(CoolHighlightId, data.CoolHighlight);
        data.Material.SetColor(ColdHeatId, data.ColdHeat);
        data.Material.SetColor(WarmHeatId, data.WarmHeat);
        data.Material.SetColor(HotHeatId, data.HotHeat);
        data.Material.SetColor(CoreHeatId, data.CoreHeat);
        data.Material.SetFloat(EnvironmentContrastId, data.EnvironmentContrast);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(MaskSoftnessId, data.MaskSoftness);
        data.Material.SetFloat(SurfaceDetailId, data.SurfaceDetail);
        data.Material.SetFloat(EdgeIntensityId, data.EdgeIntensity);
        data.Material.SetFloat(NoiseStrengthId, data.NoiseStrength);
        data.Material.SetFloat(ScanlineStrengthId, data.ScanlineStrength);
        data.Material.SetFloat(ScanlineFrequencyId, data.ScanlineFrequency);
        data.Material.SetFloat(OpacityId, data.Opacity);
        data.Material.SetFloat(ActivationId, data.Activation);
        data.Material.SetFloat(ThroughWallsId, data.ThroughWalls);
        data.Material.SetFloat(TimeOffsetId, data.TimeOffset);

        Blitter.BlitTexture(
            context.cmd,
            data.VisibleMask,
            new Vector4(1f, 1f, 0f, 0f),
            data.Material,
            0);
    }
}
