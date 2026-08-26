using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class MaskFresnelRimFeature : ScriptableRendererFeature
{
    private const string CompositeShaderName = "Hidden/RenderTextureFeature/MaskFresnelRim/Composite";

    [Tooltip("Name used for the Render Graph pass and profiler marker.")]
    public string ProfilingName = "Mask Fresnel Rim";
    [Tooltip("Material that calculates Fresnel from camera normals and limits it with a mask.")]
    public Material CompositeMaterial;
    
    public Settings RimSettings = new();
    private MaskFresnelRimPass _pass;
    private readonly MaskedEffectMaterialCache _materialCache = new(nameof(MaskFresnelRimFeature), CompositeShaderName);

    [Serializable]
    public sealed class Settings
    {
        public enum DebugView
        {
            FinalRim = 0,
            SelectionMask = 1,
            SmoothedNormal = 2,
            StandardFresnel = 3,
            PlanarFacing = 4,
            GatedFresnel = 5,
            ThresholdedRim = 6
        }

        [Tooltip("When the rim is composited. This must run after the mask texture is produced.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Texture name produced by RenderObjectsToTextureFeature.")]
        public string MaskTextureName = "_SelectionOutlineMask";

        [InspectorName("Debug View")]
        [Tooltip("Selects the final composite or an intermediate value. Debug views replace the scene with an opaque visualization; switch back to Final Rim for additive rendering.")]
        public DebugView DebugOutput = DebugView.FinalRim;

        [Tooltip("HDR additive rim tint. RGB controls the tint, while alpha multiplies brightness together with Rim Intensity; alpha does not behave as transparency in this additive pass.")]
        [ColorUsage(true, true)]
        public Color RimColor = new(1.0f, 0.85f, 0.75f, 1.0f);

        [InspectorName("Rim Tightness")]
        [Tooltip("Controls the standard Fresnel falloff before the planar-facing influence and threshold. Higher values produce a thinner, tighter rim; lower values produce a wider rim.")]
        [Range(0.25f, 8.0f)]
        public float RimPower = 3.0f;

        [Tooltip("Cuts off the gated Fresnel value. Higher values reveal less rim; lower values reveal more. Rim Softness controls how gradually this cutoff is crossed.")]
        [Range(0.0f, 1.0f)]
        public float RimThreshold = 0.4f;

        [Tooltip("Half-width of the transition around Rim Threshold. Higher values produce a softer transition; lower values produce a sharper edge.")]
        [Range(0.001f, 0.5f)]
        public float RimSoftness = 0.12f;

        [Tooltip("Scales brightness after thresholding. It does not change the mathematical rim width, although additive HDR brightness can make the rim appear wider. Zero disables the pass.")]
        [Range(0.0f, 10.0f)]
        public float RimIntensity = 1.5f;

        [Tooltip("Blends the camera Z position toward Planar Plane Z for the directional mask. Zero uses the real camera position; one uses a fully planar direction. It has no visible effect when Planar Facing Influence is zero.")]
        [Range(0.0f, 1.0f)]
        public float PlanarProjection = 1.0f;

        [Tooltip("World-space Z coordinate used when projecting the camera for the directional gate. Match this to the gameplay plane. Its influence increases with Planar Projection.")]
        public float PlanarPlaneZ = 0.0f;

        [InspectorName("Planar Facing Influence")]
        [Tooltip("Blends between standard Fresnel and the planar-facing mask before Rim Threshold. Zero disables the directional mask; one applies it fully.")]
        [Range(0.0f, 1.0f)]
        public float PlanarGateStrength = 1.0f;

        [Tooltip("Mask value where a pixel belongs to the selected object.")] [Range(0.0f, 1.0f)]
        public float MaskThreshold = 0.5f;

        [Tooltip("Smooths the selection boundary before applying the Fresnel result.")] [Range(0.001f, 0.25f)]
        public float MaskEdgeSoftness = 0.1f;

        [Tooltip("Screen-space normal smoothing radius in pixels. Increase slightly if animated normals shimmer.")]
        [Range(0.0f, 2.0f)]
        public float NormalSmoothing = 0.75f;
    }

    public override void Create()
    {
        _pass ??= new MaskFresnelRimPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        RimSettings ??= new Settings();

        bool finalRimDisabled = RimSettings.DebugOutput == Settings.DebugView.FinalRim &&
                                RimSettings.RimIntensity <= 0.0001f;

        if (string.IsNullOrWhiteSpace(RimSettings.MaskTextureName) || finalRimDisabled ||
            !_materialCache.Ensure(CompositeMaterial))
            return;

        _pass.Setup(ProfilingName, RimSettings, _materialCache.Material);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _materialCache.Dispose();
    }
}

internal sealed class MaskFresnelRimPass : ScriptableRenderPass
{
    private static readonly int CameraNormalsTextureId = Shader.PropertyToID("_CameraNormalsTexture");
    private static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
    private static readonly int MaskTexelSizeId = Shader.PropertyToID("_MaskTexelSize");

    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int RimThresholdId = Shader.PropertyToID("_RimThreshold");
    private static readonly int RimSoftnessId = Shader.PropertyToID("_RimSoftness");
    private static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
    private static readonly int PlanarProjectionId = Shader.PropertyToID("_PlanarProjection");
    private static readonly int PlanarPlaneZId = Shader.PropertyToID("_PlanarPlaneZ");
    private static readonly int PlanarGateStrengthId = Shader.PropertyToID("_PlanarGateStrength");
    private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");

    private static readonly int MaskEdgeSoftnessId = Shader.PropertyToID("_MaskEdgeSoftness");
    private static readonly int NormalSmoothingId = Shader.PropertyToID("_NormalSmoothing");
    private static readonly int DebugViewId = Shader.PropertyToID("_DebugView");
    private static readonly int DestinationBlendId = Shader.PropertyToID("_DestinationBlend");
    private readonly MaskedTextureResolver _maskResolver = new(nameof(MaskFresnelRimFeature));

    private string _profilingName;
    private MaskFresnelRimFeature.Settings _settings;
    private Material _material;
    private bool _loggedMissingCameraTextures;

    private sealed class PassData
    {
        public TextureHandle MaskTexture;
        public TextureHandle NormalsTexture;
        public TextureHandle DepthTexture;
        public Material Material;
        public Vector4 MaskTexelSize;
        public Color RimColor;
        public float RimPower;
        public float RimThreshold;
        public float RimSoftness;
        public float RimIntensity;
        public float PlanarProjection;
        public float PlanarPlaneZ;
        public float PlanarGateStrength;
        public float MaskThreshold;
        public float MaskEdgeSoftness;
        public float NormalSmoothing;
        public MaskFresnelRimFeature.Settings.DebugView DebugView;
    }

    public MaskFresnelRimPass()
    {
        ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
    }

    public void Setup(string profilingName, MaskFresnelRimFeature.Settings settings, Material material)
    {
        _settings = settings;
        _material = material;
        _maskResolver.SetTextureName(settings.MaskTextureName);
        renderPassEvent = settings.RenderPassEvent;
        profilingSampler =
            MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(profilingName, ref _profilingName,
                profilingSampler);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_settings == null || _material == null)
            return;

        if (!_maskResolver.TryResolve(frameData, out TextureHandle maskTexture, out Vector4 maskTexelSize))
            return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        TextureHandle normalsTexture = resourceData.cameraNormalsTexture;
        TextureHandle depthTexture = resourceData.cameraDepthTexture;

        if (!normalsTexture.IsValid() || !depthTexture.IsValid())
        {
            if (!_loggedMissingCameraTextures)
            {
                Debug.LogWarning($"{nameof(MaskFresnelRimFeature)} requires camera normals and depth.");
                _loggedMissingCameraTextures = true;
            }

            return;
        }

        _loggedMissingCameraTextures = false;

        using IRasterRenderGraphBuilder builder =
            renderGraph.AddRasterRenderPass(_profilingName, out PassData passData, profilingSampler);

        passData.MaskTexture = maskTexture;
        passData.NormalsTexture = normalsTexture;
        passData.DepthTexture = depthTexture;
        passData.Material = _material;
        passData.MaskTexelSize = maskTexelSize;
        passData.RimColor = _settings.RimColor;
        passData.RimPower = Mathf.Max(0.001f, _settings.RimPower);
        passData.RimThreshold = Mathf.Clamp01(_settings.RimThreshold);
        passData.RimSoftness = Mathf.Clamp(_settings.RimSoftness, 0.001f, 0.5f);
        passData.RimIntensity = Mathf.Max(0.0f, _settings.RimIntensity);
        passData.PlanarProjection = Mathf.Clamp01(_settings.PlanarProjection);
        passData.PlanarPlaneZ = _settings.PlanarPlaneZ;
        passData.PlanarGateStrength = Mathf.Clamp01(_settings.PlanarGateStrength);
        passData.MaskThreshold = Mathf.Clamp01(_settings.MaskThreshold);
        passData.MaskEdgeSoftness = Mathf.Clamp(_settings.MaskEdgeSoftness, 0.001f, 0.25f);
        passData.NormalSmoothing = Mathf.Clamp(_settings.NormalSmoothing, 0.0f, 2.0f);
        passData.DebugView = _settings.DebugOutput;

        builder.UseTexture(maskTexture, AccessFlags.Read);
        builder.UseTexture(normalsTexture, AccessFlags.Read);
        builder.UseTexture(depthTexture, AccessFlags.Read);
        builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
        builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
    }

    private static void ExecutePass(PassData data, RasterGraphContext context)
    {
        data.Material.SetTexture(CameraNormalsTextureId, data.NormalsTexture);
        data.Material.SetTexture(CameraDepthTextureId, data.DepthTexture);
        data.Material.SetVector(MaskTexelSizeId, data.MaskTexelSize);
        data.Material.SetColor(RimColorId, data.RimColor);
        data.Material.SetFloat(RimPowerId, data.RimPower);
        data.Material.SetFloat(RimThresholdId, data.RimThreshold);
        data.Material.SetFloat(RimSoftnessId, data.RimSoftness);
        data.Material.SetFloat(RimIntensityId, data.RimIntensity);
        data.Material.SetFloat(PlanarProjectionId, data.PlanarProjection);
        data.Material.SetFloat(PlanarPlaneZId, data.PlanarPlaneZ);
        data.Material.SetFloat(PlanarGateStrengthId, data.PlanarGateStrength);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(MaskEdgeSoftnessId, data.MaskEdgeSoftness);
        data.Material.SetFloat(NormalSmoothingId, data.NormalSmoothing);
        data.Material.SetFloat(DebugViewId, (float)data.DebugView);
        data.Material.SetFloat(
            DestinationBlendId,
            data.DebugView == MaskFresnelRimFeature.Settings.DebugView.FinalRim
                ? (float)BlendMode.One
                : (float)BlendMode.Zero
        );

        Blitter.BlitTexture(context.cmd, data.MaskTexture, new Vector4(1, 1, 0, 0), data.Material, 0);
    }
}
