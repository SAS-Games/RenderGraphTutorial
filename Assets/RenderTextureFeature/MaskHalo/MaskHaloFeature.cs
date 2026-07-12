using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class MaskHaloFeature : ScriptableRendererFeature
{
    private const string HaloShaderName = "Hidden/RenderTextureFeature/MaskHalo/Composite";

    [Tooltip("Name used for Render Graph passes and profiler markers.")]
    public string ProfilingName = "Mask Halo";

    [Tooltip("Material that uses Hidden/RenderTextureFeature/MaskHalo/Composite. Assign MaskHaloComposite so the shader is included in builds.")]
    public Material HaloMaterial;

    [Tooltip("Mask source, blur quality, colors, and shape of the halo.")]
    public Settings HaloSettings = new();

    private MaskHaloPass _pass;
    private readonly MaskedEffectMaterialCache _materialCache =
        new(nameof(MaskHaloFeature), HaloShaderName);
    private readonly MaskedEffectItemPool<MaskHaloMaterialSet> _materialSets =
        new(source => new MaskHaloMaterialSet(source));

    [Serializable]
    public sealed class Settings : MaskedEffectLayerSettings
    {
        public Settings()
        {
            Name = "Character Halo";
            MaskTextureName = "_CharacterHaloMask";
        }

        [Tooltip("Resolution divisor for the glow blur. 2 is the recommended balance; 4 is faster and softer.")]
        [Range(1, 4)]
        public int Downsample = 2;

        [Tooltip("Number of Kawase blur passes used to expand the mask. More passes create a wider, smoother aura.")]
        [Range(1, 6)]
        public int BlurIterations = 5;

        [Tooltip("Starting sample offset for each blur pass. Increase this to make the aura extend farther from the silhouette.")]
        [Range(0.5f, 6.0f)]
        public float BlurRadius = 2.5f;

        [Tooltip("Soft transition width above Mask Threshold. Small values preserve a precise silhouette.")]
        [Range(0.0f, 0.5f)]
        public float MaskSoftness = 0.02f;

        [Tooltip("Color of the broad aura farthest from the silhouette. HDR values work with bloom.")]
        [ColorUsage(true, true)]
        public Color OuterGlowColor = new(0.0f, 0.18f, 1.0f, 1.0f);

        [Tooltip("Brightness of the broad aura.")]
        [Range(0.0f, 5.0f)]
        public float OuterGlowIntensity = 1.25f;

        [Tooltip("Falloff curve of the broad aura. Values below 1 keep the distant glow visible for longer.")]
        [Range(0.25f, 2.0f)]
        public float OuterGlowFalloff = 0.65f;

        [Tooltip("Color of the concentrated glow close to the silhouette.")]
        [ColorUsage(true, true)]
        public Color InnerGlowColor = new(0.0f, 0.7f, 1.0f, 1.0f);

        [Tooltip("Brightness of the concentrated glow close to the silhouette.")]
        [Range(0.0f, 5.0f)]
        public float InnerGlowIntensity = 1.8f;

        [Tooltip("How tightly the inner glow hugs the silhouette. Higher values produce a narrower band.")]
        [Range(1.0f, 6.0f)]
        public float InnerGlowTightness = 2.2f;

        [Tooltip("Color of the crisp silhouette rim.")]
        [ColorUsage(true, true)]
        public Color RimColor = new(0.55f, 0.95f, 1.0f, 1.0f);

        [Tooltip("Width of the crisp rim in mask pixels.")]
        [Range(0.5f, 8.0f)]
        public float RimWidth = 3.0f;

        [Tooltip("Brightness of the crisp rim. The rim is composited after the glow so this color remains distinct.")]
        [Range(0.0f, 5.0f)]
        public float RimIntensity = 1.5f;
    }

    public override void Create()
    {
        _pass ??= new MaskHaloPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        HaloSettings ??= new Settings();

        if (!HaloSettings.Enabled ||
            HaloSettings.Opacity <= 0.0001f ||
            string.IsNullOrWhiteSpace(HaloSettings.MaskTextureName) ||
            !_materialCache.Ensure(HaloMaterial))
        {
            return;
        }

        int blurIterations = Mathf.Clamp(HaloSettings.BlurIterations, 1, 6);
        _materialSets.EnsureCount(1, _materialCache.Material, _materialCache.Version);
        _materialSets[0].EnsureBlurMaterialCount(blurIterations, _materialCache.Material);

        _pass.Setup(ProfilingName, HaloSettings, _materialSets[0]);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _materialSets.Dispose();
        _materialCache.Dispose();
    }
}
