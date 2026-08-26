using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class FresnelRimFeature : ScriptableRendererFeature
{
    private const string ShaderName = "Hidden/Chapter6/FresnelRim";

    [Tooltip("Override shader used to draw the rim. When unassigned, the feature finds Hidden/Chapter6/FresnelRim. Pass 0 must support the rim properties below.")]
    public Shader RimShader;

    [Tooltip("Controls which objects receive the rim and how its Fresnel shape, planar direction, and additive color are evaluated.")]
    public Settings RimSettings = new();

    private FresnelRimPass _pass;
    private Material _material;
    private Shader _materialShader;

    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Controls when the rim is rendered. After Rendering Opaques is recommended because the pass depth-tests against opaque geometry.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        [Tooltip("GameObject layers that receive the rim. The feature only draws renderers in the opaque render queue; transparent renderers are excluded even when their layer is selected.")]
        public LayerMask LayerMask = ~0;

        [Tooltip("HDR additive rim tint. RGB controls the tint, while alpha multiplies brightness together with Rim Intensity; alpha does not behave as transparency in this additive pass.")]
        [ColorUsage(true, true)]
        public Color RimColor = new(1.0f, 0.85f, 0.75f, 1.0f);

        [InspectorName("Rim Tightness")]
        [Tooltip("Controls the Fresnel falloff before the planar-facing influence and threshold. Higher values produce a thinner, tighter rim; lower values produce a wider rim. Rim Threshold can tighten it further.")]
        [Range(0.25f, 8.0f)]
        public float RimPower = 3.0f;

        [Tooltip("Cuts off the gated Fresnel value. Higher values reveal less rim; lower values reveal more. Rim Softness controls how gradually this cutoff is crossed.")]
        [Range(0.0f, 1.0f)]
        public float RimThreshold = 0.5f;

        [Tooltip("Half-width of the transition around Rim Threshold. Higher values produce a softer, broader transition; lower values produce a sharper edge. The transition spans Threshold minus Softness to Threshold plus Softness.")]
        [Range(0.001f, 0.5f)]
        public float RimSoftness = 0.1f;

        [Tooltip("Scales brightness after thresholding and softness. It does not change the mathematical rim width, although additive HDR brightness can make the rim appear wider. Zero disables the render pass.")]
        [Range(0.0f, 10.0f)]
        public float RimIntensity = 2.0f;

        [Tooltip("Blends the camera Z position toward Planar Plane Z for the directional mask. Zero uses the real camera position; one uses a fully planar direction. It has no visible effect when Planar Facing Influence is zero.")]
        [Range(0.0f, 1.0f)]
        public float PlanarProjection = 1.0f;

        [Tooltip("World-space Z coordinate used when projecting the camera for the directional gate. Match this to the gameplay plane. Its influence increases with Planar Projection and disappears when projection is zero.")]
        public float PlanarPlaneZ = 0.0f;

        [InspectorName("Planar Facing Influence")]
        [Tooltip("Blends between standard Fresnel and the planar-facing mask before Rim Threshold. Zero disables the directional mask; one applies it fully. Increasing this usually removes rim from surfaces facing away from the projected camera.")]
        [Range(0.0f, 1.0f)]
        public float PlanarGateStrength = 1.0f;
    }

    public override void Create()
    {
        _pass ??= new FresnelRimPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        RimSettings ??= new Settings();
        if (RimSettings.RimIntensity <= 0.0f || !EnsureMaterial())
            return;

        _pass.Setup(RimSettings, _material);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
        _material = null;
        _materialShader = null;
    }

    private bool EnsureMaterial()
    {
        Shader shader = RimShader != null ? RimShader : Shader.Find(ShaderName);
        if (shader == null)
            return false;

        if (_material != null && _materialShader == shader)
            return true;

        CoreUtils.Destroy(_material);
        _material = CoreUtils.CreateEngineMaterial(shader);
        _materialShader = shader;
        return _material != null;
    }

    private sealed class FresnelRimPass : ScriptableRenderPass
    {
        private const int RimPassIndex = 0;

        private static readonly List<ShaderTagId> ShaderTagIds = new()
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("LightweightForward")
        };

        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
        private static readonly int RimThresholdId = Shader.PropertyToID("_RimThreshold");
        private static readonly int RimSoftnessId = Shader.PropertyToID("_RimSoftness");
        private static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
        private static readonly int PlanarProjectionId = Shader.PropertyToID("_PlanarProjection");
        private static readonly int PlanarPlaneZId = Shader.PropertyToID("_PlanarPlaneZ");
        private static readonly int PlanarGateStrengthId = Shader.PropertyToID("_PlanarGateStrength");

        private readonly ProfilingSampler _profilingSampler = new("Fresnel Rim");
        private Settings _settings;
        private Material _material;

        private sealed class PassData
        {
            public RendererListHandle RendererList;
            public Material Material;
            public Color RimColor;
            public float RimPower;
            public float RimThreshold;
            public float RimSoftness;
            public float RimIntensity;
            public float PlanarProjection;
            public float PlanarPlaneZ;
            public float PlanarGateStrength;
        }

        public void Setup(Settings settings, Material material)
        {
            _settings = settings;
            _material = material;
            renderPassEvent = settings.RenderPassEvent;
            profilingSampler = _profilingSampler;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null || _settings == null)
                return;

            RendererListHandle rendererList = CreateRendererList(renderGraph, frameData);
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                "Fresnel Rim",
                out PassData passData,
                profilingSampler
            );

            passData.RendererList = rendererList;
            passData.Material = _material;
            passData.RimColor = _settings.RimColor;
            passData.RimPower = Mathf.Max(0.001f, _settings.RimPower);
            passData.RimThreshold = Mathf.Clamp01(_settings.RimThreshold);
            passData.RimSoftness = Mathf.Max(0.001f, _settings.RimSoftness);
            passData.RimIntensity = Mathf.Max(0.0f, _settings.RimIntensity);
            passData.PlanarProjection = Mathf.Clamp01(_settings.PlanarProjection);
            passData.PlanarPlaneZ = _settings.PlanarPlaneZ;
            passData.PlanarGateStrength = Mathf.Clamp01(_settings.PlanarGateStrength);

            builder.UseRendererList(rendererList);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
        }

        private RendererListHandle CreateRendererList(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                ShaderTagIds,
                renderingData,
                cameraData,
                lightData,
                SortingCriteria.CommonOpaque
            );

            drawingSettings.overrideMaterial = _material;
            drawingSettings.overrideMaterialPassIndex = RimPassIndex;

            FilteringSettings filteringSettings = new(
                RenderQueueRange.opaque,
                _settings.LayerMask
            );

            RendererListParams rendererListParams = new(
                renderingData.cullResults,
                drawingSettings,
                filteringSettings
            );

            return renderGraph.CreateRendererList(rendererListParams);
        }

        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            data.Material.SetColor(RimColorId, data.RimColor);
            data.Material.SetFloat(RimPowerId, data.RimPower);
            data.Material.SetFloat(RimThresholdId, data.RimThreshold);
            data.Material.SetFloat(RimSoftnessId, data.RimSoftness);
            data.Material.SetFloat(RimIntensityId, data.RimIntensity);
            data.Material.SetFloat(PlanarProjectionId, data.PlanarProjection);
            data.Material.SetFloat(PlanarPlaneZId, data.PlanarPlaneZ);
            data.Material.SetFloat(PlanarGateStrengthId, data.PlanarGateStrength);
            context.cmd.DrawRendererList(data.RendererList);
        }
    }
}
