using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SAS.RenderDebugging
{
    internal sealed class RenderDebugContext : IRenderDebugContext
    {
        private readonly RenderDebugSession _session;

        public RenderDebugContext(RenderDebugSession session)
        {
            _session = session;
        }

        public bool IsEnabled => _session.IsLiveWorkEnabled;

        public bool RegisterSource(IRenderDebugSource source)
        {
            return _session.Registry.RegisterSource(source);
        }

        public bool RegisterStage(string sourceId, in RenderDebugStage stage)
        {
            return _session.Registry.RegisterStage(sourceId, stage);
        }

        public bool IsStageRequested(string sourceId, string stageId)
        {
            return IsEnabled && _session.Registry.IsStageRequested(sourceId, stageId);
        }

        public void UnregisterSource(string sourceId, IRenderDebugSource owner = null)
        {
            _session.Registry.UnregisterSource(sourceId, owner);
        }

        public void PublishTexture(
            string sourceId,
            in RenderDebugStage stage,
            Texture texture,
            Camera camera = null)
        {
            if (!PreparePublication(sourceId, stage, texture != null, out bool captured))
                return;

            Texture publishedTexture = texture;
            RenderDebugTextureMetadata metadata = RenderDebugTextureMetadata.FromTexture(texture);

            // Persistent textures may be referenced in live mode. A frozen frame always owns a copy.
            if (captured && !_session.CaptureService.TryCapturePersistentTexture(
                    sourceId,
                    stage.Id,
                    texture,
                    captured: true,
                    out publishedTexture,
                    out metadata))
            {
                return;
            }

            PublishData(sourceId, stage.Id, publishedTexture, metadata, camera, captured);
        }

        public void PublishRTHandle(
            CommandBuffer commandBuffer,
            string sourceId,
            in RenderDebugStage stage,
            RTHandle texture,
            in RenderTextureDescriptor descriptor,
            Camera camera = null)
        {
            if (!PreparePublication(sourceId, stage, texture?.rt != null, out bool captured))
                return;

            if (!_session.CaptureService.TryRecordRTHandleCopy(
                    commandBuffer,
                    sourceId,
                    stage.Id,
                    texture,
                    descriptor,
                    captured,
                    out Texture publishedTexture,
                    out RenderDebugTextureMetadata metadata))
            {
                return;
            }

            PublishData(sourceId, stage.Id, publishedTexture, metadata, camera, captured);
        }

        public void PublishRenderGraphTexture(
            RenderGraph renderGraph,
            string sourceId,
            in RenderDebugStage stage,
            TextureHandle texture,
            in RenderTextureDescriptor descriptor,
            Camera camera = null)
        {
            if (!PreparePublication(sourceId, stage, texture.IsValid(), out bool captured))
                return;

            if (!_session.CaptureService.TryRecordRenderGraphCopy(
                    renderGraph,
                    sourceId,
                    stage.Id,
                    texture,
                    descriptor,
                    captured,
                    out Texture publishedTexture,
                    out RenderDebugTextureMetadata metadata))
            {
                return;
            }

            PublishData(sourceId, stage.Id, publishedTexture, metadata, camera, captured);
        }

        private bool PreparePublication(
            string sourceId,
            in RenderDebugStage stage,
            bool hasTexture,
            out bool captured)
        {
            captured = false;
            if (!IsEnabled || !hasTexture || !_session.Registry.IsStageRequested(sourceId, stage.Id))
                return false;

            if (!_session.Registry.TryGetStage(sourceId, stage.Id, out _))
                return false;

            captured = _session.Registry.ViewMode == RenderDebugViewMode.CapturePending &&
                       string.Equals(_session.Registry.CaptureSourceId, sourceId, StringComparison.Ordinal);
            return true;
        }

        private void PublishData(
            string sourceId,
            string stageId,
            Texture texture,
            in RenderDebugTextureMetadata metadata,
            Camera camera,
            bool captured)
        {
            int cameraId = camera != null ? camera.GetInstanceID() : 0;
            string cameraName = camera != null ? camera.name : string.Empty;
            RenderDebugTextureData data = new(
                sourceId,
                stageId,
                texture,
                metadata,
                Time.frameCount,
                cameraId,
                cameraName,
                captured);
            _session.Registry.PublishTextureData(data);
        }
    }
}
