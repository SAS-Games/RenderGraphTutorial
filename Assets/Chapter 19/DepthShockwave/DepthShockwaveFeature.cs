using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class DepthShockwaveFeature : ScriptableRendererFeature
{
    private const string ShaderName = "Hidden/Chapter19/DepthShockwave/Composite";

    [Tooltip("Name used for Render Graph passes and profiler markers.")]
    public string ProfilingName = "Depth Shockwave";

    [Tooltip("Material that uses Hidden/Chapter19/DepthShockwave/Composite.")]
    public Material ShockwaveMaterial;

    public Settings ShockwaveSettings = new();

    private readonly MaskedEffectMaterialCache materialCache =
        new(nameof(DepthShockwaveFeature), ShaderName);

    private FrameColorSnapshotPass sourceSnapshotPass;
    private DepthShockwavePass shockwavePass;

    [Serializable]
    public sealed class Settings
    {
        public bool Enabled = true;
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public bool ApplyToSceneView;

        [Header("Shape")]
        [Range(1, DepthShockwave.MaximumShaderShockwaves)]
        public int MaximumSimultaneousShockwaves = 8;

        [Min(0.01f)]
        public float RingWidth = 0.45f;

        [Min(0.001f)]
        public float EdgeSoftness = 0.25f;

        [Min(0f)]
        public float SecondaryRingOffset = 1.25f;

        [Range(0f, 1f)]
        public float SecondaryRingStrength = 0.35f;

        [Header("Refraction")]
        [Range(0f, 40f)]
        public float DistortionPixels = 14f;

        [Range(0f, 8f)]
        public float ChromaticPixels = 1.5f;

        [Header("Presentation")]
        [ColorUsage(true, true)]
        public Color WaveColor = new(0.1f, 0.8f, 2f, 1f);

        [Range(0f, 5f)]
        public float EmissionIntensity = 1.4f;

        [Range(0f, 2f)]
        public float Intensity = 1f;
    }

    public override void Create()
    {
        sourceSnapshotPass ??= new FrameColorSnapshotPass();
        shockwavePass ??= new DepthShockwavePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        ShockwaveSettings ??= new Settings();

        Camera camera = renderingData.cameraData.camera;
        if (!ShouldRender(camera, renderingData.cameraData.renderType) ||
            !ShockwaveSettings.Enabled ||
            ShockwaveSettings.Intensity <= 0.0001f ||
            !DepthShockwave.HasActiveEvents ||
            !materialCache.Ensure(ShockwaveMaterial))
        {
            return;
        }

        sourceSnapshotPass.Setup($"{ProfilingName} Source", ShockwaveSettings.RenderPassEvent);
        shockwavePass.Setup(
            ProfilingName,
            ShockwaveSettings,
            materialCache.Material,
            sourceSnapshotPass.SnapshotTextureId);

        renderer.EnqueuePass(sourceSnapshotPass);
        renderer.EnqueuePass(shockwavePass);
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
               (ShockwaveSettings.ApplyToSceneView && camera.cameraType == CameraType.SceneView);
    }
}
