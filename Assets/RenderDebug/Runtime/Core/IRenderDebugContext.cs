using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SAS.RenderDebugging
{
    /// <summary>
    /// Small effect-facing contract for registering metadata and conditionally publishing frame textures.
    /// </summary>
    public interface IRenderDebugContext
    {
        /// <summary>Gets whether a viewer currently permits live debug work.</summary>
        bool IsEnabled { get; }

        bool RegisterSource(IRenderDebugSource source);
        bool RegisterStage(string sourceId, in RenderDebugStage stage);
        bool IsStageRequested(string sourceId, string stageId);
        void UnregisterSource(string sourceId, IRenderDebugSource owner = null);

        /// <summary>
        /// Publishes a caller-owned persistent Texture. Use the RenderGraph or RTHandle overload for transient resources.
        /// </summary>
        void PublishTexture(
            string sourceId,
            in RenderDebugStage stage,
            Texture texture,
            Camera camera = null);

        /// <summary>Schedules an owned debug copy of an RTHandle on the supplied command buffer.</summary>
        void PublishRTHandle(
            CommandBuffer commandBuffer,
            string sourceId,
            in RenderDebugStage stage,
            RTHandle texture,
            in RenderTextureDescriptor descriptor,
            Camera camera = null);

        /// <summary>
        /// Adds a RenderGraph copy into debugger-owned external storage. The transient handle is never retained.
        /// </summary>
        void PublishRenderGraphTexture(
            RenderGraph renderGraph,
            string sourceId,
            in RenderDebugStage stage,
            TextureHandle texture,
            in RenderTextureDescriptor descriptor,
            Camera camera = null);
    }
}
