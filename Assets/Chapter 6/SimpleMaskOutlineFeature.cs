using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class SimpleMaskOutlineFeature : ScriptableRendererFeature
{
    private const string ShaderName = "Hidden/RenderTextureFeature/SimpleMaskOutline";

    public Settings OutlineSettings = new();

    private SimpleMaskOutlinePass _pass;
    private Material _material;

    [Serializable]
    public class Settings
    {
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Registered mask texture produced by ObjectsToRenderTextureFeature.")]
        public string MaskTextureName = "_SelectionOutlineMask";

        public Color Color = Color.yellow;

        [Range(1, 8)]
        public int Width = 2;

        [Range(0.0f, 5.0f)]
        public float Intensity = 1.0f;
    }

    public override void Create()
    {
        _pass = new SimpleMaskOutlinePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null)
        {
            _material = CoreUtils.CreateEngineMaterial(ShaderName);
        }

        if (_material == null)
        {
            return;
        }

        _pass.Setup(OutlineSettings, _material);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
    }

    private class SimpleMaskOutlinePass : ScriptableRenderPass
    {
        private static readonly int MaskTextureId = Shader.PropertyToID("_MaskTexture");
        private static readonly int MaskTexelSizeId = Shader.PropertyToID("_MaskTexture_TexelSize");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineIntensityId = Shader.PropertyToID("_OutlineIntensity");

        private Settings _settings;
        private Material _material;
        private readonly FrameTextureResolver _maskResolver = new(nameof(SimpleMaskOutlineFeature));

        private class PassData
        {
            public TextureHandle MaskTexture;
            public Vector4 MaskTexelSize;
            public Material Material;
            public Color OutlineColor;
            public int OutlineWidth;
            public float OutlineIntensity;
        }

        public void Setup(Settings settings, Material material)
        {
            _settings = settings;
            _material = material;
            _maskResolver.SetTextureName(settings.MaskTextureName);
            renderPassEvent = settings.RenderPassEvent;
            profilingSampler = new ProfilingSampler("Simple Mask Outline");
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null ||
                !_maskResolver.TryResolve(frameData, out TextureHandle maskTexture, out Vector4 maskTexelSize))
            {
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                "Simple Mask Outline",
                out PassData passData,
                profilingSampler);

            passData.MaskTexture = maskTexture;
            passData.MaskTexelSize = maskTexelSize;
            passData.Material = _material;
            passData.OutlineColor = _settings.Color;
            passData.OutlineWidth = _settings.Width;
            passData.OutlineIntensity = _settings.Intensity;

            builder.UseTexture(maskTexture, AccessFlags.Read);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
        }

        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            Material material = data.Material;
            material.SetTexture(MaskTextureId, data.MaskTexture);
            material.SetVector(MaskTexelSizeId, data.MaskTexelSize);
            material.SetColor(OutlineColorId, data.OutlineColor);
            material.SetInt(OutlineWidthId, data.OutlineWidth);
            material.SetFloat(OutlineIntensityId, data.OutlineIntensity);

            Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), material, 0);
        }
    }
}
