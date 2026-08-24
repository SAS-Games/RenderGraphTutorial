using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace SAS.RenderDebugging
{
    /// <summary>
    /// Profiler-style facade for a render debug source. Declare one marker, then publish stages
    /// without manually managing a context, source registration, or session recreation.
    /// </summary>
    public sealed class RenderDebugSourceMarker : IRenderDebugSource, IDisposable
    {
        private readonly RenderDebugStage[] _declaredStages;
        private Dictionary<string, RenderDebugStage> _namedStages;
        private IRenderDebugContext _context;
        private int _registeredGeneration = -1;
        private int _nextNamedStageOrder = 10;
        private bool _disposed;

        public RenderDebugSourceMarker(string debugId, string displayName, params RenderDebugStage[] stages)
        {
            if (string.IsNullOrWhiteSpace(debugId))
                throw new ArgumentException("A render debug source ID cannot be empty.", nameof(debugId));

            DebugId = debugId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? debugId : displayName;
            _declaredStages = stages == null || stages.Length == 0 ? Array.Empty<RenderDebugStage>() : (RenderDebugStage[])stages.Clone();

            for (int i = 0; i < _declaredStages.Length; i++)
            {
                RenderDebugStage stage = _declaredStages[i];
                _namedStages ??= new Dictionary<string, RenderDebugStage>(StringComparer.Ordinal);
                _namedStages[stage.Id] = stage;
                _nextNamedStageOrder = Math.Max(_nextNamedStageOrder, stage.Order + 10);
            }
        }

        public string DebugId { get; }
        public string DisplayName { get; }

        /// <summary>
        /// Gets whether the viewer requests a named stage. Named stages are registered lazily in
        /// first-publication order, which keeps simple instrumentation free of descriptor boilerplate.
        /// </summary>
        public bool IsRequested(string stageName)
        {
            if (!RenderDebugService.IsEnabled)
                return false;

            RenderDebugStage stage = GetOrCreateNamedStage(stageName);
            return IsRequested(stage);
        }

        /// <summary>Gets whether the viewer currently requests this stage.</summary>
        public bool IsRequested(in RenderDebugStage stage)
        {
            return RenderDebugService.IsEnabled &&
                   EnsureRegistered(stage) &&
                   _context.IsStageRequested(DebugId, stage.Id);
        }

        /// <summary>Publishes a caller-owned persistent texture when the stage is requested.</summary>
        [System.Diagnostics.Conditional("RENDER_DEBUG")]
        public void Publish(in RenderDebugStage stage, Texture texture, Camera camera = null)
        {
            if (EnsureRegistered(stage))
                _context.PublishTexture(DebugId, stage, texture, camera);
        }

        /// <summary>Publishes a persistent texture under a simple profiler-style stage name.</summary>
        [System.Diagnostics.Conditional("RENDER_DEBUG")]
        public void Publish(string stageName, Texture texture, Camera camera = null)
        {
            RenderDebugStage stage = GetOrCreateNamedStage(stageName);
            if (EnsureRegistered(stage))
                _context.PublishTexture(DebugId, stage, texture, camera);
        }

        /// <summary>Schedules an owned debug copy of an RTHandle when the stage is requested.</summary>
        [System.Diagnostics.Conditional("RENDER_DEBUG")]
        public void Publish(CommandBuffer commandBuffer, in RenderDebugStage stage, RTHandle texture, in RenderTextureDescriptor descriptor, Camera camera = null)
        {
            if (EnsureRegistered(stage))
                _context.PublishRTHandle(commandBuffer, DebugId, stage, texture, descriptor, camera);
        }

        /// <summary>Schedules an owned debug copy under a simple profiler-style stage name.</summary>
        [System.Diagnostics.Conditional("RENDER_DEBUG")]
        public void Publish(CommandBuffer commandBuffer, string stageName, RTHandle texture, in RenderTextureDescriptor descriptor, Camera camera = null)
        {
            RenderDebugStage stage = GetOrCreateNamedStage(stageName);
            if (EnsureRegistered(stage))
                _context.PublishRTHandle(commandBuffer, DebugId, stage, texture, descriptor, camera);
        }

        /// <summary>
        /// Adds a RenderGraph copy into debugger-owned storage when the stage is requested.
        /// </summary>
        [System.Diagnostics.Conditional("RENDER_DEBUG")]
        public void Publish(RenderGraph renderGraph, in RenderDebugStage stage, TextureHandle texture, in RenderTextureDescriptor descriptor, Camera camera = null)
        {
            if (EnsureRegistered(stage))
                _context.PublishRenderGraphTexture(renderGraph, DebugId, stage, texture, descriptor, camera);
        }

        /// <summary>Adds a requested RenderGraph copy under a simple profiler-style stage name.</summary>
        [System.Diagnostics.Conditional("RENDER_DEBUG")]
        public void Publish(RenderGraph renderGraph, string stageName, TextureHandle texture, in RenderTextureDescriptor descriptor, Camera camera = null)
        {
            RenderDebugStage stage = GetOrCreateNamedStage(stageName);
            if (EnsureRegistered(stage))
                _context.PublishRenderGraphTexture(renderGraph, DebugId, stage, texture, descriptor, camera);
        }

        /// <summary>Unregisters the source. Call from the renderer feature's Dispose method.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _context?.UnregisterSource(DebugId, this);
            _context = null;
        }

        private RenderDebugStage GetOrCreateNamedStage(string stageName)
        {
            if (string.IsNullOrWhiteSpace(stageName))
                throw new ArgumentException("A render debug stage name cannot be empty.", nameof(stageName));

            if (_namedStages != null &&
                _namedStages.TryGetValue(stageName, out RenderDebugStage stage))
                return stage;

            stage = new RenderDebugStage(stageName, stageName, _nextNamedStageOrder);
            _nextNamedStageOrder += 10;
            _namedStages ??= new Dictionary<string, RenderDebugStage>(StringComparer.Ordinal);
            _namedStages.Add(stageName, stage);
            return stage;
        }

        private bool EnsureRegistered(in RenderDebugStage stage)
        {
            if (_disposed)
                return false;

            int generation = RenderDebugService.Generation;
            if (_context != null && _registeredGeneration == generation)
                return _context.RegisterStage(DebugId, stage);

            _context = RenderDebugService.Context;
            if (!_context.RegisterSource(this))
                return false;

            for (int i = 0; i < _declaredStages.Length; i++)
                _context.RegisterStage(DebugId, _declaredStages[i]);

            _registeredGeneration = generation;
            return _context.RegisterStage(DebugId, stage);
        }
    }
}
