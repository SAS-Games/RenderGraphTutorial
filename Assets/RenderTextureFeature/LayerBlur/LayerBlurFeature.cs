using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class LayerBlurFeature : ScriptableRendererFeature
{
    private const string BlurShaderName = "Hidden/RenderTextureFeature/LayerBlur/BlurComposite";

    [Tooltip("Base name used for profiling markers and generated blur pass names.")]
    public string ProfilingName = "Layer Blur";

    [Tooltip("Material that uses Hidden/RenderTextureFeature/LayerBlur/BlurComposite. Assign the included LayerBlur material so the shader is included in builds.")]
    public Material BlurMaterial;

    [Tooltip("One entry per mask texture. Entries are composited in list order, so a later entry owns pixels where masks overlap.")]
    public List<Settings> BlurLayerSettings = new() { new Settings() };

    private readonly List<LayerBlurPass> _blurPasses = new();
    private readonly List<FrameColorSnapshotPass> _sourceSnapshotPasses = new();
    private readonly List<RenderPassEvent> _renderPassEvents = new();
    private readonly MaskedEffectItemPool<LayerBlurMaterialSet> _passMaterials =
        new(source => new LayerBlurMaterialSet(source));
    private readonly MaskedEffectMaterialCache _materialCache =
        new(nameof(LayerBlurFeature), BlurShaderName);

    [Serializable]
    public sealed class Settings : MaskedEffectLayerSettings
    {
        public Settings()
        {
            Name = "Blur Layer";
            MaskTextureName = "_LayerBlurMask";
        }

        [Tooltip("Resolution divisor for temporary blur textures. 1 is full resolution, 2 is half resolution, and 4 is quarter resolution. Layers with the same Downsample and Blur Radius share one blur chain.")]
        [Range(1, 4)]
        public int Downsample = 2;

        [Tooltip("Blur level selected from the shared chain. The chain runs only to the highest Iterations value requested by layers with the same Downsample and Blur Radius.")]
        [Range(1, 4)]
        public int Iterations = 2;

        [Tooltip("Distance between blur samples. Larger values spread the blur farther. Keep this equal across related layers so they can share one blur chain and use Iterations for different strengths.")]
        [Range(0.0f, 8.0f)]
        public float BlurRadius = 2.0f;

        [Tooltip("Transition width above Mask Threshold. Increase this to soften the boundary between sharp and blurred pixels.")]
        [Range(0.0f, 1.0f)]
        public float MaskSoftness = 0.05f;
    }

    public override void Create()
    {
        EnsureBlurPassCount(CountEnabledLayers());
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        BlurLayerSettings ??= new List<Settings>();

        int enabledLayerCount = CountEnabledLayers();
        if (enabledLayerCount == 0 || !_materialCache.Ensure(BlurMaterial))
        {
            return;
        }

        CollectRenderPassEvents();
        EnsureBlurPassCount(_renderPassEvents.Count);
        _passMaterials.EnsureCount(enabledLayerCount, _materialCache.Material, _materialCache.Version);
        EnsureSourceSnapshotPassCount(_renderPassEvents.Count);

        for (int i = 0; i < _renderPassEvents.Count; i++)
        {
            RenderPassEvent passEvent = _renderPassEvents[i];
            _blurPasses[i].Setup(
                $"{ProfilingName} ({passEvent})",
                passEvent,
                _sourceSnapshotPasses[i].SnapshotTextureId);
        }

        int enabledIndex = 0;
        for (int settingsIndex = 0; settingsIndex < BlurLayerSettings.Count; settingsIndex++)
        {
            Settings settings = BlurLayerSettings[settingsIndex];
            if (!IsEnabled(settings))
            {
                continue;
            }

            int eventIndex = _renderPassEvents.IndexOf(settings.RenderPassEvent);
            _blurPasses[eventIndex].AddLayer(
                GetPassName(settings, settingsIndex),
                settings,
                _passMaterials[enabledIndex]);
            enabledIndex++;
        }

        for (int i = 0; i < _renderPassEvents.Count; i++)
        {
            RenderPassEvent passEvent = _renderPassEvents[i];
            FrameColorSnapshotPass sourceSnapshotPass = _sourceSnapshotPasses[i];
            sourceSnapshotPass.Setup($"{ProfilingName} Source ({passEvent})", passEvent);
            renderer.EnqueuePass(sourceSnapshotPass);
            renderer.EnqueuePass(_blurPasses[i]);
        }
    }

    protected override void Dispose(bool disposing)
    {
        _passMaterials.Dispose();
        _materialCache.Dispose();
    }

    private void CollectRenderPassEvents()
    {
        _renderPassEvents.Clear();

        for (int i = 0; i < BlurLayerSettings.Count; i++)
        {
            Settings settings = BlurLayerSettings[i];
            if (IsEnabled(settings) && !_renderPassEvents.Contains(settings.RenderPassEvent))
            {
                _renderPassEvents.Add(settings.RenderPassEvent);
            }
        }

        _renderPassEvents.Sort((left, right) => left.CompareTo(right));
    }

    private int CountEnabledLayers()
    {
        if (BlurLayerSettings == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < BlurLayerSettings.Count; i++)
        {
            if (IsEnabled(BlurLayerSettings[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsEnabled(Settings settings)
    {
        return settings != null &&
               settings.Enabled &&
               !string.IsNullOrWhiteSpace(settings.MaskTextureName);
    }

    private void EnsureBlurPassCount(int count)
    {
        while (_blurPasses.Count < count)
        {
            _blurPasses.Add(new LayerBlurPass());
        }
    }

    private void EnsureSourceSnapshotPassCount(int count)
    {
        while (_sourceSnapshotPasses.Count < count)
        {
            _sourceSnapshotPasses.Add(new FrameColorSnapshotPass());
        }
    }

    private string GetPassName(Settings settings, int index)
    {
        string layerName = string.IsNullOrWhiteSpace(settings.Name)
            ? $"Layer {index}"
            : settings.Name;

        return $"{ProfilingName} ({layerName})";
    }
}
