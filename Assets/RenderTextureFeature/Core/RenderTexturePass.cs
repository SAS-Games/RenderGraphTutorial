using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;


public partial class RenderTexturePass  : ScriptableRenderPass
{
    // Compatibility alias for older consumers. New code should use FrameTextureRegistry.
    public sealed class CustomTextureData : FrameTextureRegistry
    {
        public TextureHandle Texture { get; private set; }
        public Vector4 TexelSize { get; private set; }

        public override void Reset()
        {
            base.Reset();
            Texture = TextureHandle.nullHandle;
            TexelSize = Vector4.zero;
        }

        public override void SetTexture(
            int texturePropertyId,
            TextureHandle texture,
            Vector4 texelSize)
        {
            base.SetTexture(texturePropertyId, texture, texelSize);
            Texture = texture;
            TexelSize = texelSize;
        }
    }

    private Settings _settings;
    private string _profilingName;
    private string _textureName;
    private int _texturePropertyId;
    private int _texelSizePropertyId;
    private RenderStateBlock _renderStateBlock;
    private bool _loggedSkippedDepthAttachment;

    private class PassData
    {
        public RendererListHandle RendererListHandle;
        public Settings.GlobalKeyword[] GlobalKeywords;
        public int TexturePropertyId;
        public int TexelSizePropertyId;
        public Vector4 TexelSize;
    }

    public void Setup(string profilingName, Settings settings)
    {
        _settings = settings;
        if (_textureName != settings.TextureName)
        {
            _textureName = settings.TextureName;
            _texturePropertyId = Shader.PropertyToID(settings.TextureName);
            _texelSizePropertyId = Shader.PropertyToID($"{settings.TextureName}_TexelSize");
        }
        renderPassEvent = _settings.RenderPassEvent;
        profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(
            profilingName,
            ref _profilingName,
            profilingSampler);
        _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        // Debug.Log($"RenderTexturePass: depth? {_settings.Depth}");
        if (_settings.Depth)
        {
            _renderStateBlock.mask |= RenderStateMask.Depth;
            bool writeEnabled = _settings.WriteDepth;
            CompareFunction function = _settings.DepthCompare;
            _renderStateBlock.depthState = new DepthState(writeEnabled, function);
        }
        ConfigureInput(settings.RenderPassInput);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            _settings.TextureName,
            out PassData passData,
            profilingSampler
        );

        // Initialize the pass data
        InitPassData(renderGraph, frameData, ref passData);
        // Create the destination texture
        TextureHandle destination = CreateDestinationTexture(
            renderGraph,
            frameData,
            out RenderTextureDescriptor destinationDescriptor,
            out RenderTextureDescriptor cameraDescriptor
        );
        passData.TexturePropertyId = _texturePropertyId;
        passData.TexelSizePropertyId = _texelSizePropertyId;
        passData.TexelSize = CreateTexelSize(destinationDescriptor.width, destinationDescriptor.height);
        // Make sure the renderer list is valid
        if (!passData.RendererListHandle.IsValid())
            return;

        FrameTextureRegistry customData = FrameTextureRegistry.GetOrCreate(frameData);
        customData.SetTexture(passData.TexturePropertyId, destination, passData.TexelSize);

        // We declare the RendererList we just created as an input dependency to this pass, via UseRendererList()
        builder.UseRendererList(passData.RendererListHandle);

        // Setup as a render target via UseTextureFragment, which is the equivalent of using the old cmd.SetRenderTarget
        builder.SetRenderAttachment(destination, 0);
        SetDepthAttachment(builder, frameData, destinationDescriptor, cameraDescriptor);
        builder.SetGlobalTextureAfterPass(destination, passData.TexturePropertyId);

        // Shader keyword changes are considered as global state modifications
        builder.AllowGlobalStateModification(true);
       // builder.AllowPassCulling(true);

        // Assign the ExecutePass function to the render pass delegate, which will be called by the render graph when executing the pass
        builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
    }

    // This static method is used to execute the pass and passed as the RenderFunc delegate to the RenderGraph render pass
    private static void ExecutePass(PassData data, RasterGraphContext context)
    {
        UpdateKeywordsBeforeRender(data, context.cmd);

        context.cmd.SetGlobalVector(data.TexelSizePropertyId, data.TexelSize);
        context.cmd.ClearRenderTarget(RTClearFlags.Color, Color.black, 0, 0);

        context.cmd.DrawRendererList(data.RendererListHandle);

        UpdateKeywordsAfterRender(data, context.cmd);
    }

    private static void UpdateKeywordsBeforeRender(PassData data, RasterCommandBuffer cmd)
    {
        if (data.GlobalKeywords == null)
        {
            return;
        }
        foreach (Settings.GlobalKeyword keyword in data.GlobalKeywords)
        {
            if (keyword.Disabled)
            {
                continue;
            }
            switch (keyword.BeforeRenderMode)
            {
                case Settings.GlobalKeyword.Mode.None:
                    break;
                case Settings.GlobalKeyword.Mode.Enable:
                    cmd.EnableShaderKeyword(keyword.Name);
                    break;
                case Settings.GlobalKeyword.Mode.Disable:
                    cmd.DisableShaderKeyword(keyword.Name);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static void UpdateKeywordsAfterRender(PassData data, RasterCommandBuffer cmd)
    {
        if (data.GlobalKeywords == null)
        {
            return;
        }
        foreach (Settings.GlobalKeyword keyword in data.GlobalKeywords)
        {
            if (keyword.Disabled)
            {
                continue;
            }
            switch (keyword.AfterRenderMode)
            {
                case Settings.GlobalKeyword.Mode.None:
                    break;
                case Settings.GlobalKeyword.Mode.Enable:
                    cmd.EnableShaderKeyword(keyword.Name);
                    break;
                case Settings.GlobalKeyword.Mode.Disable:
                    cmd.DisableShaderKeyword(keyword.Name);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void InitPassData(RenderGraph renderGraph, ContextContainer frameData, ref PassData passData)
    {
        var universalRenderingData = frameData.Get<UniversalRenderingData>();
        var cameraData = frameData.Get<UniversalCameraData>();
        var lightData = frameData.Get<UniversalLightData>();

        DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
            _settings.LightModeShaderTags,
            universalRenderingData,
            cameraData,
            lightData,
            _settings.SortingCriteria
        );
        drawingSettings.overrideMaterial = _settings.Material;
        drawingSettings.overrideMaterialPassIndex = _settings.MaterialPassIndex;

        var filteringSettings = new FilteringSettings(_settings.RenderQueueRange, _settings.LayerMask, (uint)_settings.RenderLayerMask);
       
        RendererListHandle renderListHandle = RenderingHelpers.CreateRendererListWithRenderStateBlock(
            renderGraph,
            ref universalRenderingData.cullResults,
            drawingSettings,
            filteringSettings,
            _renderStateBlock
        );

        passData.RendererListHandle = renderListHandle;
        passData.GlobalKeywords = _settings.GlobalShaderKeywords;
    }

    private TextureHandle CreateDestinationTexture(
        RenderGraph renderGraph,
        ContextContainer frameData,
        out RenderTextureDescriptor desc,
        out RenderTextureDescriptor cameraDescriptor
    )
    {
        var cameraData = frameData.Get<UniversalCameraData>();
        cameraDescriptor = cameraData.cameraTargetDescriptor;
        desc = cameraDescriptor;
        desc.colorFormat = _settings.ColorFormat;
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;
        ApplyTextureSize(ref desc);
        TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, _settings.TextureName, false, _settings.FilterMode, _settings.WrapMode);
        return destination;
    }

    private void SetDepthAttachment(
        IRasterRenderGraphBuilder builder,
        ContextContainer frameData,
        RenderTextureDescriptor destinationDescriptor,
        RenderTextureDescriptor cameraDescriptor
    )
    {
        if (!_settings.Depth)
        {
            return;
        }

        if (!CanUseCameraDepthAttachment(destinationDescriptor, cameraDescriptor))
        {
            LogSkippedDepthAttachmentOnce(destinationDescriptor, cameraDescriptor);
            return;
        }

        var resourceData = frameData.Get<UniversalResourceData>();
        AccessFlags depthAccess = _settings.WriteDepth ? AccessFlags.ReadWrite : AccessFlags.Read;
        builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, depthAccess);
    }

    private bool CanUseCameraDepthAttachment(RenderTextureDescriptor destinationDescriptor, RenderTextureDescriptor cameraDescriptor)
    {
        return _settings.TextureSizeMode == Settings.SizeMode.Camera
            && destinationDescriptor.width == cameraDescriptor.width
            && destinationDescriptor.height == cameraDescriptor.height
            && destinationDescriptor.volumeDepth == cameraDescriptor.volumeDepth;
    }

    private void LogSkippedDepthAttachmentOnce(RenderTextureDescriptor destinationDescriptor, RenderTextureDescriptor cameraDescriptor)
    {
        if (_loggedSkippedDepthAttachment)
        {
            return;
        }

        Debug.LogWarning(
            $"{nameof(RenderTexturePass)} skipped camera depth attachment for '{_settings.TextureName}' because the output size " +
            $"{destinationDescriptor.width}x{destinationDescriptor.height} does not match the camera depth size " +
            $"{cameraDescriptor.width}x{cameraDescriptor.height}. Use Camera Size Multiplier 1, or disable Depth, when this output must share the active camera depth texture."
        );
        _loggedSkippedDepthAttachment = true;
    }

    private void ApplyTextureSize(ref RenderTextureDescriptor desc)
    {
        switch (_settings.TextureSizeMode)
        {
            case Settings.SizeMode.Camera:
                ApplyCameraTextureSize(ref desc);
                break;
            case Settings.SizeMode.Custom:
                ApplyCustomTextureSize(ref desc);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ApplyCameraTextureSize(ref RenderTextureDescriptor desc)
    {
        float sizeMultiplier = Mathf.Clamp(_settings.CameraSizeMultiplier, 0.0f, 2.0f);
        if (Mathf.Approximately(sizeMultiplier, 1.0f))
        {
            return;
        }

        desc.width = Mathf.Max(1, Mathf.RoundToInt(desc.width * sizeMultiplier));
        desc.height = Mathf.Max(1, Mathf.RoundToInt(desc.height * sizeMultiplier));
    }

    private void ApplyCustomTextureSize(ref RenderTextureDescriptor desc)
    {
        desc.width = Mathf.Max(1, _settings.TextureSize.x);
        desc.height = Mathf.Max(1, _settings.TextureSize.y);
        desc.useDynamicScale = false;
        desc.useDynamicScaleExplicit = false;
    }

    private static Vector4 CreateTexelSize(int width, int height)
    {
        return new Vector4(1.0f / width, 1.0f / height, width, height);
    }
}
