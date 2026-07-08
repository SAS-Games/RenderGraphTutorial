using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class SimpleMaskOutlineMaterialFeature : ScriptableRendererFeature
{
    public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    public Material Material;
    public int MaterialPassIndex;

    private SimpleMaskOutlineMaterialPass _pass;

    public override void Create()
    {
        _pass = new SimpleMaskOutlineMaterialPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (Material == null)
        {
            return;
        }

        if (MaterialPassIndex < 0 || MaterialPassIndex >= Material.passCount)
        {
            Debug.LogWarning($"{nameof(SimpleMaskOutlineMaterialFeature)} skipped because Material Pass Index is out of range.");
            return;
        }

        _pass.Setup(RenderPassEvent, Material, MaterialPassIndex);
        renderer.EnqueuePass(_pass);
    }

    private class SimpleMaskOutlineMaterialPass : ScriptableRenderPass
    {
        private Material _material;
        private int _materialPassIndex;

        private class PassData
        {
            public Material Material;
            public int MaterialPassIndex;
        }

        public void Setup(RenderPassEvent passEvent, Material material, int materialPassIndex)
        {
            renderPassEvent = passEvent;
            profilingSampler = new ProfilingSampler("Simple Mask Outline Material");
            _material = material;
            _materialPassIndex = materialPassIndex;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null)
            {
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                "Simple Mask Outline Material",
                out PassData passData,
                profilingSampler);

            passData.Material = _material;
            passData.MaterialPassIndex = _materialPassIndex;

            builder.UseAllGlobalTextures(true);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
        }

        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), data.Material, data.MaterialPassIndex);
        }
    }
}
