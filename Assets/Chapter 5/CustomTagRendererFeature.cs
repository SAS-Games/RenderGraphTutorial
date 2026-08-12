using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CustomTagRendererFeature : ScriptableRendererFeature
{
    class CustomTagPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId CustomShaderTagId = new("CustomOutlineTag");
        private static readonly List<ShaderTagId> StandardShaderTagIds = new()
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("LightweightForward")
        };

        private readonly FilteringSettings filteringSettings;
        private readonly Material overrideMaterial;
        private readonly int overrideMaterialPassIndex;

        class PassData
        {
            public RendererListHandle rendererList;
        }

        public CustomTagPass(
            RenderPassEvent rpEvent,
            LayerMask layerMask,
            Material material,
            int materialPassIndex)
        {
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, layerMask);
            overrideMaterial = material;
            overrideMaterialPassIndex = materialPassIndex;
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

            DrawingSettings drawingSettings;
            if (overrideMaterial == null)
            {
                // Original Chapter 5 path: render a pass supplied by the object's own material.
                drawingSettings = new DrawingSettings(CustomShaderTagId, sortingSettings);
            }
            else
            {
                // Player path: its regular URP materials do not contain CustomOutlineTag, so
                // select their normal forward pass and replace it with the outline pass.
                drawingSettings = new DrawingSettings(StandardShaderTagIds[0], sortingSettings);
                for (int i = 1; i < StandardShaderTagIds.Count; i++)
                {
                    drawingSettings.SetShaderPassName(i, StandardShaderTagIds[i]);
                }

                drawingSettings.overrideMaterial = overrideMaterial;
                drawingSettings.overrideMaterialPassIndex = overrideMaterialPassIndex;
            }

            var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            var rendererList = renderGraph.CreateRendererList(rendererListParams);
            
            using var builder = renderGraph.AddRasterRenderPass<PassData>("Custom MultiPass Execution", out var passData);

            passData.rendererList = rendererList;

            builder.UseRendererList(rendererList);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => { context.cmd.DrawRendererList(data.rendererList); });
        }
    }

    [SerializeField] RenderPassEvent m_RenderPassEvent;
    [SerializeField] LayerMask m_LayerMask = ~0;
    [SerializeField] Material m_OverrideMaterial;
    [SerializeField] int m_OverrideMaterialPassIndex = 1;
    private CustomTagPass _customTagPass;
    

    public override void Create()
    {
        _customTagPass = new CustomTagPass(
            m_RenderPassEvent,
            m_LayerMask,
            m_OverrideMaterial,
            m_OverrideMaterialPassIndex);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_customTagPass);
    }
}
