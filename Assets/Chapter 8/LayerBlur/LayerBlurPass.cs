using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

internal sealed class LayerBlurMaterialSet : IDisposable
{
    public LayerBlurMaterialSet(Material source)
    {
        Horizontal = new MaskedEffectMaterialInstance(source);
        Vertical = new MaskedEffectMaterialInstance(source);
        Composite = new MaskedEffectMaterialInstance(source);
    }

    public MaskedEffectMaterialInstance Horizontal { get; private set; }
    public MaskedEffectMaterialInstance Vertical { get; private set; }
    public MaskedEffectMaterialInstance Composite { get; private set; }

    public bool IsValid =>
        Horizontal != null && Horizontal.IsValid &&
        Vertical != null && Vertical.IsValid &&
        Composite != null && Composite.IsValid;

    public void Dispose()
    {
        Horizontal?.Dispose();
        Vertical?.Dispose();
        Composite?.Dispose();
        Horizontal = null;
        Vertical = null;
        Composite = null;
    }
}

internal sealed class LayerBlurPass : ScriptableRenderPass
{
    private const int HorizontalBlurPassIndex = 0;
    private const int VerticalBlurPassIndex = 1;
    private const int CompositePassIndex = 2;

    private static readonly int LayerBlurredTextureId = Shader.PropertyToID("_LayerBlurredTexture");
    private static readonly int LayerBlurSourceTextureId = Shader.PropertyToID("_LayerBlurSourceTexture");
    private static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");
    private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
    private static readonly int BlurTexelSizeId = Shader.PropertyToID("_BlurTexelSize");
    private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");
    private static readonly int MaskSoftnessId = Shader.PropertyToID("_MaskSoftness");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    private readonly List<LayerRuntime> _layers = new();
    private readonly List<BlurGroup> _blurGroups = new();
    private string _profilingName;
    private int _sourceTextureId;
    private int _activeLayerCount;
    private int _activeBlurGroupCount;

    private sealed class LayerRuntime
    {
        public readonly MaskedTextureResolver MaskResolver = new(nameof(LayerBlurFeature));
        public string Name;
        public LayerBlurFeature.Settings Settings;
        public LayerBlurMaterialSet Materials;
        public TextureHandle MaskTexture;
        public TextureHandle BlurredTexture;
        public int BlurGroupIndex;
        public bool HasMask;

        public void Setup(string name, LayerBlurFeature.Settings settings, LayerBlurMaterialSet materials)
        {
            Name = name;
            Settings = settings;
            Materials = materials;
            MaskResolver.SetTextureName(settings.MaskTextureName);
            MaskTexture = TextureHandle.nullHandle;
            BlurredTexture = TextureHandle.nullHandle;
            BlurGroupIndex = -1;
            HasMask = false;
        }
    }

    private sealed class BlurGroup
    {
        public int Downsample;
        public float Radius;
        public int MaxIterations;
        public LayerBlurMaterialSet Materials;

        public void Setup(int downsample, float radius, LayerBlurMaterialSet materials)
        {
            Downsample = downsample;
            Radius = radius;
            MaxIterations = 1;
            Materials = materials;
        }
    }

    private sealed class BlurPassData
    {
        public TextureHandle Source;
        public Material Material;
        public Vector4 Direction;
        public Vector4 TexelSize;
        public float Radius;
        public int PassIndex;
    }

    private sealed class CompositePassData
    {
        public TextureHandle SourceTexture;
        public TextureHandle BlurredTexture;
        public TextureHandle MaskTexture;
        public Material Material;
        public float MaskThreshold;
        public float MaskSoftness;
        public float Opacity;
    }

    public void Setup(string profilingName, RenderPassEvent passEvent, int sourceTextureId)
    {
        _activeLayerCount = 0;
        _sourceTextureId = sourceTextureId;
        renderPassEvent = passEvent;
        profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(profilingName, ref _profilingName, profilingSampler);
    }

    public void AddLayer(string name, LayerBlurFeature.Settings settings, LayerBlurMaterialSet materials)
    {
        while (_layers.Count <= _activeLayerCount)
        {
            _layers.Add(new LayerRuntime());
        }

        _layers[_activeLayerCount].Setup(name, settings, materials);
        _activeLayerCount++;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_activeLayerCount == 0)
        {
            return;
        }

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        TextureHandle sourceColor = GetSourceColor(frameData, resourceData);

        BuildBlurGroups(frameData, sourceColor);
        RecordBlurGroups(renderGraph, cameraData.cameraTargetDescriptor, sourceColor);

        for (int i = 0; i < _activeLayerCount; i++)
        {
            LayerRuntime layer = _layers[i];
            if (!layer.HasMask || layer.Materials == null || !layer.Materials.IsValid)
            {
                continue;
            }

            AddCompositePass(renderGraph, $"{layer.Name} Composite", resourceData.activeColorTexture, sourceColor, layer.BlurredTexture, layer.MaskTexture, layer.Settings, layer.Materials.Composite.Material);
        }
    }

    private void BuildBlurGroups(ContextContainer frameData, TextureHandle sourceColor)
    {
        _activeBlurGroupCount = 0;

        for (int i = 0; i < _activeLayerCount; i++)
        {
            LayerRuntime layer = _layers[i];
            layer.HasMask = layer.MaskResolver.TryResolve(frameData, out TextureHandle maskTexture, out _);
            layer.MaskTexture = maskTexture;
            layer.BlurredTexture = sourceColor;
            layer.BlurGroupIndex = -1;

            if (!layer.HasMask || layer.Materials == null || !layer.Materials.IsValid)
                continue;

            float radius = Mathf.Clamp(layer.Settings.BlurRadius, 0.0f, 8.0f);
            if (radius <= 0.0001f || layer.Settings.Opacity <= 0.0001f)
                continue;

            int downsample = Mathf.Clamp(layer.Settings.Downsample, 1, 4);
            int groupIndex = FindOrCreateBlurGroup(downsample, radius, layer.Materials);
            BlurGroup group = _blurGroups[groupIndex];
            group.MaxIterations = Mathf.Max(group.MaxIterations, Mathf.Clamp(layer.Settings.Iterations, 1, 4));
            layer.BlurGroupIndex = groupIndex;
        }
    }

    private int FindOrCreateBlurGroup(int downsample, float radius, LayerBlurMaterialSet materials)
    {
        for (int i = 0; i < _activeBlurGroupCount; i++)
        {
            BlurGroup group = _blurGroups[i];
            if (group.Downsample == downsample && Mathf.Approximately(group.Radius, radius))
                return i;
        }

        while (_blurGroups.Count <= _activeBlurGroupCount)
        {
            _blurGroups.Add(new BlurGroup());
        }

        _blurGroups[_activeBlurGroupCount].Setup(downsample, radius, materials);
        _activeBlurGroupCount++;
        return _activeBlurGroupCount - 1;
    }

    private void RecordBlurGroups(RenderGraph renderGraph, RenderTextureDescriptor cameraDescriptor, TextureHandle sourceColor)
    {
        for (int groupIndex = 0; groupIndex < _activeBlurGroupCount; groupIndex++)
        {
            BlurGroup group = _blurGroups[groupIndex];
            RenderTextureDescriptor descriptor = CreateBlurDescriptor(cameraDescriptor, group.Downsample);
            Vector4 texelSize = MaskedEffectRenderGraphUtility.CreateTexelSize(descriptor.width, descriptor.height);
            TextureHandle ping = CreateBlurTexture(renderGraph, descriptor, groupIndex, "Ping");
            TextureHandle currentSource = sourceColor;

            for (int iteration = 1; iteration <= group.MaxIterations; iteration++)
            {
                TextureHandle result = CreateBlurTexture(renderGraph, descriptor, groupIndex, $"Level {iteration}");

                AddBlurPass(renderGraph, $"{_profilingName} Chain {groupIndex + 1} Horizontal {iteration}",
                    currentSource, ping, group.Materials.Horizontal.Material, Vector2.right,
                    texelSize, group.Radius, HorizontalBlurPassIndex);

                AddBlurPass(renderGraph, $"{_profilingName} Chain {groupIndex + 1} Vertical {iteration}",
                    ping, result, group.Materials.Vertical.Material, Vector2.up,
                    texelSize, group.Radius, VerticalBlurPassIndex);

                AssignBlurResult(groupIndex, iteration, result);
                currentSource = result;
            }
        }
    }

    private void AssignBlurResult(int groupIndex, int iteration, TextureHandle result)
    {
        for (int i = 0; i < _activeLayerCount; i++)
        {
            LayerRuntime layer = _layers[i];
            if (layer.BlurGroupIndex == groupIndex && Mathf.Clamp(layer.Settings.Iterations, 1, 4) == iteration)
                layer.BlurredTexture = result;
        }
    }

    private TextureHandle CreateBlurTexture(RenderGraph renderGraph, RenderTextureDescriptor descriptor, int groupIndex, string suffix)
    {
        return UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor,
            $"{_profilingName} Chain {groupIndex + 1} {suffix}",
            false, FilterMode.Bilinear, TextureWrapMode.Clamp);
    }

    private void AddBlurPass(RenderGraph renderGraph, string passName, TextureHandle source, TextureHandle destination, Material material, Vector2 direction, Vector4 texelSize, float radius, int passIndex)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(passName, out BlurPassData passData, profilingSampler);

        passData.Source = source;
        passData.Material = material;
        passData.Direction = new Vector4(direction.x, direction.y, 0.0f, 0.0f);
        passData.TexelSize = texelSize;
        passData.Radius = radius;
        passData.PassIndex = passIndex;

        builder.UseTexture(source, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0);
        builder.SetRenderFunc((BlurPassData data, RasterGraphContext context) => ExecuteBlurPass(data, context));
    }

    private void AddCompositePass(RenderGraph renderGraph, string passName, TextureHandle activeColor, TextureHandle sourceTexture, TextureHandle blurredTexture, TextureHandle maskTexture, LayerBlurFeature.Settings settings, Material material)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(passName, out CompositePassData passData, profilingSampler);

        passData.SourceTexture = sourceTexture;
        passData.BlurredTexture = blurredTexture;
        passData.MaskTexture = maskTexture;
        passData.Material = material;
        passData.MaskThreshold = Mathf.Clamp01(settings.MaskThreshold);
        passData.MaskSoftness = Mathf.Clamp01(settings.MaskSoftness);
        passData.Opacity = Mathf.Clamp01(settings.Opacity);

        builder.UseTexture(sourceTexture, AccessFlags.Read);
        builder.UseTexture(blurredTexture, AccessFlags.Read);
        builder.UseTexture(maskTexture, AccessFlags.Read);
        builder.SetRenderAttachment(activeColor, 0, AccessFlags.ReadWrite);
        builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) => ExecuteCompositePass(data, context));
    }

    private TextureHandle GetSourceColor(ContextContainer frameData, UniversalResourceData resourceData)
    {
        if (!FrameTextureRegistry.TryGet(frameData, out FrameTextureRegistry textureRegistry))
        {
            return resourceData.activeColorTexture;
        }
        return textureRegistry.TryGetTexture(_sourceTextureId, out TextureHandle sourceColor, out _) ? sourceColor : resourceData.activeColorTexture;
    }

    private static RenderTextureDescriptor CreateBlurDescriptor(RenderTextureDescriptor descriptor, int downsample)
    {
        descriptor.depthBufferBits = 0;
        descriptor.msaaSamples = 1;
        descriptor.width = Mathf.Max(1, descriptor.width / downsample);
        descriptor.height = Mathf.Max(1, descriptor.height / downsample);
        descriptor.useDynamicScale = false;
        descriptor.useDynamicScaleExplicit = false;
        return descriptor;
    }

    private static void ExecuteBlurPass(BlurPassData data, RasterGraphContext context)
    {
        data.Material.SetVector(BlurDirectionId, data.Direction);
        data.Material.SetVector(BlurTexelSizeId, data.TexelSize);
        data.Material.SetFloat(BlurRadiusId, data.Radius);

        Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1, 1, 0, 0), data.Material, data.PassIndex);
    }

    private static void ExecuteCompositePass(CompositePassData data, RasterGraphContext context)
    {
        data.Material.SetTexture(LayerBlurSourceTextureId, data.SourceTexture);
        data.Material.SetTexture(LayerBlurredTextureId, data.BlurredTexture);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(MaskSoftnessId, data.MaskSoftness);
        data.Material.SetFloat(OpacityId, data.Opacity);

        Blitter.BlitTexture(context.cmd, data.MaskTexture, new Vector4(1, 1, 0, 0), data.Material, CompositePassIndex);
    }
}
