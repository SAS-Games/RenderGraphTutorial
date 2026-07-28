using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class SimpleInvertedHullOutlineFeature : ScriptableRendererFeature
{
    private const string ShaderName = "Hidden/Chapter6/SimpleOutline";

    [Tooltip("Inverted-hull shader used as the override material for outlined objects.")]
    public Shader OutlineShader;
    public Settings OutlineSettings = new();

    private SimpleOutlinePass _pass;
    private Material _material;
    private Shader _materialShader;

    [Serializable]
    public sealed class Settings
    {
        [Tooltip("The outline is drawn before normal opaque objects so their original materials cover the inner shell.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.BeforeRenderingOpaques;

        [Tooltip("GameObject layers that receive the outline.")]
        public LayerMask LayerMask = ~0;

        [ColorUsage(true, true)]
        public Color Color = Color.yellow;

        [Tooltip("Object-space distance used to expand the outline shell along vertex normals.")]
        [Range(0.001f, 0.5f)]
        public float Width = 0.05f;
    }

    public override void Create()
    {
        _pass ??= new SimpleOutlinePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        OutlineSettings ??= new Settings();
        if (OutlineSettings.Width <= 0.0f || !EnsureMaterial())
            return;

        _pass.Setup(OutlineSettings, _material);
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
        Shader shader = OutlineShader != null ? OutlineShader : Shader.Find(ShaderName);
        if (shader == null)
            return false;

        if (_material != null && _materialShader == shader)
            return true;

        CoreUtils.Destroy(_material);
        _material = CoreUtils.CreateEngineMaterial(shader);
        _materialShader = shader;
        return _material != null;
    }

    private sealed class SimpleOutlinePass : ScriptableRenderPass
    {
        private const int OutlinePassIndex = 0;

        private static readonly List<ShaderTagId> ShaderTagIds = new()
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("LightweightForward")
        };

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private Settings _settings;
        private Material _material;
        private readonly ProfilingSampler _profilingSampler = new("Simple Outline");

        private sealed class PassData
        {
            public RendererListHandle RendererList;
            public Material Material;
            public Color OutlineColor;
            public float OutlineWidth;
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
            {
                return;
            }

            RendererListHandle rendererList = CreateRendererList(renderGraph, frameData);
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Simple Outline", out PassData passData, profilingSampler);

            passData.RendererList = rendererList;
            passData.Material = _material;
            passData.OutlineColor = _settings.Color;
            passData.OutlineWidth = Mathf.Max(0.0f, _settings.Width);

            builder.UseRendererList(rendererList);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
        }

        private RendererListHandle CreateRendererList(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(ShaderTagIds, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);

            drawingSettings.overrideMaterial = _material;
            drawingSettings.overrideMaterialPassIndex = OutlinePassIndex;

            FilteringSettings filteringSettings = new(RenderQueueRange.opaque, _settings.LayerMask);
            RendererListParams rendererListParams = new(renderingData.cullResults, drawingSettings, filteringSettings);

            return renderGraph.CreateRendererList(rendererListParams);
        }

        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            context.cmd.DrawRendererList(data.RendererList);
            data.Material.SetColor(OutlineColorId, data.OutlineColor);
            data.Material.SetFloat(OutlineWidthId, data.OutlineWidth);
        }
    }
}
