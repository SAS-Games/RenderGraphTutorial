using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class FrameTextureProcessingFeature : ScriptableRendererFeature
{
    [Tooltip("Base name used for profiling markers and generated processing pass names.")]
    public string ProfilingName = "Frame Texture Processing";

    [Tooltip("One optional fullscreen material operation per entry. Entries run in list order when they use the same Render Pass Event.")]
    public List<Settings> ProcessingSettings = new() { new Settings() };

    private readonly List<FrameTextureProcessingPass> _passes = new();

    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Disables this processing entry without removing its setup.")]
        public bool Enabled = true;

        [Tooltip("Inspector and profiling label for this operation.")]
        public string Name = "Process Texture";

        [Tooltip("When this operation runs. The input producer must run earlier; entries at the same event follow renderer-feature and list order.")]
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        [Tooltip("Registered texture name to read, for example _SelectionOutlineMask.")]
        public string InputTextureName = "_InputTexture";

        [Tooltip("Name used to register and globally publish the processed texture. This may match Input Texture Name to replace that logical texture for later consumers.")]
        public string OutputTextureName = "_ProcessedTexture";

        [Tooltip("Fullscreen material that reads the URP _BlitTexture input and writes the processed result.")]
        public Material ProcessingMaterial;

        [Tooltip("Single shader pass executed from Processing Material. A value outside the material's pass range skips this entry and reports an error.")]
        [Min(0)]
        public int MaterialPassIndex;

        [Tooltip("Output resolution relative to the registered input. 1 preserves its size, 0.5 creates half resolution, and 2 creates double resolution.")]
        [Range(0.1f, 2.0f)]
        public float OutputScale = 1.0f;

        [Tooltip("Sampling mode stored on the processed output for later consumers. Bilinear is suitable for soft data; Point preserves discrete mask or id values.")]
        public FilterMode OutputFilterMode = FilterMode.Bilinear;

        [Tooltip("Sampling behavior outside the processed output's 0-1 UV range. Clamp is recommended for screen-space textures.")]
        public TextureWrapMode OutputWrapMode = TextureWrapMode.Clamp;
    }

    public override void Create()
    {
        EnsurePassCount(ProcessingSettings?.Count ?? 0);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ProcessingSettings == null)
        {
            return;
        }

        EnsurePassCount(ProcessingSettings.Count);

        for (int i = 0; i < ProcessingSettings.Count; i++)
        {
            Settings settings = ProcessingSettings[i];
            if (!ShouldEnqueue(settings))
            {
                continue;
            }

            FrameTextureProcessingPass pass = _passes[i];
            pass.Setup(ProfilingName, settings, i);
            renderer.EnqueuePass(pass);
        }
    }

    private void EnsurePassCount(int count)
    {
        while (_passes.Count < count)
        {
            _passes.Add(new FrameTextureProcessingPass());
        }
    }

    private static bool ShouldEnqueue(Settings settings)
    {
        return settings != null &&
               settings.Enabled &&
               settings.ProcessingMaterial != null &&
               !string.IsNullOrWhiteSpace(settings.InputTextureName) &&
               !string.IsNullOrWhiteSpace(settings.OutputTextureName);
    }
}
