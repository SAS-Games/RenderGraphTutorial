using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class ThermalVisionFeature : ScriptableRendererFeature
{
    private const string CompositeShaderName = "Hidden/Chapter16/ThermalVision/Composite";

    private static float runtimeIntensity;
    private static bool runtimeThroughWalls;

    [Tooltip("Name used for Render Graph passes and profiler markers.")]
    public string ProfilingName = "Thermal Vision";

    [Tooltip("Material that uses Hidden/Chapter16/ThermalVision/Composite.")]
    public Material CompositeMaterial;

    [Tooltip("Thermal palette, mask sources, and presentation settings.")]
    public Settings ThermalSettings = new();

    private FrameColorSnapshotPass sourceSnapshotPass;
    private ThermalVisionPass thermalPass;
    private readonly MaskedEffectMaterialCache materialCache =
        new(nameof(ThermalVisionFeature), CompositeShaderName);

    internal static float RuntimeIntensity => runtimeIntensity;
    internal static bool RuntimeThroughWalls => runtimeThroughWalls;

    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Disables every thermal pass without removing the renderer feature.")]
        public bool Enabled = true;

        [Tooltip("When the thermal composite runs. Both masks must be produced before this event.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Depth-tested heat mask used by the physically plausible mode.")]
        public string VisibleMaskTextureName = "_ThermalVisibleMask";

        [Tooltip("Depth-independent heat mask used by the optional game-style mode.")]
        public string ThroughWallMaskTextureName = "_ThermalThroughWallMask";

        [Tooltip("Depth-tested mask whose pixels keep the original camera color, used to exclude the playable character from thermal recoloring.")]
        public string PlayerMaskTextureName = "_ThermalPlayerMask";

        [Tooltip("Preview strength outside Play mode. Runtime strength is controlled by ThermalVisionDemoController.")]
        [Range(0f, 1f)]
        public float EditorPreviewIntensity;

        [Tooltip("Selects the depth-independent mask outside Play mode. Runtime mode is controlled by ThermalVisionDemoController.")]
        public bool PreviewThroughWalls;

        [Tooltip("Overall strength multiplied by the controller's animated activation.")]
        [Range(0f, 1f)]
        public float Opacity = 1f;

        [Header("Environment Palette")]
        [ColorUsage(true, true)] public Color CoolShadow = new(0.005f, 0.01f, 0.055f, 1f);
        [ColorUsage(true, true)] public Color CoolMid = new(0.015f, 0.12f, 0.32f, 1f);
        [ColorUsage(true, true)] public Color CoolHighlight = new(0.08f, 0.65f, 0.8f, 1f);

        [Tooltip("Contrast applied to scene luminance before evaluating the cool palette.")]
        [Range(0.25f, 3f)]
        public float EnvironmentContrast = 1.25f;

        [Header("Heat Palette")]
        [ColorUsage(true, true)] public Color ColdHeat = new(0.2f, 0.0f, 0.35f, 1f);
        [ColorUsage(true, true)] public Color WarmHeat = new(1.0f, 0.05f, 0.0f, 1f);
        [ColorUsage(true, true)] public Color HotHeat = new(1.5f, 0.55f, 0.0f, 1f);
        [ColorUsage(true, true)] public Color CoreHeat = new(2.2f, 2.0f, 1.2f, 1f);

        [Tooltip("Mask value where a heat source begins.")]
        [Range(0f, 1f)]
        public float MaskThreshold = 0.05f;

        [Tooltip("Softens the transition at heat-source silhouette edges.")]
        [Range(0f, 0.5f)]
        public float MaskSoftness = 0.04f;

        [Tooltip("Mixes visible surface luminance into the simulated temperature signal.")]
        [Range(0f, 1f)]
        public float SurfaceDetail = 0.22f;

        [Tooltip("Brightness of the thin hot edge around a detected heat source.")]
        [Range(0f, 3f)]
        public float EdgeIntensity = 0.7f;

        [Header("Sensor Character")]
        [Tooltip("Strength of fine animated sensor noise.")]
        [Range(0f, 0.25f)]
        public float NoiseStrength = 0.025f;

        [Tooltip("Strength of horizontal sensor scan lines.")]
        [Range(0f, 0.25f)]
        public float ScanlineStrength = 0.035f;

        [Tooltip("Number of horizontal scan lines across the screen.")]
        [Range(20f, 600f)]
        public float ScanlineFrequency = 220f;
    }

    public static void SetRuntimeState(float intensity, bool throughWalls)
    {
        runtimeIntensity = Mathf.Clamp01(intensity);
        runtimeThroughWalls = throughWalls;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        runtimeIntensity = 0f;
        runtimeThroughWalls = false;
    }

    public override void Create()
    {
        sourceSnapshotPass ??= new FrameColorSnapshotPass();
        thermalPass ??= new ThermalVisionPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        ThermalSettings ??= new Settings();

        if (!ThermalSettings.Enabled ||
            ThermalSettings.Opacity <= 0.0001f ||
            string.IsNullOrWhiteSpace(ThermalSettings.VisibleMaskTextureName) ||
            string.IsNullOrWhiteSpace(ThermalSettings.ThroughWallMaskTextureName) ||
            string.IsNullOrWhiteSpace(ThermalSettings.PlayerMaskTextureName) ||
            !materialCache.Ensure(CompositeMaterial))
        {
            return;
        }

        float intensity = Application.isPlaying
            ? RuntimeIntensity
            : Mathf.Clamp01(ThermalSettings.EditorPreviewIntensity);

        if (intensity <= 0.0001f)
            return;

        bool throughWalls = Application.isPlaying
            ? RuntimeThroughWalls
            : ThermalSettings.PreviewThroughWalls;

        sourceSnapshotPass.Setup($"{ProfilingName} Source", ThermalSettings.RenderPassEvent);
        thermalPass.Setup(
            ProfilingName,
            ThermalSettings,
            materialCache.Material,
            sourceSnapshotPass.SnapshotTextureId,
            intensity,
            throughWalls);

        renderer.EnqueuePass(sourceSnapshotPass);
        renderer.EnqueuePass(thermalPass);
    }

    protected override void Dispose(bool disposing)
    {
        materialCache.Dispose();
    }
}
