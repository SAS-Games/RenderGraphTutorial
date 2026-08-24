using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SAS.RenderDebugging
{
    /// <summary>
    /// Allocates, reuses, copies, and releases debugger-owned GPU textures.
    /// Transient RenderGraph handles are consumed only while recording a copy pass.
    /// </summary>
    public sealed class RenderDebugTextureCaptureService : IDisposable
    {
        private readonly struct ResourceKey : IEquatable<ResourceKey>
        {
            public ResourceKey(string sourceId, string stageId, bool captured)
            {
                SourceId = sourceId;
                StageId = stageId;
                Captured = captured;
            }

            public string SourceId { get; }
            public string StageId { get; }
            public bool Captured { get; }

            public bool Equals(ResourceKey other)
            {
                return Captured == other.Captured &&
                       string.Equals(SourceId, other.SourceId, StringComparison.Ordinal) &&
                       string.Equals(StageId, other.StageId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ResourceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = SourceId != null ? StringComparer.Ordinal.GetHashCode(SourceId) : 0;
                    hash = (hash * 397) ^ (StageId != null ? StringComparer.Ordinal.GetHashCode(StageId) : 0);
                    return (hash * 397) ^ Captured.GetHashCode();
                }
            }
        }

        private sealed class ResourceEntry
        {
            public RTHandle Handle;
            public RenderTextureDescriptor Descriptor;
        }

        private sealed class CopyPassData
        {
            public TextureHandle Source;
        }

        private readonly Dictionary<ResourceKey, ResourceEntry> _resources = new();
        private readonly HashSet<string> _loggedWarnings = new(StringComparer.Ordinal);
        private readonly ProfilingSampler _copySampler = new("Render Debug Texture Copy");
        private bool _disposed;

        public bool TryRecordRenderGraphCopy(
            RenderGraph renderGraph,
            string sourceId,
            string stageId,
            TextureHandle source,
            in RenderTextureDescriptor sourceDescriptor,
            bool captured,
            out Texture texture,
            out RenderDebugTextureMetadata metadata)
        {
            texture = null;
            metadata = default;

            if (_disposed || renderGraph == null || !source.IsValid())
                return false;

            if (!TryCreateCopyDescriptor(sourceDescriptor, sourceId, stageId, out RenderTextureDescriptor copyDescriptor))
                return false;

            try
            {
                ResourceEntry entry = GetOrCreate(sourceId, stageId, captured, copyDescriptor);
                if (entry?.Handle?.rt == null)
                    return false;

                TextureHandle destination = renderGraph.ImportTexture(entry.Handle);
                using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(
                    $"Render Debug Copy: {sourceId}/{stageId}",
                    out CopyPassData passData,
                    _copySampler);

                passData.Source = source;
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CopyPassData data, RasterGraphContext context) =>
                    Blitter.BlitTexture(
                        context.cmd,
                        data.Source,
                        new Vector4(1f, 1f, 0f, 0f),
                        0f,
                        false));

                texture = entry.Handle.rt;
                metadata = RenderDebugTextureMetadata.FromDescriptor(copyDescriptor);
                return true;
            }
            catch (Exception exception)
            {
                WarnOnce(
                    $"rendergraph-copy:{sourceId}:{stageId}:{exception.GetType().Name}",
                    $"Render Debug skipped '{sourceId}/{stageId}' because its RenderGraph copy failed: {exception.Message}");
                return false;
            }
        }

        public bool TryRecordRTHandleCopy(
            CommandBuffer commandBuffer,
            string sourceId,
            string stageId,
            RTHandle source,
            in RenderTextureDescriptor sourceDescriptor,
            bool captured,
            out Texture texture,
            out RenderDebugTextureMetadata metadata)
        {
            texture = null;
            metadata = default;

            if (_disposed || commandBuffer == null || source?.rt == null ||
                !TryCreateCopyDescriptor(sourceDescriptor, sourceId, stageId, out RenderTextureDescriptor copyDescriptor))
            {
                return false;
            }

            try
            {
                ResourceEntry entry = GetOrCreate(sourceId, stageId, captured, copyDescriptor);
                if (entry?.Handle?.rt == null)
                    return false;

                Blitter.BlitCameraTexture(commandBuffer, source, entry.Handle, 0f, false);
                texture = entry.Handle.rt;
                metadata = RenderDebugTextureMetadata.FromDescriptor(copyDescriptor);
                return true;
            }
            catch (Exception exception)
            {
                WarnOnce(
                    $"rthandle-copy:{sourceId}:{stageId}:{exception.GetType().Name}",
                    $"Render Debug skipped '{sourceId}/{stageId}' because its RTHandle copy failed: {exception.Message}");
                return false;
            }
        }

        public bool TryCapturePersistentTexture(
            string sourceId,
            string stageId,
            Texture source,
            bool captured,
            out Texture texture,
            out RenderDebugTextureMetadata metadata)
        {
            texture = null;
            metadata = default;
            if (_disposed || source == null)
                return false;

            RenderTextureDescriptor sourceDescriptor = new(source.width, source.height, source.graphicsFormat, 0)
            {
                dimension = source.dimension,
                volumeDepth = source is RenderTexture renderTexture ? renderTexture.volumeDepth : 1,
                msaaSamples = source is RenderTexture msaaTexture ? msaaTexture.antiAliasing : 1
            };

            if (!TryCreateCopyDescriptor(sourceDescriptor, sourceId, stageId, out RenderTextureDescriptor copyDescriptor))
                return false;

            try
            {
                ResourceEntry entry = GetOrCreate(sourceId, stageId, captured, copyDescriptor);
                if (entry?.Handle?.rt == null)
                    return false;

                Graphics.Blit(source, entry.Handle.rt);
                texture = entry.Handle.rt;
                metadata = RenderDebugTextureMetadata.FromDescriptor(copyDescriptor);
                return true;
            }
            catch (Exception exception)
            {
                WarnOnce(
                    $"texture-copy:{sourceId}:{stageId}:{exception.GetType().Name}",
                    $"Render Debug skipped '{sourceId}/{stageId}' because its texture capture failed: {exception.Message}");
                return false;
            }
        }

        public void ReleaseCaptured()
        {
            ReleaseWhere(key => key.Captured);
        }

        public void ReleaseLive()
        {
            ReleaseWhere(key => !key.Captured);
        }

        public void ReleaseSource(string sourceId)
        {
            ReleaseWhere(key => string.Equals(key.SourceId, sourceId, StringComparison.Ordinal));
        }

        public void ReleaseAll()
        {
            foreach (KeyValuePair<ResourceKey, ResourceEntry> pair in _resources)
                pair.Value.Handle?.Release();
            _resources.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ReleaseAll();
            _loggedWarnings.Clear();
        }

        private ResourceEntry GetOrCreate(
            string sourceId,
            string stageId,
            bool captured,
            in RenderTextureDescriptor descriptor)
        {
            ResourceKey key = new(sourceId, stageId, captured);
            if (_resources.TryGetValue(key, out ResourceEntry entry) &&
                DescriptorMatches(entry.Descriptor, descriptor) && entry.Handle?.rt != null)
            {
                return entry;
            }

            entry?.Handle?.Release();
            RTHandle handle = RTHandles.Alloc(
                descriptor,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: $"RenderDebug_{sourceId}_{stageId}_{(captured ? "Captured" : "Live")}");

            entry ??= new ResourceEntry();
            entry.Handle = handle;
            entry.Descriptor = descriptor;
            _resources[key] = entry;
            return entry;
        }

        private bool TryCreateCopyDescriptor(
            in RenderTextureDescriptor source,
            string sourceId,
            string stageId,
            out RenderTextureDescriptor descriptor)
        {
            descriptor = source;
            if (source.width <= 0 || source.height <= 0)
                return false;

            if (source.dimension != TextureDimension.Tex2D)
            {
                WarnOnce(
                    $"dimension:{sourceId}:{stageId}:{source.dimension}",
                    $"Render Debug currently previews only 2D textures. '{sourceId}/{stageId}' uses {source.dimension} and was skipped.");
                return false;
            }

            if (GraphicsFormatUtility.IsDepthFormat(source.graphicsFormat) ||
                GraphicsFormatUtility.IsDepthStencilFormat(source.depthStencilFormat))
            {
                WarnOnce(
                    $"depth:{sourceId}:{stageId}",
                    $"Render Debug requires an explicit color visualization for depth stage '{sourceId}/{stageId}'. The raw depth texture was skipped.");
                return false;
            }

            GraphicsFormat format = source.graphicsFormat;
            if (format == GraphicsFormat.None || !SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render))
                format = GraphicsFormat.R8G8B8A8_UNorm;

            descriptor.width = Mathf.Max(1, source.width);
            descriptor.height = Mathf.Max(1, source.height);
            descriptor.graphicsFormat = format;
            descriptor.depthBufferBits = 0;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.volumeDepth = 1;
            descriptor.dimension = TextureDimension.Tex2D;
            descriptor.useDynamicScale = false;
            descriptor.useDynamicScaleExplicit = false;
            descriptor.enableRandomWrite = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.mipCount = 1;
            descriptor.memoryless = RenderTextureMemoryless.None;
            descriptor.vrUsage = VRTextureUsage.None;
            return true;
        }

        private void ReleaseWhere(Predicate<ResourceKey> predicate)
        {
            if (_resources.Count == 0)
                return;

            List<ResourceKey> keys = new();
            foreach (KeyValuePair<ResourceKey, ResourceEntry> pair in _resources)
            {
                if (predicate(pair.Key))
                    keys.Add(pair.Key);
            }

            for (int i = 0; i < keys.Count; i++)
            {
                ResourceKey key = keys[i];
                _resources[key].Handle?.Release();
                _resources.Remove(key);
            }
        }

        private void WarnOnce(string key, string message)
        {
            if (_loggedWarnings.Add(key))
                Debug.LogWarning(message);
        }

        private static bool DescriptorMatches(
            in RenderTextureDescriptor left,
            in RenderTextureDescriptor right)
        {
            return left.width == right.width &&
                   left.height == right.height &&
                   left.volumeDepth == right.volumeDepth &&
                   left.graphicsFormat == right.graphicsFormat &&
                   left.dimension == right.dimension &&
                   left.msaaSamples == right.msaaSamples;
        }
    }
}
