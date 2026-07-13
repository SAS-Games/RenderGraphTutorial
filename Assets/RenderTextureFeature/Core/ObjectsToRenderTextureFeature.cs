using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public partial class ObjectsToRenderTextureFeature : ScriptableRendererFeature
{
    private const string DebugShaderName = "Hidden/RenderTextureFeature/DebugTexture";

    public string ProfilingName = "Render To Texture";
    [FormerlySerializedAs("RenderTextureOutputs")]
    [FormerlySerializedAs("TextureSettings")]
    public List<RenderTexturePass.Settings> RenderTextureOutputSettings = new() { new RenderTexturePass.Settings() };

    private readonly List<RenderTexturePass> _renderPasses = new();
    private readonly List<RenderTextureDebugPass> _debugPasses = new();
    private Material _debugMaterial;

    public override void Create()
    {
        ValidateConfiguration();
        EnsurePassCount(GetRenderTextureOutputSettingsCount());
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        int outputSettingsCount = GetRenderTextureOutputSettingsCount();
        if (outputSettingsCount == 0)
        {
            return;
        }

        EnsurePassCount(outputSettingsCount);

        for (int i = 0; i < outputSettingsCount; i++)
        {
            RenderTexturePass.Settings outputSettings = GetRenderTextureOutputSettings(i);
            if (outputSettings == null || string.IsNullOrWhiteSpace(outputSettings.TextureName))
            {
                continue;
            }

            string passName = GetPassName(outputSettings, i);
            RenderTexturePass renderPass = _renderPasses[i];
            renderPass.Setup(passName, outputSettings);
            renderer.EnqueuePass(renderPass);

            if (!outputSettings.DebugView || !EnsureDebugMaterial())
            {
                continue;
            }

            RenderTextureDebugPass debugPass = _debugPasses[i];
            debugPass.Setup($"{passName} Debug", outputSettings, _debugMaterial);
            renderer.EnqueuePass(debugPass);
        }
    }

    private bool EnsureDebugMaterial()
    {
        if (_debugMaterial == null)
        {
            _debugMaterial = CoreUtils.CreateEngineMaterial(DebugShaderName);
        }

        return _debugMaterial != null;
    }

    private void EnsurePassCount(int count)
    {
        while (_renderPasses.Count < count)
        {
            _renderPasses.Add(new RenderTexturePass());
        }

        while (_debugPasses.Count < count)
        {
            _debugPasses.Add(new RenderTextureDebugPass());
        }
    }

    private int GetRenderTextureOutputSettingsCount()
    {
        return RenderTextureOutputSettings?.Count ?? 0;
    }

    private RenderTexturePass.Settings GetRenderTextureOutputSettings(int index)
    {
        return RenderTextureOutputSettings[index];
    }

    private string GetPassName(RenderTexturePass.Settings settings, int index)
    {
        string textureName = string.IsNullOrWhiteSpace(settings.TextureName)
            ? $"Texture {index}"
            : settings.TextureName;

        return $"{ProfilingName} ({textureName})";
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_debugMaterial);
    }
}
