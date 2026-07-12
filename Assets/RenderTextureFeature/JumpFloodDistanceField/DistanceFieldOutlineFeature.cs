using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class DistanceFieldOutlineFeature : ScriptableRendererFeature
{
    private const string ShaderName =
        "Hidden/RenderTextureFeature/JumpFloodDistanceField/Outline";

    [Tooltip("Name used for the Render Graph pass and profiler marker.")]
    public string ProfilingName = "Distance Field Outline";

    [Tooltip("Material that uses Hidden/RenderTextureFeature/JumpFloodDistanceField/Outline. Assign the included material so the shader is retained in player builds.")]
    public Material OutlineMaterial;

    [Tooltip("Signed-distance source, band shape, color, and composition settings.")]
    public Settings OutlineSettings = new();

    private DistanceFieldOutlinePass _pass;
    private readonly MaskedEffectMaterialCache _materialCache =
        new(nameof(DistanceFieldOutlineFeature), ShaderName);
    private readonly MaskedEffectItemPool<MaskedEffectMaterialInstance> _materials =
        new(source => new MaskedEffectMaterialInstance(source));

    public enum Placement
    {
        Outside = 0,
        Inside = 1,
        Both = 2
    }

    public enum Composition
    {
        Alpha = 0,
        Additive = 1
    }

    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Disables the effect without removing the renderer feature.")]
        public bool Enabled = true;

        [Tooltip("When the band is composited. Use the same event as the JFA producer and place this feature after it in the renderer feature list.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Signed-distance texture published by JumpFloodDistanceFieldFeature.")]
        public string DistanceTextureName = "_JfaSignedDistance";

        [Tooltip("Draws the band outside the mask, inside it, or on both sides of the silhouette.")]
        public Placement BandPlacement = Placement.Outside;

        [Tooltip("Distance from the silhouette where the band begins, measured in input-mask pixels. Use 0 for a normal outline and a larger value for a detached ring.")]
        [Range(0.0f, 1024.0f)]
        public float BandStartPixels;

        [Tooltip("Distance from the silhouette where the band ends, measured in input-mask pixels. The JFA Max Distance Pixels must be at least this value plus Softness Pixels.")]
        [Range(0.5f, 1024.0f)]
        public float BandEndPixels = 16.0f;

        [Tooltip("Soft transition width at the band boundaries, measured in input-mask pixels. Zero still uses derivative anti-aliasing.")]
        [Range(0.0f, 128.0f)]
        public float SoftnessPixels = 2.0f;

        [Tooltip("HDR band color. Alpha controls coverage strength for both composition modes.")]
        [ColorUsage(true, true)]
        public Color Color = new(0.0f, 0.75f, 1.0f, 1.0f);

        [Tooltip("Color brightness multiplier. Values above 1 can drive bloom when HDR and post-processing are enabled.")]
        [Range(0.0f, 10.0f)]
        public float Intensity = 1.5f;

        [Tooltip("Overall effect visibility.")]
        [Range(0.0f, 1.0f)]
        public float Opacity = 1.0f;

        [Tooltip("Alpha replaces the scene according to coverage. Additive adds light and is useful for glow or bloom.")]
        public Composition BlendMode = Composition.Alpha;
    }

    public override void Create()
    {
        _pass ??= new DistanceFieldOutlinePass();
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        OutlineSettings ??= new Settings();
        if (!ShouldEnqueue(OutlineSettings) || !_materialCache.Ensure(OutlineMaterial))
        {
            return;
        }

        _materials.EnsureCount(1, _materialCache.Material, _materialCache.Version);
        _pass.Setup(
            ProfilingName,
            OutlineSettings,
            _materials[0].Material);
        renderer.EnqueuePass(_pass);
    }

    private static bool ShouldEnqueue(Settings settings)
    {
        return settings != null &&
               settings.Enabled &&
               settings.BandEndPixels > 0.0f &&
               settings.Intensity > 0.0f &&
               settings.Opacity > 0.0f &&
               !string.IsNullOrWhiteSpace(settings.DistanceTextureName);
    }

    protected override void Dispose(bool disposing)
    {
        _materials.Dispose();
        _materialCache.Dispose();
    }

    private sealed class DistanceFieldOutlinePass : ScriptableRenderPass
    {
        private static readonly int BandPlacementId = Shader.PropertyToID("_BandPlacement");
        private static readonly int BandStartPixelsId = Shader.PropertyToID("_BandStartPixels");
        private static readonly int BandEndPixelsId = Shader.PropertyToID("_BandEndPixels");
        private static readonly int SoftnessPixelsId = Shader.PropertyToID("_SoftnessPixels");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private readonly FrameTextureResolver _distanceResolver =
            new(nameof(DistanceFieldOutlineFeature));
        private string _profilingName;
        private Settings _settings;
        private Material _material;

        private sealed class PassData
        {
            public TextureHandle DistanceTexture;
            public Material Material;
            public float BandPlacement;
            public float BandStartPixels;
            public float BandEndPixels;
            public float SoftnessPixels;
            public Color Color;
            public float Intensity;
            public float Opacity;
            public int MaterialPass;
        }

        public void Setup(
            string profilingName,
            Settings settings,
            Material material)
        {
            _settings = settings;
            _material = material;
            _distanceResolver.SetTextureName(settings.DistanceTextureName);
            renderPassEvent = settings.RenderPassEvent;
            profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(
                profilingName,
                ref _profilingName,
                profilingSampler);
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            if (_settings == null || _material == null ||
                !_distanceResolver.TryResolve(
                    frameData,
                    out TextureHandle distanceTexture,
                    out _))
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                _profilingName,
                out PassData passData,
                profilingSampler);

            float bandStart = Mathf.Max(0.0f, _settings.BandStartPixels);
            passData.DistanceTexture = distanceTexture;
            passData.Material = _material;
            passData.BandPlacement = (float)_settings.BandPlacement;
            passData.BandStartPixels = bandStart;
            passData.BandEndPixels = Mathf.Max(bandStart + 0.001f, _settings.BandEndPixels);
            passData.SoftnessPixels = Mathf.Max(0.0f, _settings.SoftnessPixels);
            passData.Color = _settings.Color;
            passData.Intensity = Mathf.Max(0.0f, _settings.Intensity);
            passData.Opacity = Mathf.Clamp01(_settings.Opacity);
            passData.MaterialPass = Mathf.Clamp((int)_settings.BlendMode, 0, 1);

            builder.UseTexture(distanceTexture, AccessFlags.Read);
            builder.SetRenderAttachment(
                resourceData.activeColorTexture,
                0,
                AccessFlags.ReadWrite);
            builder.SetRenderFunc(
                (PassData data, RasterGraphContext context) => ExecutePass(data, context));
        }

        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            data.Material.SetFloat(BandPlacementId, data.BandPlacement);
            data.Material.SetFloat(BandStartPixelsId, data.BandStartPixels);
            data.Material.SetFloat(BandEndPixelsId, data.BandEndPixels);
            data.Material.SetFloat(SoftnessPixelsId, data.SoftnessPixels);
            data.Material.SetColor(ColorId, data.Color);
            data.Material.SetFloat(IntensityId, data.Intensity);
            data.Material.SetFloat(OpacityId, data.Opacity);

            Blitter.BlitTexture(
                context.cmd,
                data.DistanceTexture,
                new Vector4(1, 1, 0, 0),
                data.Material,
                data.MaterialPass);
        }
    }
}
