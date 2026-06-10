using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace SAS.Rendering
{
    public static class FullscreenMaterialPass
    {
        private class PassData
        {
            public TextureHandle Source;
            public Material Material;
            public int MaterialPassIndex;

            public Action<Material> SetupMaterial;
        }

        /// <summary>
        /// Creates a destination texture automatically and returns it.
        /// </summary>
        public static TextureHandle Execute(
            RenderGraph renderGraph,
            ContextContainer frameData,
            string passName,
            TextureHandle source,
            Material material,
            int materialPassIndex = 0,
            Action<Material> setupMaterial = null)
        {
            UniversalCameraData cameraData =
                frameData.Get<UniversalCameraData>();

            RenderTextureDescriptor desc =
                cameraData.cameraTargetDescriptor;

            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;

            TextureHandle destination =
                UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    desc,
                    passName,
                    false);

            Execute(
                renderGraph,
                passName,
                source,
                destination,
                material,
                materialPassIndex,
                setupMaterial);

            return destination;
        }

        /// <summary>
        /// Uses an existing destination texture.
        /// </summary>
        public static void Execute(
            RenderGraph renderGraph,
            string passName,
            TextureHandle source,
            TextureHandle destination,
            Material material,
            int materialPassIndex = 0,
            Action<Material> setupMaterial = null)
        {
            using IRasterRenderGraphBuilder builder =
                renderGraph.AddRasterRenderPass(
                    passName,
                    out PassData passData);

            passData.Source = source;
            passData.Material = material;
            passData.MaterialPassIndex = materialPassIndex;
            passData.SetupMaterial = setupMaterial;

            builder.UseTexture(source);

            builder.SetRenderAttachment(
                destination,
                0);

            builder.AllowPassCulling(false);

            builder.SetRenderFunc(
                static (PassData data,
                        RasterGraphContext context) =>
                {
                    data.SetupMaterial?.Invoke(
                        data.Material);

                    Blitter.BlitTexture(
                        context.cmd,
                        data.Source,
                        Vector4.one,
                        data.Material,
                        data.MaterialPassIndex);
                });
        }
    }
}