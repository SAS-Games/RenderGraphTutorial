using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class SelectiveMotionBlurFeature : ScriptableRendererFeature
{
    private const string ShaderName = "Hidden/Chapter18/SelectiveMotionBlur/Composite";

    [Tooltip("Name used for Render Graph passes and profiler markers.")]
    public string ProfilingName = "Selective Motion Blur";

    [Tooltip("Material that uses Hidden/Chapter18/SelectiveMotionBlur/Composite.")]
    public Material MotionBlurMaterial;

    [Tooltip("Selection, velocity dilation, and presentation settings.")]
    public Settings MotionBlurSettings = new();

    private readonly MaskedEffectMaterialCache materialCache = new(nameof(SelectiveMotionBlurFeature), ShaderName);
    private readonly MaskedEffectItemPool<SelectiveMotionBlurMaterialSet> materialPool = new(source => new SelectiveMotionBlurMaterialSet(source));

    private FrameColorSnapshotPass sourceSnapshotPass;
    private SelectiveMotionBlurPass motionBlurPass;

    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Disables the effect without removing the renderer feature.")]
        public bool Enabled = true;

        [Tooltip("When velocity processing and compositing run. The selection mask must already exist.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Texture produced by ObjectsToRenderTextureFeature for objects allowed to blur.")]
        public string MaskTextureName = "_SelectiveMotionBlurMask";

        [Tooltip("Run for Scene view cameras as well as Game cameras.")]
        public bool ApplyToSceneView;

        [Header("Velocity")]
        [Tooltip("Width and height of the velocity tiles in source pixels. Larger tiles are faster and spread velocity farther, but can increase background bleeding.")]
        [Range(4, 16)] public int TileSize = 8;

        [Tooltip("Virtual shutter duration in seconds. Because motion vectors are normalized by frame time, this produces similar blur lengths at different refresh rates.")]
        [Range(0f, 0.05f)] public float ShutterDuration = 1f / 30f;

        [Tooltip("Multiplier applied after frame-rate compensation. Unity's built-in object motion blur uses a comparable 2x UV-space scale.")]
        [Range(0f, 4f)] public float MotionVectorScale = 2f;

        [Tooltip("Maximum blur length in screen pixels after shutter scaling.")]
        [Range(0f, 96f)] public float MaxBlurPixels = 48f;

        [Header("Selection")]
        [Tooltip("Mask value where selected-object motion blur begins.")]
        [Range(0f, 1f)] public float MaskThreshold = 0.45f;

        [Tooltip("Softens selection-mask edges and the ends of velocity streaks.")]
        [Range(0.0001f, 0.5f)] public float MaskSoftness = 0.05f;

        [Header("Presentation")]
        [Tooltip("Number of scene-color samples used along the velocity line.")]
        [Range(4, 24)] public int SampleCount = 16;

        [Tooltip("Overall blend strength of the selective blur.")]
        [Range(0f, 1f)] public float Intensity = 1f;
    }

    public override void Create()
    {
        sourceSnapshotPass ??= new FrameColorSnapshotPass();
        motionBlurPass ??= new SelectiveMotionBlurPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        MotionBlurSettings ??= new Settings();

        Camera camera = renderingData.cameraData.camera;
        if (!ShouldRender(camera, renderingData.cameraData.renderType) ||
            !MotionBlurSettings.Enabled ||
            MotionBlurSettings.Intensity <= 0.0001f ||
            MotionBlurSettings.ShutterDuration <= 0.00001f ||
            MotionBlurSettings.MaxBlurPixels <= 0.0001f ||
            string.IsNullOrWhiteSpace(MotionBlurSettings.MaskTextureName) ||
            !materialCache.Ensure(MotionBlurMaterial))
        {
            return;
        }

        materialPool.EnsureCount(1, materialCache.Material, materialCache.Version);
        SelectiveMotionBlurMaterialSet materials = materialPool[0];
        if (materials == null || !materials.IsValid)
            return;

        sourceSnapshotPass.Setup($"{ProfilingName} Source", MotionBlurSettings.RenderPassEvent);
        motionBlurPass.Setup(
            ProfilingName,
            MotionBlurSettings,
            materials,
            sourceSnapshotPass.SnapshotTextureId);

        renderer.EnqueuePass(sourceSnapshotPass);
        renderer.EnqueuePass(motionBlurPass);
    }

    protected override void Dispose(bool disposing)
    {
        materialPool.Dispose();
        materialCache.Dispose();
    }

    private bool ShouldRender(Camera camera, CameraRenderType renderType)
    {
        if (camera == null || renderType != CameraRenderType.Base)
            return false;

        return camera.cameraType == CameraType.Game || (MotionBlurSettings.ApplyToSceneView && camera.cameraType == CameraType.SceneView);
    }
}
