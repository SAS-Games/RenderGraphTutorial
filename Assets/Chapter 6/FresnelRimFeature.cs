using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class FresnelRimFeature : ScriptableRendererFeature
{
    private const string ShaderName = "Hidden/Chapter6/SideScrollerFresnelRim";

    [Tooltip("Fresnel shader used as the override material for rimmed objects.")]
    public Shader RimShader;
    public Settings RimSettings = new();

    private FresnelRimPass _pass;
    private Material _material;
    private Shader _materialShader;

    [Serializable]
    public sealed class Settings
    {
        [Tooltip("The rim is added after opaque objects have populated the camera depth buffer.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        [Tooltip("GameObject layers that receive the Fresnel rim.")]
        public LayerMask LayerMask = ~0;

        [ColorUsage(true, true)]
        public Color RimColor = new(1.0f, 0.85f, 0.75f, 1.0f);

        [Range(0.25f, 8.0f)]
        public float RimPower = 3.0f;

        [Range(0.0f, 1.0f)]
        public float RimThreshold = 0.5f;

        [Range(0.001f, 0.5f)]
        public float RimSoftness = 0.1f;

        [Range(0.0f, 10.0f)]
        public float RimIntensity = 2.0f;
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
            context.cmd.DrawRendererList(data.RendererList);
        }
    }
}
