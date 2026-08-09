using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class MaskDistortionFeature : ScriptableRendererFeature
{
    private const string CompositeShaderName = "Hidden/Chapter9/MaskDistortion/Composite";

    [Tooltip("Base name used for profiling markers and generated render pass names.")]
    public string ProfilingName = "Mask Distortion";

    [Tooltip("Material that uses Hidden/Chapter9/MaskDistortion/Composite. Assign the included MaskDistortionComposite material for build-safe shader references.")]
    public Material DistortionMaterial;

    [Tooltip("Settings for the masked screen-space distortion effect.")]
    public Settings DistortionSettings = new();

    private FrameColorSnapshotPass _sourceSnapshotPass;
    private MaskDistortionPass _distortionPass;
    private readonly MaskedEffectMaterialCache _materialCache = new(nameof(MaskDistortionFeature), CompositeShaderName);

    [Serializable]
    public class Settings
    {
        [Tooltip("Disables the distortion without removing the renderer feature.")]
        public bool Enabled = true;

        [Tooltip("When the distortion runs. It must run after the mask texture has been produced.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Texture name produced by ObjectsToRenderTextureFeature, for example _FrostedGlassBlurMask, _PortalMask, or _HeatHazeMask.")]
        public string MaskTextureName = "_MaskDistortionMask";

        [Tooltip("Maximum screen-space distortion measured in pixels. 0 disables warping but still allows tinting.")]
        [Range(0.0f, 32.0f)] public float DistortionStrengthPixels = 4.0f;

        [Tooltip("Number of wave bands across the screen. Higher values create tighter shimmer.")]
        [Range(0.1f, 80.0f)] public float DistortionFrequency = 18.0f;

        [Tooltip("Animation speed for the procedural distortion waves. 0 freezes the distortion pattern.")]
        [Range(-10.0f, 10.0f)] public float DistortionSpeed = 0.35f;

        [Tooltip("Extra red/blue channel separation measured in pixels. Keep this subtle; high values look glitchy.")]
        [Range(0.0f, 8.0f)]
        public float ChromaticAberrationPixels = 0.4f;

        [Tooltip("Mask value where the effect starts. Pure black remains unaffected.")]
        [Range(0.0f, 1.0f)] public float MaskThreshold = 0.5f;

        [Tooltip("Soft transition width above Mask Threshold. Higher values make mask edges fade in more smoothly.")]
        [Range(0.0f, 1.0f)] public float MaskSoftness = 0.08f;

        [Tooltip("Overall blend strength. 0 skips the effect, 1 fully applies the distorted color inside the mask.")]
        [Range(0.0f, 1.0f)] public float Opacity = 0.65f;

        [Tooltip("Optional color mixed into the distorted area. Use alpha and Tint Strength together for subtle glass, heat, or magic color.")] 
        public Color TintColor = new(0.75f, 0.95f, 1.0f, 1.0f);

        [Tooltip("How much Tint Color is mixed into the distorted area.")]
        [Range(0.0f, 1.0f)] public float TintStrength = 0.08f;
    }

    public override void Create()
    {
        _sourceSnapshotPass ??= new FrameColorSnapshotPass();
        _distortionPass ??= new MaskDistortionPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        DistortionSettings ??= new Settings();

        if (!ShouldEnqueue(DistortionSettings))
            return;

        if (!_materialCache.Ensure(DistortionMaterial))
            return;

        _sourceSnapshotPass.Setup($"{ProfilingName} Source", DistortionSettings.RenderPassEvent);
        _distortionPass.Setup(ProfilingName, DistortionSettings, _materialCache.Material, _sourceSnapshotPass.SnapshotTextureId);

        renderer.EnqueuePass(_sourceSnapshotPass);
        renderer.EnqueuePass(_distortionPass);
    }

    private static bool ShouldEnqueue(Settings settings)
    {
        return settings != null && settings.Enabled && settings.Opacity > 0.0f && !string.IsNullOrWhiteSpace(settings.MaskTextureName) &&
               (settings.DistortionStrengthPixels > 0.0f || settings.ChromaticAberrationPixels > 0.0f || settings.TintStrength > 0.0f);
    }

    protected override void Dispose(bool disposing)
    {
        _materialCache.Dispose();
    }

    private sealed class MaskDistortionPass : ScriptableRenderPass
    {
        private static readonly int SourceTextureId = Shader.PropertyToID("_MaskDistortionSourceTexture");
        private static readonly int SourceTexelSizeId = Shader.PropertyToID("_SourceTexelSize");
        private static readonly int DistortionStrengthPixelsId = Shader.PropertyToID("_DistortionStrengthPixels");
        private static readonly int DistortionFrequencyId = Shader.PropertyToID("_DistortionFrequency");
        private static readonly int DistortionSpeedId = Shader.PropertyToID("_DistortionSpeed");
        private static readonly int ChromaticAberrationPixelsId = Shader.PropertyToID("_ChromaticAberrationPixels");
        private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");
        private static readonly int MaskSoftnessId = Shader.PropertyToID("_MaskSoftness");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int TintStrengthId = Shader.PropertyToID("_TintStrength");
        private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");

        private string _profilingName;
        private Settings _settings;
        private Material _material;
        private int _sourceTextureId;
        private readonly MaskedTextureResolver _maskResolver = new(nameof(MaskDistortionFeature));

        private class PassData
        {
            public TextureHandle SourceTexture;
            public TextureHandle MaskTexture;
            public Material Material;
            public Vector4 SourceTexelSize;
            public float DistortionStrengthPixels;
            public float DistortionFrequency;
            public float DistortionSpeed;
            public float ChromaticAberrationPixels;
            public float MaskThreshold;
            public float MaskSoftness;
            public float Opacity;
            public Color TintColor;
            public float TintStrength;
            public float TimeOffset;
        }

        public void Setup(string profilingName, Settings settings, Material material, int sourceTextureId)
        {
            _settings = settings;
            _material = material;
            _sourceTextureId = sourceTextureId;
            _maskResolver.SetTextureName(settings.MaskTextureName);

            renderPassEvent = settings.RenderPassEvent;
            profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(profilingName, ref _profilingName, profilingSampler);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null || _settings == null)
                return;

            if (!_maskResolver.TryResolve(frameData, out TextureHandle maskTexture, out _))
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle sourceTexture = GetSourceTexture(frameData, resourceData, out Vector4 sourceTexelSize);

            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(_profilingName, out PassData passData, profilingSampler);

            passData.SourceTexture = sourceTexture;
            passData.MaskTexture = maskTexture;
            passData.Material = _material;
            passData.SourceTexelSize = sourceTexelSize;
            passData.DistortionStrengthPixels = Mathf.Clamp(_settings.DistortionStrengthPixels, 0.0f, 32.0f);
            passData.DistortionFrequency = Mathf.Clamp(_settings.DistortionFrequency, 0.1f, 80.0f);
            passData.DistortionSpeed = Mathf.Clamp(_settings.DistortionSpeed, -10.0f, 10.0f);
            passData.ChromaticAberrationPixels = Mathf.Clamp(_settings.ChromaticAberrationPixels, 0.0f, 8.0f);
            passData.MaskThreshold = Mathf.Clamp01(_settings.MaskThreshold);
            passData.MaskSoftness = Mathf.Clamp01(_settings.MaskSoftness);
            passData.Opacity = Mathf.Clamp01(_settings.Opacity);
            passData.TintColor = _settings.TintColor;
            passData.TintStrength = Mathf.Clamp01(_settings.TintStrength);
            passData.TimeOffset = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;

            builder.UseTexture(sourceTexture, AccessFlags.Read);
            builder.UseTexture(maskTexture, AccessFlags.Read);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
        }

        private TextureHandle GetSourceTexture(ContextContainer frameData, UniversalResourceData resourceData, out Vector4 sourceTexelSize)
        {
            if (FrameTextureRegistry.TryGet(frameData, out FrameTextureRegistry textureRegistry))
            {
                if (textureRegistry.TryGetTexture(_sourceTextureId, out TextureHandle sourceTexture, out sourceTexelSize))
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
            data.Material.SetVector(SourceTexelSizeId, data.SourceTexelSize);
            data.Material.SetFloat(DistortionStrengthPixelsId, data.DistortionStrengthPixels);
            data.Material.SetFloat(DistortionFrequencyId, data.DistortionFrequency);
            data.Material.SetFloat(DistortionSpeedId, data.DistortionSpeed);
            data.Material.SetFloat(ChromaticAberrationPixelsId, data.ChromaticAberrationPixels);
            data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
            data.Material.SetFloat(MaskSoftnessId, data.MaskSoftness);
            data.Material.SetFloat(OpacityId, data.Opacity);
            data.Material.SetColor(TintColorId, data.TintColor);
            data.Material.SetFloat(TintStrengthId, data.TintStrength);
            data.Material.SetFloat(TimeOffsetId, data.TimeOffset);

            Blitter.BlitTexture(context.cmd, data.MaskTexture, new Vector4(1, 1, 0, 0), data.Material, 0);
        }
    }
}
