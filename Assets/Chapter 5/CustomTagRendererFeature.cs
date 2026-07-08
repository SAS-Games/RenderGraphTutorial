using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CustomTagRendererFeature : ScriptableRendererFeature
{
    class CustomTagPass : ScriptableRenderPass
    {
        private readonly ShaderTagId customShaderTagId = new ShaderTagId("CustomOutlineTag");
        private FilteringSettings filteringSettings;

        class PassData
        {
            public RendererListHandle rendererList;
        }

        public CustomTagPass(RenderPassEvent rpEvent)
        {
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            renderPassEvent = rpEvent;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            var sortingSettings = new SortingSettings(cameraData.camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };

            var drawingSettings = new DrawingSettings(customShaderTagId, sortingSettings);
            var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            var rendererList = renderGraph.CreateRendererList(rendererListParams);
            
            using var builder = renderGraph.AddRasterRenderPass<PassData>("Custom MultiPass Execution", out var passData);

            passData.rendererList = rendererList;

            builder.UseRendererList(rendererList);
            builder.SetRenderAttachment(resourceData.cameraColor, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(resourceData.cameraDepth, AccessFlags.Write);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => { context.cmd.DrawRendererList(data.rendererList); });
        }
    }

    [SerializeField] RenderPassEvent m_RenderPassEvent;
    private CustomTagPass _customTagPass;
    

    public override void Create()
    {
        _customTagPass = new CustomTagPass(m_RenderPassEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_customTagPass);
    }
}