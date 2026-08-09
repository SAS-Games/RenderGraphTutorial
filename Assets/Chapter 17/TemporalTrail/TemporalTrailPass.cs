using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

internal sealed class TemporalTrailPass : ScriptableRenderPass
{
    private static readonly int SourceTextureId = Shader.PropertyToID("_TemporalSourceTexture");
    private static readonly int HistoryTextureId = Shader.PropertyToID("_TemporalHistoryTexture");
    private static readonly int MotionTextureId = Shader.PropertyToID("_TemporalMotionTexture");
    private static readonly int MaskTextureId = Shader.PropertyToID("_TemporalMaskTexture");
    private static readonly int HistoryValidId = Shader.PropertyToID("_HistoryValid");
    private static readonly int HistoryRetentionId = Shader.PropertyToID("_HistoryRetention");
    private static readonly int CaptureCurrentFrameId = Shader.PropertyToID("_CaptureCurrentFrame");
    private static readonly int MotionVectorScaleId = Shader.PropertyToID("_MotionVectorScale");
    private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");
    private static readonly int MaskSoftnessId = Shader.PropertyToID("_MaskSoftness");
    private static readonly int TrailColorId = Shader.PropertyToID("_TrailColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int SuppressCurrentFrameId = Shader.PropertyToID("_SuppressCurrentFrame");

    private readonly MaskedTextureResolver maskResolver = new(nameof(TemporalTrailFeature));
    private string profilingName;
    private TemporalTrailFeature.Settings settings;
    private Material material;
    private int sourceTextureId;
    private TemporalTrailHistoryFrame historyFrame;

    private sealed class AccumulationPassData
    {
        public TextureHandle Source;
        public TextureHandle Mask;
        public TextureHandle History;
        public TextureHandle Motion;
        public Material Material;
        public float HistoryValid;
        public float HistoryRetention;
        public float CaptureCurrentFrame;
        public float MotionVectorScale;
        public float MaskThreshold;
        public float MaskSoftness;
    }

    private sealed class CompositePassData
    {
        public TextureHandle Source;
        public TextureHandle Mask;
        public TextureHandle History;
        public Material Material;
        public Color TrailColor;
        public float Intensity;
        public float MaskThreshold;
        public float MaskSoftness;
        public float SuppressCurrentFrame;
    }

    public void Setup(
        string passName,
        TemporalTrailFeature.Settings passSettings,
        Material passMaterial,
        int snapshotTextureId)
    {
        settings = passSettings;
        material = passMaterial;
        sourceTextureId = snapshotTextureId;
        maskResolver.SetTextureName(settings.MaskTextureName);

        ConfigureInput(settings.MotionCompensation
            ? ScriptableRenderPassInput.Motion
            : ScriptableRenderPassInput.None);
        renderPassEvent = settings.RenderPassEvent;
        profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(
            passName,
            ref profilingName,
            profilingSampler);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (settings == null || material == null)
            return;

        if (!maskResolver.TryResolve(frameData, out TextureHandle mask, out _))
            return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        TextureHandle source = ResolveSourceTexture(frameData, resourceData);

        if (!source.IsValid() || !mask.IsValid() || cameraData.historyManager == null)
            return;

        UniversalCameraHistory historyManager = cameraData.historyManager;
        historyManager.RequestAccess<TemporalTrailHistory>();

        TemporalTrailHistory history = historyManager.GetHistoryForWrite<TemporalTrailHistory>();
        if (history == null)
            return;

        RenderTextureDescriptor descriptor = TemporalTrailFeature.CreateHistoryDescriptor(
            cameraData.cameraTargetDescriptor,
            settings.HistoryResolutionScale);
        history.Update(ref descriptor);
        historyFrame = history.Prepare(cameraData.camera, settings);

        if (historyFrame.Read == null || historyFrame.Write == null)
            return;

        TextureHandle previousHistory = renderGraph.ImportTexture(historyFrame.Read);
        TextureHandle currentHistory = renderGraph.ImportTexture(historyFrame.Write);
        TextureHandle motion = settings.MotionCompensation
            ? resourceData.motionVectorColor
            : renderGraph.defaultResources.blackTexture;

        if (!previousHistory.IsValid() || !currentHistory.IsValid() || !motion.IsValid())
        {
            return;
        }

        RecordAccumulation(renderGraph, source, mask, previousHistory, currentHistory, motion);
        RecordComposite(renderGraph, resourceData.activeColorTexture, source, mask, currentHistory);
        historyFrame.Commit();
    }

    private void RecordAccumulation(
        RenderGraph renderGraph,
        TextureHandle source,
        TextureHandle mask,
        TextureHandle previousHistory,
        TextureHandle currentHistory,
        TextureHandle motion)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            $"{profilingName} Accumulation",
            out AccumulationPassData passData,
            profilingSampler);

        passData.Source = source;
        passData.Mask = mask;
        passData.History = previousHistory;
        passData.Motion = motion;
        passData.Material = material;
        passData.HistoryValid = historyFrame.IsValid ? 1f : 0f;
        passData.HistoryRetention = historyFrame.Retention;
        passData.CaptureCurrentFrame = historyFrame.CaptureCurrentFrame ? 1f : 0f;
        passData.MotionVectorScale = settings.MotionCompensation
            ? Mathf.Clamp(settings.MotionVectorScale, 0f, 2f)
            : 0f;
        passData.MaskThreshold = Mathf.Clamp01(settings.MaskThreshold);
        passData.MaskSoftness = Mathf.Clamp(settings.MaskSoftness, 0.0001f, 0.5f);

        builder.UseTexture(source, AccessFlags.Read);
        builder.UseTexture(mask, AccessFlags.Read);
        builder.UseTexture(previousHistory, AccessFlags.Read);
        builder.UseTexture(motion, AccessFlags.Read);
        builder.SetRenderAttachment(currentHistory, 0, AccessFlags.Write);
        builder.SetRenderFunc((AccumulationPassData data, RasterGraphContext context) =>
            ExecuteAccumulation(data, context));
    }

    private void RecordComposite(
        RenderGraph renderGraph,
        TextureHandle destination,
        TextureHandle source,
        TextureHandle mask,
        TextureHandle currentHistory)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            $"{profilingName} Composite",
            out CompositePassData passData,
            profilingSampler);

        passData.Source = source;
        passData.Mask = mask;
        passData.History = currentHistory;
        passData.Material = material;
        passData.TrailColor = settings.TrailColor;
        passData.Intensity = Mathf.Clamp(settings.Intensity, 0f, 3f);
        passData.MaskThreshold = Mathf.Clamp01(settings.MaskThreshold);
        passData.MaskSoftness = Mathf.Clamp(settings.MaskSoftness, 0.0001f, 0.5f);
        passData.SuppressCurrentFrame = Mathf.Clamp01(settings.SuppressCurrentFrame);

        builder.UseTexture(source, AccessFlags.Read);
        builder.UseTexture(mask, AccessFlags.Read);
        builder.UseTexture(currentHistory, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
        builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) =>
            ExecuteComposite(data, context));
    }

    private TextureHandle ResolveSourceTexture(
        ContextContainer frameData,
        UniversalResourceData resourceData)
    {
        if (FrameTextureRegistry.TryGet(frameData, out FrameTextureRegistry registry) &&
            registry.TryGetTexture(sourceTextureId, out TextureHandle sourceTexture, out _))
        {
            return sourceTexture;
        }

        return resourceData.activeColorTexture;
    }

    private static void ExecuteAccumulation(
        AccumulationPassData data,
        RasterGraphContext context)
    {
        data.Material.SetTexture(SourceTextureId, data.Source);
        data.Material.SetTexture(HistoryTextureId, data.History);
        data.Material.SetTexture(MotionTextureId, data.Motion);
        data.Material.SetFloat(HistoryValidId, data.HistoryValid);
        data.Material.SetFloat(HistoryRetentionId, data.HistoryRetention);
        data.Material.SetFloat(CaptureCurrentFrameId, data.CaptureCurrentFrame);
        data.Material.SetFloat(MotionVectorScaleId, data.MotionVectorScale);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(MaskSoftnessId, data.MaskSoftness);

        Blitter.BlitTexture(
            context.cmd,
            data.Mask,
            new Vector4(1f, 1f, 0f, 0f),
            data.Material,
            0);
    }

    private static void ExecuteComposite(
        CompositePassData data,
        RasterGraphContext context)
    {
        data.Material.SetTexture(HistoryTextureId, data.History);
        data.Material.SetTexture(MaskTextureId, data.Mask);
        data.Material.SetColor(TrailColorId, data.TrailColor);
        data.Material.SetFloat(IntensityId, data.Intensity);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(MaskSoftnessId, data.MaskSoftness);
        data.Material.SetFloat(SuppressCurrentFrameId, data.SuppressCurrentFrame);

        Blitter.BlitTexture(
            context.cmd,
            data.Source,
            new Vector4(1f, 1f, 0f, 0f),
            data.Material,
            1);
    }
}
