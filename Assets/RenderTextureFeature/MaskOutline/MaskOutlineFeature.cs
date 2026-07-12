using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class MaskOutlineFeature : ScriptableRendererFeature
{
    private const string CompositeShaderName = "Hidden/RenderTextureFeature/MaskOutline/Composite";

    [Tooltip("Name used for Render Graph passes and profiler markers.")]
    public string ProfilingName = "Mask Outline";

    [Tooltip("Material that uses Hidden/RenderTextureFeature/MaskOutline/Composite.")]
    public Material CompositeMaterial;

    [Tooltip("Mask source, edge placement, color, and width of the outline.")]
    public Settings OutlineSettings = new();

    private MaskOutlinePass _pass;
    private readonly MaskedEffectMaterialCache _materialCache =
        new(nameof(MaskOutlineFeature), CompositeShaderName);
    private readonly MaskedEffectItemPool<MaskOutlineMaterialSet> _materialSets =
        new(source => new MaskOutlineMaterialSet(source));

    public enum OutlineMode
    {
        Outside = 0,
        Inside = 1,
        Both = 2
    }

    [Serializable]
    public sealed class Settings
    {
        [Tooltip("When the outline is composited. This must run after the mask texture is produced.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Texture name produced by ObjectsToRenderTextureFeature.")]
        public string MaskTextureName = "_MaskOutlineMask";

        [ColorUsage(true, true)]
        public Color OutlineColor = new(1.0f, 0.82f, 0.0f, 1.0f);

        [Tooltip("Outline width in mask pixels.")]
        [Range(1.0f, 16.0f)]
        public float OutlineWidth = 3.0f;

        [Tooltip("Brightness and alpha strength of the outline.")]
        [Range(0.0f, 5.0f)]
        public float OutlineIntensity = 1.0f;

        [Tooltip("Mask value where a pixel is considered part of the object.")]
        [Range(0.0f, 1.0f)]
        public float MaskThreshold = 0.5f;

        [Tooltip("Outside draws around the silhouette, Inside draws within it, and Both draws across the edge.")]
        public OutlineMode Mode = OutlineMode.Outside;
    }

    public override void Create()
    {
        _pass ??= new MaskOutlinePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        OutlineSettings ??= new Settings();

        if (string.IsNullOrWhiteSpace(OutlineSettings.MaskTextureName) ||
            OutlineSettings.OutlineIntensity <= 0.0001f ||
            !_materialCache.Ensure(CompositeMaterial))
        {
            return;
        }

        _materialSets.EnsureCount(1, _materialCache.Material, _materialCache.Version);
        _pass.Setup(ProfilingName, OutlineSettings, _materialSets[0]);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _materialSets.Dispose();
        _materialCache.Dispose();
    }
}
