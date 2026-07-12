using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class JumpFloodDistanceFieldFeature : ScriptableRendererFeature
{
    private const string ShaderName = "Hidden/RenderTextureFeature/JumpFloodDistanceField";

    [Tooltip("Name used for Render Graph passes and profiler markers.")]
    public string ProfilingName = "Jump Flood Distance Field";

    [Tooltip("Material that uses Hidden/RenderTextureFeature/JumpFloodDistanceField. Assign the included material so the shader is included in builds.")]
    public Material JumpFloodMaterial;

    [Tooltip("Input mask, output texture, precision, resolution, and optional debug visualization.")]
    public Settings DistanceFieldSettings = new();

    private JumpFloodDistanceFieldPass _pass;
    private readonly MaskedEffectMaterialCache _materialCache =
        new(nameof(JumpFloodDistanceFieldFeature), ShaderName);
    private readonly MaskedEffectItemPool<JumpFloodMaterialSet> _materialSets =
        new(source => new JumpFloodMaterialSet(source));

    public enum Precision
    {
        Half = 0,
        Float = 1
    }

    public enum DebugMode
    {
        Disabled = 0,
        SignedDistance = 1,
        Contours = 2
    }

    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Disables distance-field generation without removing the renderer feature.")]
        public bool Enabled = true;

        [Tooltip("When the distance field is generated. This must run after the input mask pass.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Mask texture produced by ObjectsToRenderTextureFeature.")]
        public string InputMaskTextureName = "_JfaInputMask";

        [Tooltip("Published signed-distance texture name. Later renderer features and shaders read this exact name.")]
        public string OutputTextureName = "_JfaSignedDistance";

        [Tooltip("Mask value where pixels are considered inside. Distances are negative inside and positive outside.")]
        [Range(0.0f, 1.0f)]
        public float MaskThreshold = 0.5f;

        [Tooltip("Resolution divisor for JFA working and output textures. 1 is full resolution, 2 is half, and 4 is quarter.")]
        [Range(1, 4)]
        public int Downsample = 2;

        [Tooltip("Moves boundary seeds from pixel centers toward the estimated subpixel mask edge. Enable this for smoother outlines and halos; it reuses initialization samples and adds negligible cost.")]
        public bool SubpixelBoundary = true;

        [Tooltip("Additional one-pixel JFA refinement passes after the normal jump sequence. One improves contour stability; use zero on tightly constrained platforms.")]
        [Range(0, 2)]
        public int FinalRefinementPasses = 1;

        [Tooltip("Half uses 16-bit float textures and is recommended for most effects. Float uses 32-bit textures for very large or precision-sensitive fields.")]
        public Precision TexturePrecision = Precision.Half;

        [Tooltip("Maximum absolute distance stored in the output, measured in input-mask pixels. JFA limits its largest jump to the range needed by this value, so smaller distances require fewer passes.")]
        [Range(1.0f, 2048.0f)]
        public float MaxDistancePixels = 256.0f;

        [Tooltip("Optional visualization drawn over the camera after generation. Disable this when another effect consumes the output.")]
        public DebugMode DebugView = DebugMode.Disabled;

        [Tooltip("Distance range represented by the signed-distance debug colors.")]
        [Range(1.0f, 512.0f)]
        public float DebugRangePixels = 64.0f;

        [Tooltip("Pixel spacing between contour lines in Contours debug mode.")]
        [Range(1.0f, 128.0f)]
        public float DebugContourSpacing = 16.0f;

        [Tooltip("Opacity of the fullscreen debug visualization.")]
        [Range(0.0f, 1.0f)]
        public float DebugOpacity = 0.85f;
    }

    public override void Create()
    {
        _pass ??= new JumpFloodDistanceFieldPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        DistanceFieldSettings ??= new Settings();

        if (!DistanceFieldSettings.Enabled ||
            string.IsNullOrWhiteSpace(DistanceFieldSettings.InputMaskTextureName) ||
            string.IsNullOrWhiteSpace(DistanceFieldSettings.OutputTextureName) ||
            !_materialCache.Ensure(JumpFloodMaterial))
        {
            return;
        }

        _materialSets.EnsureCount(1, _materialCache.Material, _materialCache.Version);
        _pass.Setup(ProfilingName, DistanceFieldSettings, _materialSets[0]);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _materialSets.Dispose();
        _materialCache.Dispose();
    }
}
