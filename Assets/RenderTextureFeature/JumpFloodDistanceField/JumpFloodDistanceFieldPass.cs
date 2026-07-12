using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

internal sealed class JumpFloodMaterialSet : IDisposable
{
    private readonly Material _source;
    private readonly List<MaskedEffectMaterialInstance> _jumpMaterials = new();

    public JumpFloodMaterialSet(Material source)
    {
        _source = source;
        Initialize = new MaskedEffectMaterialInstance(source);
        Resolve = new MaskedEffectMaterialInstance(source);
        Debug = new MaskedEffectMaterialInstance(source);
    }

    public MaskedEffectMaterialInstance Initialize { get; private set; }
    public MaskedEffectMaterialInstance Resolve { get; private set; }
    public MaskedEffectMaterialInstance Debug { get; private set; }

    public bool IsValid =>
        Initialize != null && Initialize.IsValid &&
        Resolve != null && Resolve.IsValid &&
        Debug != null && Debug.IsValid;

    public Material GetJumpMaterial(int index)
    {
        return _jumpMaterials[index].Material;
    }

    public void EnsureJumpMaterialCount(int count)
    {
        while (_jumpMaterials.Count < count)
        {
            _jumpMaterials.Add(new MaskedEffectMaterialInstance(_source));
        }

        while (_jumpMaterials.Count > count)
        {
            int lastIndex = _jumpMaterials.Count - 1;
            _jumpMaterials[lastIndex].Dispose();
            _jumpMaterials.RemoveAt(lastIndex);
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _jumpMaterials.Count; i++)
        {
            _jumpMaterials[i].Dispose();
        }

        _jumpMaterials.Clear();
        Initialize?.Dispose();
        Resolve?.Dispose();
        Debug?.Dispose();
        Initialize = null;
        Resolve = null;
        Debug = null;
    }
}

internal sealed class JumpFloodDistanceFieldPass : ScriptableRenderPass
{
    private const int InitializeShaderPass = 0;
    private const int JumpShaderPass = 1;
    private const int ResolveShaderPass = 2;
    private const int DebugShaderPass = 3;

    private static readonly int WorkingTexelSizeId = Shader.PropertyToID("_WorkingTexelSize");
    private static readonly int MaskTexelSizeId = Shader.PropertyToID("_MaskTexelSize");
    private static readonly int MaskTextureId = Shader.PropertyToID("_JfaMaskTexture");
    private static readonly int SeedTextureId = Shader.PropertyToID("_JfaSeedTexture");
    private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");
    private static readonly int SubpixelBoundaryId = Shader.PropertyToID("_SubpixelBoundary");
    private static readonly int JumpStepId = Shader.PropertyToID("_JumpStep");
    private static readonly int MaxDistanceId = Shader.PropertyToID("_MaxDistancePixels");
    private static readonly int DebugModeId = Shader.PropertyToID("_DebugMode");
    private static readonly int DebugRangeId = Shader.PropertyToID("_DebugRangePixels");
    private static readonly int DebugContourSpacingId = Shader.PropertyToID("_DebugContourSpacing");
    private static readonly int DebugOpacityId = Shader.PropertyToID("_DebugOpacity");

    private readonly List<int> _jumpSteps = new();
    private readonly MaskedTextureResolver _maskResolver = new(nameof(JumpFloodDistanceFieldFeature));
    private string _profilingName;
    private JumpFloodDistanceFieldFeature.Settings _settings;
    private JumpFloodMaterialSet _materials;
    private string _outputTextureName;
    private int _outputTextureId;
    private int _outputTexelSizeId;

    private sealed class InitializePassData
    {
        public TextureHandle MaskTexture;
        public Material Material;
        public Vector4 WorkingTexelSize;
        public float MaskThreshold;
        public float SubpixelBoundary;
    }

    private sealed class JumpPassData
    {
        public TextureHandle SeedTexture;
        public Material Material;
        public Vector4 WorkingTexelSize;
        public float JumpStep;
    }

    private sealed class ResolvePassData
    {
        public TextureHandle MaskTexture;
        public TextureHandle SeedTexture;
        public Material Material;
        public Vector4 MaskTexelSize;
        public Vector4 OutputTexelSize;
        public float MaskThreshold;
        public float MaxDistancePixels;
        public int OutputTexelSizePropertyId;
    }

    private sealed class DebugPassData
    {
        public TextureHandle DistanceTexture;
        public Material Material;
        public float DebugMode;
        public float DebugRangePixels;
        public float DebugContourSpacing;
        public float DebugOpacity;
    }

    public void Setup(
        string profilingName,
        JumpFloodDistanceFieldFeature.Settings settings,
        JumpFloodMaterialSet materials)
    {
        _settings = settings;
        _materials = materials;
        _maskResolver.SetTextureName(settings.InputMaskTextureName);
        if (_outputTextureName != settings.OutputTextureName)
        {
            _outputTextureName = settings.OutputTextureName;
            _outputTextureId = Shader.PropertyToID(settings.OutputTextureName);
            _outputTexelSizeId = Shader.PropertyToID($"{settings.OutputTextureName}_TexelSize");
        }
        renderPassEvent = settings.RenderPassEvent;
        profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(
            profilingName,
            ref _profilingName,
            profilingSampler);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_settings == null || _materials == null || !_materials.IsValid)
        {
            return;
        }

        if (!_maskResolver.TryResolve(
                frameData,
                out TextureHandle maskTexture,
                out Vector4 maskTexelSize))
        {
            return;
        }

        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        RenderTextureDescriptor workingDescriptor = CreateWorkingDescriptor(
            cameraData.cameraTargetDescriptor,
            maskTexelSize);
        RenderTextureDescriptor outputDescriptor = CreateOutputDescriptor(workingDescriptor);
        Vector4 workingTexelSize = MaskedEffectRenderGraphUtility.CreateTexelSize(
            workingDescriptor.width,
            workingDescriptor.height);

        int maximumJumpDistance = CalculateMaximumJumpDistance(
            workingDescriptor,
            maskTexelSize);
        BuildJumpSteps(maximumJumpDistance);
        AddFinalRefinementSteps();
        _materials.EnsureJumpMaterialCount(_jumpSteps.Count);

        TextureHandle seedPing = CreateTexture(
            renderGraph,
            workingDescriptor,
            $"{_profilingName} Seed Ping",
            FilterMode.Point);
        TextureHandle seedPong = _jumpSteps.Count > 0
            ? CreateTexture(
                renderGraph,
                workingDescriptor,
                $"{_profilingName} Seed Pong",
                FilterMode.Point)
            : TextureHandle.nullHandle;
        TextureHandle distanceTexture = CreateTexture(
            renderGraph,
            outputDescriptor,
            _settings.OutputTextureName,
            FilterMode.Bilinear);

        AddInitializePass(renderGraph, maskTexture, seedPing, workingTexelSize);

        TextureHandle currentSeeds = seedPing;
        for (int i = 0; i < _jumpSteps.Count; i++)
        {
            TextureHandle destination = currentSeeds == seedPing ? seedPong : seedPing;
            AddJumpPass(
                renderGraph,
                $"{_profilingName} Jump {_jumpSteps[i]}",
                currentSeeds,
                destination,
                _materials.GetJumpMaterial(i),
                workingTexelSize,
                _jumpSteps[i]);
            currentSeeds = destination;
        }

        Vector4 outputTexelSize = MaskedEffectRenderGraphUtility.CreateTexelSize(
            outputDescriptor.width,
            outputDescriptor.height);
        RegisterOutputTexture(frameData, distanceTexture, outputTexelSize);
        AddResolvePass(
            renderGraph,
            maskTexture,
            currentSeeds,
            distanceTexture,
            maskTexelSize,
            outputTexelSize);

        if (_settings.DebugView != JumpFloodDistanceFieldFeature.DebugMode.Disabled)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            AddDebugPass(renderGraph, resourceData.activeColorTexture, distanceTexture);
        }
    }

    private void AddInitializePass(
        RenderGraph renderGraph,
        TextureHandle maskTexture,
        TextureHandle destination,
        Vector4 workingTexelSize)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            $"{_profilingName} Initialize",
            out InitializePassData passData,
            profilingSampler);

        passData.MaskTexture = maskTexture;
        passData.Material = _materials.Initialize.Material;
        passData.WorkingTexelSize = workingTexelSize;
        passData.MaskThreshold = Mathf.Clamp01(_settings.MaskThreshold);
        passData.SubpixelBoundary = _settings.SubpixelBoundary ? 1.0f : 0.0f;

        builder.UseTexture(maskTexture, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0);
        builder.SetRenderFunc((InitializePassData data, RasterGraphContext context) => ExecuteInitializePass(data, context));
    }

    private void AddJumpPass(
        RenderGraph renderGraph,
        string passName,
        TextureHandle source,
        TextureHandle destination,
        Material material,
        Vector4 workingTexelSize,
        int jumpStep)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            passName,
            out JumpPassData passData,
            profilingSampler);

        passData.SeedTexture = source;
        passData.Material = material;
        passData.WorkingTexelSize = workingTexelSize;
        passData.JumpStep = jumpStep;

        builder.UseTexture(source, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0);
        builder.SetRenderFunc((JumpPassData data, RasterGraphContext context) => ExecuteJumpPass(data, context));
    }

    private void AddResolvePass(
        RenderGraph renderGraph,
        TextureHandle maskTexture,
        TextureHandle seedTexture,
        TextureHandle destination,
        Vector4 maskTexelSize,
        Vector4 outputTexelSize)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            $"{_profilingName} Resolve",
            out ResolvePassData passData,
            profilingSampler);

        passData.MaskTexture = maskTexture;
        passData.SeedTexture = seedTexture;
        passData.Material = _materials.Resolve.Material;
        passData.MaskTexelSize = maskTexelSize;
        passData.OutputTexelSize = outputTexelSize;
        passData.MaskThreshold = Mathf.Clamp01(_settings.MaskThreshold);
        passData.MaxDistancePixels = Mathf.Max(1.0f, _settings.MaxDistancePixels);
        passData.OutputTexelSizePropertyId = _outputTexelSizeId;

        builder.UseTexture(maskTexture, AccessFlags.Read);
        builder.UseTexture(seedTexture, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0);
        builder.SetGlobalTextureAfterPass(destination, _outputTextureId);
        builder.AllowGlobalStateModification(true);
        builder.SetRenderFunc((ResolvePassData data, RasterGraphContext context) => ExecuteResolvePass(data, context));
    }

    private void AddDebugPass(
        RenderGraph renderGraph,
        TextureHandle activeColor,
        TextureHandle distanceTexture)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            $"{_profilingName} Debug",
            out DebugPassData passData,
            profilingSampler);

        passData.DistanceTexture = distanceTexture;
        passData.Material = _materials.Debug.Material;
        passData.DebugMode = (float)_settings.DebugView;
        passData.DebugRangePixels = Mathf.Max(1.0f, _settings.DebugRangePixels);
        passData.DebugContourSpacing = Mathf.Max(1.0f, _settings.DebugContourSpacing);
        passData.DebugOpacity = Mathf.Clamp01(_settings.DebugOpacity);

        builder.UseTexture(distanceTexture, AccessFlags.Read);
        builder.SetRenderAttachment(activeColor, 0, AccessFlags.ReadWrite);
        builder.SetRenderFunc((DebugPassData data, RasterGraphContext context) => ExecuteDebugPass(data, context));
    }

    private void RegisterOutputTexture(
        ContextContainer frameData,
        TextureHandle distanceTexture,
        Vector4 texelSize)
    {
        FrameTextureRegistry textureData = FrameTextureRegistry.GetOrCreate(frameData);
        textureData.SetTexture(_outputTextureId, distanceTexture, texelSize);
    }

    private RenderTextureDescriptor CreateWorkingDescriptor(
        RenderTextureDescriptor descriptor,
        Vector4 maskTexelSize)
    {
        int downsample = Mathf.Clamp(_settings.Downsample, 1, 4);
        descriptor.width = Mathf.Max(1, Mathf.CeilToInt(maskTexelSize.z / downsample));
        descriptor.height = Mathf.Max(1, Mathf.CeilToInt(maskTexelSize.w / downsample));
        descriptor.graphicsFormat = _settings.TexturePrecision == JumpFloodDistanceFieldFeature.Precision.Float
            ? GraphicsFormat.R32G32_SFloat
            : GraphicsFormat.R16G16_SFloat;
        descriptor.depthBufferBits = 0;
        descriptor.msaaSamples = 1;
        descriptor.useDynamicScale = false;
        descriptor.useDynamicScaleExplicit = false;
        return descriptor;
    }

    private RenderTextureDescriptor CreateOutputDescriptor(RenderTextureDescriptor descriptor)
    {
        descriptor.graphicsFormat = _settings.TexturePrecision == JumpFloodDistanceFieldFeature.Precision.Float
            ? GraphicsFormat.R32_SFloat
            : GraphicsFormat.R16_SFloat;
        return descriptor;
    }

    private static TextureHandle CreateTexture(
        RenderGraph renderGraph,
        RenderTextureDescriptor descriptor,
        string name,
        FilterMode filterMode)
    {
        return UniversalRenderer.CreateRenderGraphTexture(
            renderGraph,
            descriptor,
            name,
            false,
            filterMode,
            TextureWrapMode.Clamp);
    }

    private int CalculateMaximumJumpDistance(
        RenderTextureDescriptor workingDescriptor,
        Vector4 maskTexelSize)
    {
        int textureLimit = Mathf.Max(
            workingDescriptor.width - 1,
            workingDescriptor.height - 1);
        if (textureLimit <= 0)
        {
            return 0;
        }

        float maxDistancePixels = Mathf.Max(1.0f, _settings.MaxDistancePixels);
        float maskWidth = Mathf.Max(1.0f, maskTexelSize.z);
        float maskHeight = Mathf.Max(1.0f, maskTexelSize.w);
        float requiredHorizontalDistance =
            maxDistancePixels * workingDescriptor.width / maskWidth;
        float requiredVerticalDistance =
            maxDistancePixels * workingDescriptor.height / maskHeight;
        int requiredWorkingDistance = Mathf.Max(
            1,
            Mathf.CeilToInt(Mathf.Max(
                requiredHorizontalDistance,
                requiredVerticalDistance)));

        return Mathf.Min(textureLimit, requiredWorkingDistance);
    }

    private void BuildJumpSteps(int maximumJumpDistance)
    {
        _jumpSteps.Clear();
        if (maximumJumpDistance <= 0)
        {
            return;
        }

        int step = 1;

        while (step <= maximumJumpDistance / 2)
        {
            step <<= 1;
        }

        while (step >= 1)
        {
            _jumpSteps.Add(step);
            step >>= 1;
        }
    }

    private void AddFinalRefinementSteps()
    {
        if (_jumpSteps.Count == 0)
        {
            return;
        }

        int refinementPasses = Mathf.Clamp(_settings.FinalRefinementPasses, 0, 2);
        for (int i = 0; i < refinementPasses; i++)
        {
            _jumpSteps.Add(1);
        }
    }

    private static void ExecuteInitializePass(InitializePassData data, RasterGraphContext context)
    {
        data.Material.SetVector(WorkingTexelSizeId, data.WorkingTexelSize);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(SubpixelBoundaryId, data.SubpixelBoundary);

        Blitter.BlitTexture(
            context.cmd,
            data.MaskTexture,
            new Vector4(1, 1, 0, 0),
            data.Material,
            InitializeShaderPass);
    }

    private static void ExecuteJumpPass(JumpPassData data, RasterGraphContext context)
    {
        data.Material.SetVector(WorkingTexelSizeId, data.WorkingTexelSize);
        data.Material.SetFloat(JumpStepId, data.JumpStep);

        Blitter.BlitTexture(
            context.cmd,
            data.SeedTexture,
            new Vector4(1, 1, 0, 0),
            data.Material,
            JumpShaderPass);
    }

    private static void ExecuteResolvePass(ResolvePassData data, RasterGraphContext context)
    {
        data.Material.SetTexture(MaskTextureId, data.MaskTexture);
        data.Material.SetTexture(SeedTextureId, data.SeedTexture);
        data.Material.SetVector(MaskTexelSizeId, data.MaskTexelSize);
        data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
        data.Material.SetFloat(MaxDistanceId, data.MaxDistancePixels);
        context.cmd.SetGlobalVector(data.OutputTexelSizePropertyId, data.OutputTexelSize);

        Blitter.BlitTexture(
            context.cmd,
            data.SeedTexture,
            new Vector4(1, 1, 0, 0),
            data.Material,
            ResolveShaderPass);
    }

    private static void ExecuteDebugPass(DebugPassData data, RasterGraphContext context)
    {
        data.Material.SetFloat(DebugModeId, data.DebugMode);
        data.Material.SetFloat(DebugRangeId, data.DebugRangePixels);
        data.Material.SetFloat(DebugContourSpacingId, data.DebugContourSpacing);
        data.Material.SetFloat(DebugOpacityId, data.DebugOpacity);

        Blitter.BlitTexture(
            context.cmd,
            data.DistanceTexture,
            new Vector4(1, 1, 0, 0),
            data.Material,
            DebugShaderPass);
    }
}
