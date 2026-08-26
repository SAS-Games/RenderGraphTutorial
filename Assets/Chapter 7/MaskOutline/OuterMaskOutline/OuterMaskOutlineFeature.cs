using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class OuterMaskOutlineFeature : ScriptableRendererFeature
{
    private const string CompositeShaderName =
        "Hidden/RenderTextureFeature/OuterMaskOutline/Composite";

    [Tooltip("Name used for Render Graph passes and profiler markers.")]
    public string ProfilingName = "Outer Mask Outline";

    [Tooltip("Material that uses the outer-mask-outline composite shader.")]
    public Material CompositeMaterial;

    public Settings OutlineSettings = new();

    private OuterMaskOutlinePass _pass;
    private readonly MaskedEffectMaterialCache _materialCache = new(
        nameof(OuterMaskOutlineFeature),
        CompositeShaderName
    );
    private readonly MaskedEffectItemPool<OuterMaskOutlineMaterialSet> _materialSets =
        new(source => new OuterMaskOutlineMaterialSet(source));

    [Serializable]
    public sealed class Settings
    {
        [Tooltip("When the outline is composited. This must run after the mask texture is produced.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Texture name produced by RenderObjectsToTextureFeature.")]
        public string MaskTextureName = "_SelectionOutlineMask";

        [ColorUsage(true, true)]
        public Color OutlineColor = new(1.0f, 0.82f, 0.0f, 1.0f);

        [Tooltip("Solid outside-outline width in mask pixels.")]
        [Range(1.0f, 16.0f)]
        public float OutlineWidth = 3.0f;

        [Tooltip("Feather distance beyond the solid outline, in mask pixels.")]
        [Range(0.0f, 8.0f)]
        public float OutlineSoftness = 2.0f;

        [Tooltip("Brightness and alpha strength of the outline.")]
        [Range(0.0f, 5.0f)]
        public float OutlineIntensity = 1.0f;

        [Tooltip("Mask value where a pixel is considered part of the object.")]
        [Range(0.0f, 1.0f)]
        public float MaskThreshold = 0.5f;

        [Tooltip("Smooth transition range around the mask threshold.")]
        [Range(0.001f, 0.25f)]
        public float EdgeSoftness = 0.03f;
    }

    public override void Create()
    {
        _pass ??= new OuterMaskOutlinePass();
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData
    )
    {
        OutlineSettings ??= new Settings();

        if (string.IsNullOrWhiteSpace(OutlineSettings.MaskTextureName) ||
            OutlineSettings.OutlineIntensity <= 0.0001f ||
            !_materialCache.Ensure(CompositeMaterial))
        {
            return;
        }

        _materialSets.EnsureCount(
            1,
            _materialCache.Material,
            _materialCache.Version
        );
        _pass.Setup(ProfilingName, OutlineSettings, _materialSets[0]);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _materialSets.Dispose();
        _materialCache.Dispose();
    }
}
