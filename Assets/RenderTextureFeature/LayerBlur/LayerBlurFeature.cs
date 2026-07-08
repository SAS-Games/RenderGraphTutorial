using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class LayerBlurFeature : ScriptableRendererFeature
{
    private const string BlurShaderName = "Hidden/RenderTextureFeature/LayerBlur/BlurComposite";

    [Tooltip("Base name used for profiling markers and generated blur pass names.")]
    public string ProfilingName = "Layer Blur";

    [Tooltip("Material that uses Hidden/RenderTextureFeature/LayerBlur/BlurComposite. Assign the included LayerBlur material for build-safe shader references.")]
    public Material BlurMaterial;

    [Tooltip("One entry per blur mask. Each entry can read a different mask texture and use a different blur radius, iteration count, downsample value, and opacity.")]
    public List<Settings> BlurLayerSettings = new() { new Settings() };

    [SerializeField, HideInInspector, FormerlySerializedAs("BlurSettings")]
    private Settings _legacyBlurSettings = new();

    [SerializeField, HideInInspector]
    private bool _legacySettingsMigrated;

    private readonly List<LayerBlurPass> _passes = new();
    private readonly List<LayerBlurSourceCopyPass> _sourceCopyPasses = new();
    private Material _runtimeMaterial;
    private Material _sourceMaterial;
    private bool _loggedMissingShader;

    [Serializable]
    public class Settings
    {
        [Tooltip("Disables this blur entry without removing it from the list.")]
        public bool Enabled = true;

        [Tooltip("Human-readable name for this blur entry. Used only for profiling and pass labels.")]
        public string Name = "Blur Layer";

        [Tooltip("When this blur entry runs. It must run after the mask texture it reads has been produced.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Texture name produced by ObjectsToRenderTextureFeature for this blur entry, for example _LightBlurMask or _HeavyBlurMask.")]
        public string MaskTextureName = "_LayerBlurMask";

        [Tooltip("Resolution divisor for the temporary blur textures. 1 is full resolution, 2 is half resolution, and 4 is quarter resolution. Higher values are faster and softer.")]
        [Range(1, 4)]
        public int Downsample = 2;

        [Tooltip("Number of horizontal/vertical blur pass pairs. More iterations are smoother but more expensive.")]
        [Range(1, 4)]
        public int Iterations = 2;

        [Tooltip("Sample spread for each blur pass. Larger values create stronger blur and more visible softness.")]
        [Range(0.0f, 8.0f)]
        public float BlurRadius = 2.0f;

        [Tooltip("Mask value where blur starts to appear. Pure black remains unblurred; raise this when soft or noisy masks leak blur into unwanted areas.")]
        [Range(0.0f, 1.0f)]
        public float MaskThreshold = 0.5f;

        [Tooltip("Soft transition width above Mask Threshold. Higher values create smoother blur transitions at mask edges.")]
        [Range(0.0f, 1.0f)]
        public float MaskSoftness = 0.05f;

        [Tooltip("Blend strength of this blur entry. 0 skips the entry; 1 fully applies the blurred color inside the mask.")]
        [Range(0.0f, 1.0f)]
        public float Opacity = 1.0f;
    }

    public override void Create()
    {
        MigrateLegacySettingsIfNeeded();
        EnsurePassCount(GetEnabledSettingsCount());
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        MigrateLegacySettingsIfNeeded();
        BlurLayerSettings ??= new List<Settings>();

        int enabledSettingsCount = GetEnabledSettingsCount();
        if (enabledSettingsCount == 0)
        {
            return;
        }

        if (!EnsureBlurMaterial())
        {
            return;
        }

        EnsurePassCount(enabledSettingsCount);
        List<RenderPassEvent> enabledRenderPassEvents = GetEnabledRenderPassEvents();
        EnsureSourceCopyPassCount(enabledRenderPassEvents.Count);

        for (int i = 0; i < enabledRenderPassEvents.Count; i++)
        {
            RenderPassEvent sourceRenderPassEvent = enabledRenderPassEvents[i];
            LayerBlurSourceCopyPass sourceCopyPass = _sourceCopyPasses[i];
            sourceCopyPass.Setup($"{ProfilingName} Source ({sourceRenderPassEvent})", sourceRenderPassEvent);
            renderer.EnqueuePass(sourceCopyPass);
        }

        int passIndex = 0;
        for (int i = 0; i < BlurLayerSettings.Count; i++)
        {
            Settings settings = BlurLayerSettings[i];
            if (!ShouldEnqueue(settings))
            {
                continue;
            }

            LayerBlurPass pass = _passes[passIndex];
            pass.Setup(GetPassName(settings, i), settings, _runtimeMaterial);
            renderer.EnqueuePass(pass);
            passIndex++;
        }
    }

    private void OnValidate()
    {
        MigrateLegacySettingsIfNeeded();
    }

    private bool EnsureBlurMaterial()
    {
        if (BlurMaterial != null)
        {
            if (_runtimeMaterial == null || _sourceMaterial != BlurMaterial)
            {
                CoreUtils.Destroy(_runtimeMaterial);
                _runtimeMaterial = new Material(BlurMaterial)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _sourceMaterial = BlurMaterial;
            }

            return true;
        }

        if (_runtimeMaterial != null && _sourceMaterial == null)
        {
            return true;
        }

        CoreUtils.Destroy(_runtimeMaterial);
        _runtimeMaterial = CoreUtils.CreateEngineMaterial(BlurShaderName);
        _sourceMaterial = null;
        if (_runtimeMaterial != null || _loggedMissingShader)
        {
            return _runtimeMaterial != null;
        }

        Debug.LogError($"{nameof(LayerBlurFeature)} could not find shader '{BlurShaderName}'.");
        _loggedMissingShader = true;
        return false;
    }

    private void EnsurePassCount(int count)
    {
        while (_passes.Count < count)
        {
            _passes.Add(new LayerBlurPass());
        }
    }

    private void EnsureSourceCopyPassCount(int count)
    {
        while (_sourceCopyPasses.Count < count)
        {
            _sourceCopyPasses.Add(new LayerBlurSourceCopyPass());
        }
    }

    private int GetEnabledSettingsCount()
    {
        if (BlurLayerSettings == null)
        {
            return 0;
        }

        int count = 0;
        foreach (Settings settings in BlurLayerSettings)
        {
            if (ShouldEnqueue(settings))
            {
                count++;
            }
        }

        return count;
    }

    private List<RenderPassEvent> GetEnabledRenderPassEvents()
    {
        var renderPassEvents = new List<RenderPassEvent>();
        if (BlurLayerSettings == null)
        {
            return renderPassEvents;
        }

        foreach (Settings settings in BlurLayerSettings)
        {
            if (!ShouldEnqueue(settings) || renderPassEvents.Contains(settings.RenderPassEvent))
            {
                continue;
            }

            renderPassEvents.Add(settings.RenderPassEvent);
        }

        renderPassEvents.Sort((a, b) => a.CompareTo(b));
        return renderPassEvents;
    }

    private static bool ShouldEnqueue(Settings settings)
    {
        return settings != null &&
               settings.Enabled &&
               settings.Opacity > 0.0f &&
               !string.IsNullOrWhiteSpace(settings.MaskTextureName);
    }

    private string GetPassName(Settings settings, int index)
    {
        string layerName = string.IsNullOrWhiteSpace(settings.Name)
            ? $"Layer {index}"
            : settings.Name;

        return $"{ProfilingName} ({layerName})";
    }

    private void MigrateLegacySettingsIfNeeded()
    {
        if (_legacySettingsMigrated || !HasConfiguredSettings(_legacyBlurSettings))
        {
            return;
        }

        BlurLayerSettings ??= new List<Settings>();
        if (BlurLayerSettings.Count == 0 ||
            (BlurLayerSettings.Count == 1 && !HasConfiguredSettings(BlurLayerSettings[0])))
        {
            BlurLayerSettings.Clear();
            BlurLayerSettings.Add(_legacyBlurSettings);
        }

        _legacySettingsMigrated = true;
    }

    private static bool HasConfiguredSettings(Settings settings)
    {
        if (settings == null)
        {
            return false;
        }

        return !settings.Enabled ||
               settings.Name != "Blur Layer" ||
               settings.RenderPassEvent != RenderPassEvent.AfterRenderingTransparents ||
               settings.MaskTextureName != "_LayerBlurMask" ||
               settings.Downsample != 2 ||
               settings.Iterations != 2 ||
               !Mathf.Approximately(settings.BlurRadius, 2.0f) ||
               !Mathf.Approximately(settings.MaskThreshold, 0.5f) ||
               !Mathf.Approximately(settings.MaskSoftness, 0.05f) ||
               !Mathf.Approximately(settings.Opacity, 1.0f);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_runtimeMaterial);
        _runtimeMaterial = null;
        _sourceMaterial = null;
    }

    public class LayerBlurSourceData : ContextItem
    {
        private readonly Dictionary<int, TextureHandle> _sourceTextures = new();

        public override void Reset()
        {
            _sourceTextures.Clear();
        }

        public void SetSource(RenderPassEvent renderPassEvent, TextureHandle sourceTexture)
        {
            _sourceTextures[(int)renderPassEvent] = sourceTexture;
        }

        public bool TryGetSource(RenderPassEvent renderPassEvent, out TextureHandle sourceTexture)
        {
            return _sourceTextures.TryGetValue((int)renderPassEvent, out sourceTexture);
        }
    }

    private class LayerBlurSourceCopyPass : ScriptableRenderPass
    {
        private string _profilingName;
        private RenderPassEvent _sourceRenderPassEvent;

        private class SourceCopyPassData
        {
            public TextureHandle Source;
        }

        public void Setup(string profilingName, RenderPassEvent sourceRenderPassEvent)
        {
            _profilingName = profilingName;
            _sourceRenderPassEvent = sourceRenderPassEvent;
            renderPassEvent = sourceRenderPassEvent;
            profilingSampler = new ProfilingSampler(profilingName);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            TextureHandle sourceCopy = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                descriptor,
                $"{_profilingName} Texture",
                false,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp);

            LayerBlurSourceData sourceData = frameData.GetOrCreate<LayerBlurSourceData>();
            sourceData.SetSource(_sourceRenderPassEvent, sourceCopy);

            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                _profilingName,
                out SourceCopyPassData passData,
                profilingSampler);

            passData.Source = resourceData.activeColorTexture;

            builder.UseTexture(passData.Source, AccessFlags.Read);
            builder.SetRenderAttachment(sourceCopy, 0);
            builder.SetRenderFunc((SourceCopyPassData data, RasterGraphContext context) => ExecutePass(data, context));
        }

        private static void ExecutePass(SourceCopyPassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1, 1, 0, 0), 0.0f, false);
        }
    }

    private class LayerBlurPass : ScriptableRenderPass
    {
        private const int HorizontalBlurPassIndex = 0;
        private const int VerticalBlurPassIndex = 1;
        private const int CompositePassIndex = 2;

        private static readonly int LayerBlurredTextureId = Shader.PropertyToID("_LayerBlurredTexture");
        private static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");
        private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
        private static readonly int BlurTexelSizeId = Shader.PropertyToID("_BlurTexelSize");
        private static readonly int MaskThresholdId = Shader.PropertyToID("_MaskThreshold");
        private static readonly int MaskSoftnessId = Shader.PropertyToID("_MaskSoftness");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private string _profilingName;
        private Settings _settings;
        private Material _material;
        private int _maskTexturePropertyId;
        private string _maskTextureName;
        private bool _loggedMissingMaskData;
        private bool _loggedMissingMaskTexture;

        private class BlurPassData
        {
            public TextureHandle Source;
            public Material Material;
            public Vector4 Direction;
            public Vector4 TexelSize;
            public float Radius;
            public int PassIndex;
        }

        private class CompositePassData
        {
            public TextureHandle BlurredTexture;
            public TextureHandle MaskTexture;
            public Material Material;
            public float MaskThreshold;
            public float MaskSoftness;
            public float Opacity;
        }

        public void Setup(string profilingName, Settings settings, Material material)
        {
            _profilingName = profilingName;
            _settings = settings;
            _material = material;

            if (_maskTextureName != settings.MaskTextureName)
            {
                _maskTextureName = settings.MaskTextureName;
                _maskTexturePropertyId = Shader.PropertyToID(_maskTextureName);
                _loggedMissingMaskData = false;
                _loggedMissingMaskTexture = false;
            }

            renderPassEvent = settings.RenderPassEvent;
            profilingSampler = new ProfilingSampler(profilingName);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null || _settings == null)
            {
                return;
            }

            if (!frameData.Contains<RenderTexturePass.CustomTextureData>())
            {
                LogMissingMaskDataOnce();
                return;
            }

            RenderTexturePass.CustomTextureData textureData = frameData.Get<RenderTexturePass.CustomTextureData>();
            if (!textureData.TryGetTexture(_maskTexturePropertyId, out TextureHandle maskTexture, out _))
            {
                LogMissingMaskTextureOnce();
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            RenderTextureDescriptor blurDescriptor = CreateBlurDescriptor(cameraData.cameraTargetDescriptor);
            Vector4 blurTexelSize = CreateTexelSize(blurDescriptor.width, blurDescriptor.height);

            TextureHandle blurPing = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                blurDescriptor,
                $"{_profilingName} Ping",
                false,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp);

            TextureHandle blurPong = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                blurDescriptor,
                $"{_profilingName} Pong",
                false,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp);

            int iterations = Mathf.Clamp(_settings.Iterations, 1, 4);
            float radius = Mathf.Clamp(_settings.BlurRadius, 0.0f, 8.0f);
            TextureHandle sourceColor = GetSourceColor(frameData, resourceData);
            TextureHandle currentSource = sourceColor;

            for (int i = 0; i < iterations; i++)
            {
                AddBlurPass(
                    renderGraph,
                    $"{_profilingName} Horizontal {i + 1}",
                    currentSource,
                    blurPing,
                    Vector2.right,
                    blurTexelSize,
                    radius,
                    HorizontalBlurPassIndex);

                currentSource = blurPing;

                AddBlurPass(
                    renderGraph,
                    $"{_profilingName} Vertical {i + 1}",
                    currentSource,
                    blurPong,
                    Vector2.up,
                    blurTexelSize,
                    radius,
                    VerticalBlurPassIndex);

                currentSource = blurPong;
            }

            AddCompositePass(renderGraph, resourceData.activeColorTexture, currentSource, maskTexture);
        }

        private void AddBlurPass(
            RenderGraph renderGraph,
            string passName,
            TextureHandle source,
            TextureHandle destination,
            Vector2 direction,
            Vector4 texelSize,
            float radius,
            int passIndex)
        {
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                passName,
                out BlurPassData passData,
                profilingSampler);

            passData.Source = source;
            passData.Material = _material;
            passData.Direction = new Vector4(direction.x, direction.y, 0.0f, 0.0f);
            passData.TexelSize = texelSize;
            passData.Radius = radius;
            passData.PassIndex = passIndex;

            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0);

            builder.SetRenderFunc((BlurPassData data, RasterGraphContext context) => ExecuteBlurPass(data, context));
        }

        private void AddCompositePass(
            RenderGraph renderGraph,
            TextureHandle activeColor,
            TextureHandle blurredTexture,
            TextureHandle maskTexture)
        {
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                $"{_profilingName} Composite",
                out CompositePassData passData,
                profilingSampler);

            passData.MaskTexture = maskTexture;
            passData.BlurredTexture = blurredTexture;
            passData.Material = _material;
            passData.MaskThreshold = Mathf.Clamp01(_settings.MaskThreshold);
            passData.MaskSoftness = Mathf.Clamp01(_settings.MaskSoftness);
            passData.Opacity = Mathf.Clamp01(_settings.Opacity);

            builder.UseTexture(blurredTexture, AccessFlags.Read);
            builder.UseTexture(maskTexture, AccessFlags.Read);
            builder.SetRenderAttachment(activeColor, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc((CompositePassData data, RasterGraphContext context) => ExecuteCompositePass(data, context));
        }

        private TextureHandle GetSourceColor(ContextContainer frameData, UniversalResourceData resourceData)
        {
            if (!frameData.Contains<LayerBlurSourceData>())
            {
                return resourceData.activeColorTexture;
            }

            LayerBlurSourceData sourceData = frameData.Get<LayerBlurSourceData>();
            return sourceData.TryGetSource(_settings.RenderPassEvent, out TextureHandle sourceColor)
                ? sourceColor
                : resourceData.activeColorTexture;
        }

        private RenderTextureDescriptor CreateBlurDescriptor(RenderTextureDescriptor cameraDescriptor)
        {
            int downsample = Mathf.Clamp(_settings.Downsample, 1, 4);
            cameraDescriptor.depthBufferBits = 0;
            cameraDescriptor.msaaSamples = 1;
            cameraDescriptor.width = Mathf.Max(1, cameraDescriptor.width / downsample);
            cameraDescriptor.height = Mathf.Max(1, cameraDescriptor.height / downsample);
            cameraDescriptor.useDynamicScale = false;
            cameraDescriptor.useDynamicScaleExplicit = false;
            return cameraDescriptor;
        }

        private void LogMissingMaskDataOnce()
        {
            if (_loggedMissingMaskData)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(LayerBlurFeature)} did not find render texture data. " +
                $"Add {nameof(ObjectsToRenderTextureFeature)} before this feature and render a mask named '{_maskTextureName}'.");
            _loggedMissingMaskData = true;
        }

        private void LogMissingMaskTextureOnce()
        {
            if (_loggedMissingMaskTexture)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(LayerBlurFeature)} did not find mask texture '{_maskTextureName}'. " +
                $"Make sure a {nameof(ObjectsToRenderTextureFeature)} output uses the same Texture Name.");
            _loggedMissingMaskTexture = true;
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
            data.Material.SetTexture(LayerBlurredTextureId, data.BlurredTexture);
            data.Material.SetFloat(MaskThresholdId, data.MaskThreshold);
            data.Material.SetFloat(MaskSoftnessId, data.MaskSoftness);
            data.Material.SetFloat(OpacityId, data.Opacity);

            Blitter.BlitTexture(context.cmd, data.MaskTexture, new Vector4(1, 1, 0, 0), data.Material, CompositePassIndex);
        }

        private static Vector4 CreateTexelSize(int width, int height)
        {
            return new Vector4(1.0f / width, 1.0f / height, width, height);
        }
    }
}
