using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class FrameColorSnapshotPass : ScriptableRenderPass
{
    private static int _nextSnapshotId;

    private readonly int _snapshotTextureId = Shader.PropertyToID(
        $"_FrameColorSnapshot{Interlocked.Increment(ref _nextSnapshotId)}");
    private string _profilingName;

    public int SnapshotTextureId => _snapshotTextureId;

    private sealed class PassData
    {
        public TextureHandle Source;
    }

    public void Setup(string profilingName, RenderPassEvent sourceRenderPassEvent)
    {
        renderPassEvent = sourceRenderPassEvent;
        profilingSampler = MaskedEffectRenderGraphUtility.GetOrCreateProfilingSampler(
            profilingName,
            ref _profilingName,
            profilingSampler);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        FrameTextureRegistry textureRegistry = FrameTextureRegistry.GetOrCreate(frameData);
        if (textureRegistry.TryGetTexture(_snapshotTextureId, out _, out _))
        {
            return;
        }

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 0;
        descriptor.msaaSamples = 1;

        TextureHandle sourceCopy = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph,
            descriptor,
            $"{_profilingName} Texture",
            false,
            FilterMode.Bilinear,
            TextureWrapMode.Clamp);
        Vector4 texelSize = MaskedEffectRenderGraphUtility.CreateTexelSize(
            descriptor.width,
            descriptor.height);
        textureRegistry.SetTexture(_snapshotTextureId, sourceCopy, texelSize);

        using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
            _profilingName,
            out PassData passData,
            profilingSampler);

        passData.Source = resourceData.activeColorTexture;
        builder.UseTexture(passData.Source, AccessFlags.Read);
        builder.SetRenderAttachment(sourceCopy, 0);
        builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
    }

    private static void ExecutePass(PassData data, RasterGraphContext context)
    {
        Blitter.BlitTexture(
            context.cmd,
            data.Source,
            new Vector4(1, 1, 0, 0),
            0.0f,
            false);
    }
}
