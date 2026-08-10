using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class FrameTextureProcessingPass : ScriptableRenderPass
{
    private readonly FrameTextureResolver _inputResolver =
        new(nameof(FrameTextureProcessingFeature));

    private FrameTextureProcessingFeature.Settings _settings;
    private string _featureProfilingName;
    private string _settingsName;
    private string _profilingName;
    private int _settingsIndex = -1;
    private string _outputTextureName;
    private int _outputTextureId;
    private Material _validatedMaterial;
    private int _validatedPassIndex = -1;
    private bool _loggedInvalidMaterialPass;
    private bool _loggedUnsupportedInput;

    private sealed class PassData
    {
        public TextureHandle Source;
        public Material Material;
        public int MaterialPassIndex;
    }

    public void Setup(
        string featureProfilingName,
        FrameTextureProcessingFeature.Settings settings,
        int settingsIndex)
    {
        _settings = settings;
        _inputResolver.SetTextureName(settings.InputTextureName);
        UpdateOutputTexture(settings.OutputTextureName);
        UpdateProfilingName(featureProfilingName, settings.Name, settingsIndex);
        ResetValidationLogsIfNeeded(settings.ProcessingMaterial, settings.MaterialPassIndex);

        renderPassEvent = settings.RenderPassEvent;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_settings == null ||
            !IsMaterialPassValid() ||
            !_inputResolver.TryResolve(frameData, out TextureHandle source, out Vector4 sourceTexelSize))
        {
            return;
        }

        TextureDesc outputDescriptor = source.GetDescriptor(renderGraph);
        if (outputDescriptor.colorFormat == GraphicsFormat.None)
        {
            LogUnsupportedInputOnce();
            return;
        }

        int sourceWidth = Mathf.Max(1, Mathf.RoundToInt(sourceTexelSize.z));
        int sourceHeight = Mathf.Max(1, Mathf.RoundToInt(sourceTexelSize.w));
        float outputScale = Mathf.Clamp(_settings.OutputScale, 0.1f, 2.0f);
        int outputWidth = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * outputScale));
        int outputHeight = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * outputScale));

        ConfigureOutputDescriptor(ref outputDescriptor, outputWidth, outputHeight);
        TextureHandle destination = renderGraph.CreateTexture(outputDescriptor);
        Vector4 outputTexelSize = MaskedEffectRenderGraphUtility.CreateTexelSize(
            outputWidth,
            outputHeight);

        FrameTextureRegistry textureRegistry = FrameTextureRegistry.GetOrCreate(frameData);
        textureRegistry.SetTexture(_outputTextureId, destination, outputTexelSize);

        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            _profilingName,
            out PassData passData,
            profilingSampler);

        passData.Source = source;
        passData.Material = _settings.ProcessingMaterial;
        passData.MaterialPassIndex = _settings.MaterialPassIndex;

        builder.UseTexture(source, AccessFlags.Read);
        builder.SetRenderAttachment(destination, 0);
        builder.SetGlobalTextureAfterPass(destination, _outputTextureId);
        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            ExecutePass(data, context));
    }

    private void ConfigureOutputDescriptor(
        ref TextureDesc descriptor,
        int outputWidth,
        int outputHeight)
    {
        descriptor.name = _outputTextureName;
        descriptor.sizeMode = TextureSizeMode.Explicit;
        descriptor.width = outputWidth;
        descriptor.height = outputHeight;
        descriptor.scale = Vector2.one;
        descriptor.func = null;
        descriptor.depthBufferBits = DepthBits.None;
        descriptor.msaaSamples = MSAASamples.None;
        descriptor.bindTextureMS = false;
        descriptor.enableRandomWrite = false;
        descriptor.useMipMap = false;
        descriptor.autoGenerateMips = false;
        descriptor.useDynamicScale = false;
        descriptor.useDynamicScaleExplicit = false;
        descriptor.clearBuffer = false;
        descriptor.filterMode = _settings.OutputFilterMode;
        descriptor.wrapMode = _settings.OutputWrapMode;
    }

    private bool IsMaterialPassValid()
    {
        Material material = _settings.ProcessingMaterial;
        int passIndex = _settings.MaterialPassIndex;
        if (material != null && passIndex >= 0 && passIndex < material.passCount)
        {
            return true;
        }

        if (!_loggedInvalidMaterialPass)
        {
            string materialName = material == null ? "None" : material.name;
            int passCount = material == null ? 0 : material.passCount;
            Debug.LogError(
                $"{nameof(FrameTextureProcessingFeature)} skipped '{_profilingName}' because material " +
                $"'{materialName}' has {passCount} shader passes, but pass index {passIndex} was requested.");
            _loggedInvalidMaterialPass = true;
        }

        return false;
    }

    private void UpdateOutputTexture(string outputTextureName)
    {
        if (_outputTextureName == outputTextureName)
        {
            return;
        }

        _outputTextureName = outputTextureName;
        _outputTextureId = Shader.PropertyToID(outputTextureName);
        _loggedUnsupportedInput = false;
    }

    private void UpdateProfilingName(
        string featureProfilingName,
        string settingsName,
        int settingsIndex)
    {
        if (_featureProfilingName == featureProfilingName &&
            _settingsName == settingsName &&
            _settingsIndex == settingsIndex)
        {
            return;
        }

        _featureProfilingName = featureProfilingName;
        _settingsName = settingsName;
        _settingsIndex = settingsIndex;

        string operationName = string.IsNullOrWhiteSpace(settingsName)
            ? $"Operation {settingsIndex}"
            : settingsName;
        string profilingName = string.IsNullOrWhiteSpace(featureProfilingName)
            ? operationName
            : $"{featureProfilingName} ({operationName})";

        profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(
            profilingName,
            ref _profilingName,
            profilingSampler);
    }

    private void ResetValidationLogsIfNeeded(Material material, int passIndex)
    {
        if (_validatedMaterial == material && _validatedPassIndex == passIndex)
        {
            return;
        }

        _validatedMaterial = material;
        _validatedPassIndex = passIndex;
        _loggedInvalidMaterialPass = false;
    }

    private void LogUnsupportedInputOnce()
    {
        if (_loggedUnsupportedInput)
        {
            return;
        }

        Debug.LogError(
            $"{nameof(FrameTextureProcessingFeature)} skipped '{_profilingName}' because registered texture " +
            $"'{_inputResolver.TextureName}' is not a color texture. This utility does not process depth textures.");
        _loggedUnsupportedInput = true;
    }

    private static void ExecutePass(PassData data, RasterGraphContext context)
    {
        Blitter.BlitTexture(
            context.cmd,
            data.Source,
            new Vector4(1, 1, 0, 0),
            data.Material,
            data.MaterialPassIndex);
    }
}
