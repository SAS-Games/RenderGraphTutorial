using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class TemporalTrailFeature : ScriptableRendererFeature
{
    private const string CompositeShaderName = "Hidden/Chapter17/TemporalTrail/Composite";

    [Tooltip("Name used for Render Graph passes and profiler markers.")]
    public string ProfilingName = "Temporal Trail";

    [Tooltip("Material that uses Hidden/Chapter17/TemporalTrail/Composite.")]
    public Material CompositeMaterial;

    [Tooltip("Selection, history, and presentation settings.")]
    public Settings TrailSettings = new();

    private readonly MaskedEffectMaterialCache materialCache =
        new(nameof(TemporalTrailFeature), CompositeShaderName);

    private FrameColorSnapshotPass sourceSnapshotPass;
    private TemporalTrailPass temporalTrailPass;

    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Disables the effect without removing the renderer feature.")]
        public bool Enabled = true;

        [Tooltip("When accumulation and compositing run. The selection mask must already exist.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Texture produced by RenderObjectsToTextureFeature for trail sources.")]
        public string MaskTextureName = "_TemporalTrailMask";

        [Tooltip("Run for Scene view cameras as well as Game cameras.")]
        public bool ApplyToSceneView;

        [Header("History")]
        [Tooltip("Fraction of camera resolution used by the persistent history buffers.")]
        [Range(0.25f, 1f)]
        public float HistoryResolutionScale = 0.5f;

        [Tooltip("Seconds required for an abandoned trail to lose half of its strength.")]
        [Min(0.01f)]
        public float HalfLife = 0.15f;

        [Tooltip("Seconds between new trail snapshots. Zero captures continuously for the smoothest motion; non-zero values intentionally create discrete afterimages.")]
        [Min(0f)]
        public float CaptureInterval;

        [Tooltip("Reprojects history using URP motion vectors before accumulation.")]
        public bool MotionCompensation = true;

        [Tooltip("Multiplier applied to the sampled motion vector.")]
        [Range(0f, 2f)]
        public float MotionVectorScale = 1f;

        [Tooltip("Discard history after a camera position jump larger than this distance.")]
        [Min(0f)]
        public float CameraCutDistance = 2f;

        [Tooltip("Discard history after a camera rotation jump larger than this angle.")]
        [Range(0f, 180f)]
        public float CameraCutAngle = 35f;

        [Header("Selection")]
        [Tooltip("Mask value where the current object begins contributing to history.")]
        [Range(0f, 1f)]
        public float MaskThreshold = 0.45f;

        [Tooltip("Softens the current object's silhouette in the accumulation buffer.")]
        [Range(0.0001f, 0.5f)]
        public float MaskSoftness = 0.04f;

        [Header("Presentation")]
        [Tooltip("HDR color multiplied into the stored trail.")]
        [ColorUsage(true, true)]
        public Color TrailColor = new(0.15f, 0.8f, 2f, 1f);

        [Tooltip("Overall trail contribution added to the camera color.")]
        [Range(0f, 3f)]
        public float Intensity = 1.15f;

        [Tooltip("Hides trail color beneath the object's current silhouette.")]
        [Range(0f, 1f)]
        public float SuppressCurrentFrame = 1f;

        [Tooltip("Expands current-frame suppression by this many mask texels. Increase slightly when lower-resolution history leaves a fringe around a stationary subject.")]
        [Range(0f, 4f)]
        public float SuppressionRadius = 1.5f;
    }

    public override void Create()
    {
        sourceSnapshotPass ??= new FrameColorSnapshotPass();
        temporalTrailPass ??= new TemporalTrailPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        TrailSettings ??= new Settings();

        Camera camera = renderingData.cameraData.camera;
        if (!ShouldRender(camera, renderingData.cameraData.renderType) ||
            !TrailSettings.Enabled ||
            TrailSettings.Intensity <= 0.0001f ||
            string.IsNullOrWhiteSpace(TrailSettings.MaskTextureName) ||
            !materialCache.Ensure(CompositeMaterial))
        {
            return;
        }

        sourceSnapshotPass.Setup($"{ProfilingName} Source", TrailSettings.RenderPassEvent);
        temporalTrailPass.Setup(
            ProfilingName,
            TrailSettings,
            materialCache.Material,
            sourceSnapshotPass.SnapshotTextureId);

        renderer.EnqueuePass(sourceSnapshotPass);
        renderer.EnqueuePass(temporalTrailPass);
    }

    protected override void Dispose(bool disposing)
    {
        materialCache.Dispose();
    }

    private bool ShouldRender(Camera camera, CameraRenderType renderType)
    {
        if (camera == null || renderType != CameraRenderType.Base)
            return false;

        return camera.cameraType == CameraType.Game ||
               (TrailSettings.ApplyToSceneView && camera.cameraType == CameraType.SceneView);
    }

    internal static RenderTextureDescriptor CreateHistoryDescriptor(
        RenderTextureDescriptor cameraDescriptor,
        float resolutionScale)
    {
        float scale = Mathf.Clamp(resolutionScale, 0.25f, 1f);
        cameraDescriptor.width = Mathf.Max(1, Mathf.RoundToInt(cameraDescriptor.width * scale));
        cameraDescriptor.height = Mathf.Max(1, Mathf.RoundToInt(cameraDescriptor.height * scale));
        cameraDescriptor.depthBufferBits = 0;
        cameraDescriptor.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
        cameraDescriptor.msaaSamples = 1;
        cameraDescriptor.bindMS = false;
        cameraDescriptor.useMipMap = false;
        cameraDescriptor.autoGenerateMips = false;
        cameraDescriptor.enableRandomWrite = false;
        cameraDescriptor.graphicsFormat = SystemInfo.IsFormatSupported(
            GraphicsFormat.R16G16B16A16_SFloat,
            GraphicsFormatUsage.Render)
            ? GraphicsFormat.R16G16B16A16_SFloat
            : GraphicsFormat.R8G8B8A8_UNorm;
        return cameraDescriptor;
    }
}
