using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;


public partial class RenderTexturePass  : ScriptableRenderPass
{
    public class CustomTextureData : ContextItem
    {
        public TextureHandle Texture;

        public override void Reset()
        {
            Texture = TextureHandle.nullHandle;
        }
    }
    
    private Settings _settings;
    private RenderTextureDescriptor _descriptor;

    private RenderStateBlock _renderStateBlock;

    private class PassData
    {
        public RendererListHandle RendererListHandle;
        public Settings.GlobalKeyword[] GlobalKeywords;
    }

    public void Setup(string profilingName, Settings settings)
    {
        _settings = settings;
        renderPassEvent = _settings.RenderPassEvent;
        profilingSampler = new ProfilingSampler(profilingName);
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
        TextureHandle destination = CreateDestinationTexture(renderGraph, frameData);
        // Make sure the renderer list is valid
        if (!passData.RendererListHandle.IsValid())
            return;

        var customData = frameData.Create<CustomTextureData>();
        customData.Texture = destination;

        // We declare the RendererList we just created as an input dependency to this pass, via UseRendererList()
        builder.UseRendererList(passData.RendererListHandle);

        // Setup as a render target via UseTextureFragment, which is the equivalent of using the old cmd.SetRenderTarget
        builder.SetRenderAttachment(destination, 0);
        builder.SetGlobalTextureAfterPass(destination, Shader.PropertyToID(_settings.TextureName));

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

        context.cmd.ClearRenderTarget(RTClearFlags.ColorDepth, Color.black, 0, 0);

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

    private TextureHandle CreateDestinationTexture(RenderGraph renderGraph, ContextContainer frameData)
    {
        var cameraData = frameData.Get<UniversalCameraData>();
        RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
        desc.colorFormat = _settings.ColorFormat;
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;
        TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, _settings.TextureName, false);
        return destination;
    }
}