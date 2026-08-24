using System;
using System.Collections.Generic;
using UnityEngine;

namespace SAS.RenderDebugging
{
    /// <summary>Read-only viewer-facing state for one registered stage.</summary>
    public sealed class RenderDebugStageRecord
    {
        internal RenderDebugStageRecord(in RenderDebugStage descriptor)
        {
            Descriptor = descriptor;
        }

        public RenderDebugStage Descriptor { get; }
        public RenderDebugTextureData TextureData { get; private set; }
        public bool HasTextureData => TextureData.IsValid;

        internal void Publish(in RenderDebugTextureData data)
        {
            TextureData = data;
        }

        internal void ClearTextureData()
        {
            TextureData = default;
        }
    }

    /// <summary>Read-only viewer-facing state for one registered rendering source.</summary>
    public sealed class RenderDebugSourceRecord
    {
        private readonly Dictionary<string, RenderDebugStageRecord> _stagesById =
            new(StringComparer.Ordinal);
        private readonly List<RenderDebugStageRecord> _orderedStages = new();

        internal RenderDebugSourceRecord(IRenderDebugSource owner)
        {
            Owner = owner;
            DebugId = owner.DebugId;
            DisplayName = string.IsNullOrWhiteSpace(owner.DisplayName) ? owner.DebugId : owner.DisplayName;
        }

        internal IRenderDebugSource Owner { get; }
        public string DebugId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<RenderDebugStageRecord> Stages => _orderedStages;

        public bool TryGetStage(string stageId, out RenderDebugStageRecord stage)
        {
            return _stagesById.TryGetValue(stageId, out stage);
        }

        internal bool AddStage(in RenderDebugStage descriptor)
        {
            if (_stagesById.ContainsKey(descriptor.Id))
                return false;

            RenderDebugStageRecord record = new(descriptor);
            _stagesById.Add(descriptor.Id, record);

            int index = 0;
            while (index < _orderedStages.Count && CompareStages(_orderedStages[index].Descriptor, descriptor) <= 0)
                index++;

            _orderedStages.Insert(index, record);
            return true;
        }

        internal bool IsOwnerAlive()
        {
            return Owner is not UnityEngine.Object unityObject || unityObject != null;
        }

        internal void ClearTextureData(bool capturedOnly)
        {
            for (int i = 0; i < _orderedStages.Count; i++)
            {
                RenderDebugStageRecord stage = _orderedStages[i];
                if (!capturedOnly || stage.TextureData.IsCaptured)
                    stage.ClearTextureData();
            }
        }

        private static int CompareStages(in RenderDebugStage left, in RenderDebugStage right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Tracks sources, ordered stage metadata, requested pixels, and latest frame publications.
    /// The registry never owns Unity textures.
    /// </summary>
    public sealed class RenderDebugRegistry : IDisposable
    {
        private readonly struct StageKey : IEquatable<StageKey>
        {
            public StageKey(string sourceId, string stageId)
            {
                SourceId = sourceId;
                StageId = stageId;
            }

            public string SourceId { get; }
            public string StageId { get; }

            public bool Equals(StageKey other)
            {
                return string.Equals(SourceId, other.SourceId, StringComparison.Ordinal) &&
                       string.Equals(StageId, other.StageId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is StageKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((SourceId != null ? StringComparer.Ordinal.GetHashCode(SourceId) : 0) * 397) ^
                           (StageId != null ? StringComparer.Ordinal.GetHashCode(StageId) : 0);
                }
            }
        }

        private readonly Dictionary<string, RenderDebugSourceRecord> _sourcesById =
            new(StringComparer.Ordinal);
        private readonly List<RenderDebugSourceRecord> _orderedSources = new();
        private readonly HashSet<StageKey> _requestedStages = new();
        private readonly HashSet<string> _loggedWarnings = new(StringComparer.Ordinal);
        private readonly Action<string> _warningLogger;
        private bool _captureReceivedData;
        private bool _disposed;

        public RenderDebugRegistry(Action<string> warningLogger = null)
        {
            _warningLogger = warningLogger ?? Debug.LogWarning;
        }

        public IReadOnlyList<RenderDebugSourceRecord> Sources => _orderedSources;
        public RenderDebugViewMode ViewMode { get; private set; } = RenderDebugViewMode.Live;
        public string CaptureSourceId { get; private set; } = string.Empty;

        public event Action<string> SourceUnregistered;
        public event Action Changed;

        public bool RegisterSource(IRenderDebugSource source)
        {
            if (_disposed || source == null || string.IsNullOrWhiteSpace(source.DebugId))
            {
                WarnOnce("invalid-source", "Render Debug ignored a source with an empty ID.");
                return false;
            }

            if (_sourcesById.TryGetValue(source.DebugId, out RenderDebugSourceRecord existing))
            {
                if (ReferenceEquals(existing.Owner, source))
                    return true;

                WarnOnce(
                    $"duplicate-source:{source.DebugId}",
                    $"Render Debug source ID '{source.DebugId}' is already registered by '{existing.DisplayName}'. The duplicate was ignored.");
                return false;
            }

            RenderDebugSourceRecord record = new(source);
            _sourcesById.Add(record.DebugId, record);
            int index = 0;
            while (index < _orderedSources.Count && CompareSources(_orderedSources[index], record) <= 0)
                index++;
            _orderedSources.Insert(index, record);
            Changed?.Invoke();
            return true;
        }

        public bool UnregisterSource(string sourceId, IRenderDebugSource owner = null)
        {
            if (_disposed || string.IsNullOrWhiteSpace(sourceId) ||
                !_sourcesById.TryGetValue(sourceId, out RenderDebugSourceRecord record))
            {
                return false;
            }

            if (owner != null && !ReferenceEquals(record.Owner, owner))
                return false;

            _sourcesById.Remove(sourceId);
            _orderedSources.Remove(record);
            RemoveRequestsForSource(sourceId);

            if (string.Equals(CaptureSourceId, sourceId, StringComparison.Ordinal))
                ReturnToLive();

            SourceUnregistered?.Invoke(sourceId);
            Changed?.Invoke();
            return true;
        }

        public bool RegisterStage(string sourceId, in RenderDebugStage stage)
        {
            if (_disposed || string.IsNullOrWhiteSpace(stage.Id) ||
                !_sourcesById.TryGetValue(sourceId, out RenderDebugSourceRecord source))
            {
                WarnOnce(
                    $"missing-source:{sourceId}",
                    $"Render Debug cannot register stage '{stage.Id}' because source '{sourceId}' is not registered.");
                return false;
            }

            if (source.TryGetStage(stage.Id, out RenderDebugStageRecord existing))
            {
                if (existing.Descriptor.Equals(stage))
                    return true;

                WarnOnce(
                    $"duplicate-stage:{sourceId}:{stage.Id}",
                    $"Render Debug stage ID '{stage.Id}' is already registered for source '{sourceId}' with different metadata. The duplicate was ignored.");
                return false;
            }

            source.AddStage(stage);
            Changed?.Invoke();
            return true;
        }

        public bool TryGetSource(string sourceId, out RenderDebugSourceRecord source)
        {
            return _sourcesById.TryGetValue(sourceId, out source);
        }

        public bool TryGetStage(string sourceId, string stageId, out RenderDebugStageRecord stage)
        {
            stage = null;
            return _sourcesById.TryGetValue(sourceId, out RenderDebugSourceRecord source) &&
                   source.TryGetStage(stageId, out stage);
        }

        public bool SetStageRequested(string sourceId, string stageId, bool requested)
        {
            if (!TryGetStage(sourceId, stageId, out _))
                return false;

            StageKey key = new(sourceId, stageId);
            return requested ? _requestedStages.Add(key) : _requestedStages.Remove(key);
        }

        public bool IsStageRequested(string sourceId, string stageId)
        {
            if (_disposed || ViewMode == RenderDebugViewMode.Captured)
                return false;

            if (ViewMode == RenderDebugViewMode.CapturePending &&
                string.Equals(CaptureSourceId, sourceId, StringComparison.Ordinal))
            {
                return TryGetStage(sourceId, stageId, out _);
            }

            return _requestedStages.Contains(new StageKey(sourceId, stageId));
        }

        public void ClearStageRequests()
        {
            _requestedStages.Clear();
        }

        public bool BeginCapture(string sourceId)
        {
            if (_disposed || !_sourcesById.ContainsKey(sourceId))
                return false;

            CaptureSourceId = sourceId;
            ViewMode = RenderDebugViewMode.CapturePending;
            _captureReceivedData = false;
            Changed?.Invoke();
            return true;
        }

        public bool CompletePendingCapture()
        {
            if (ViewMode != RenderDebugViewMode.CapturePending || !_captureReceivedData)
                return false;

            ViewMode = RenderDebugViewMode.Captured;
            Changed?.Invoke();
            return true;
        }

        public void ReturnToLive()
        {
            if (!string.IsNullOrEmpty(CaptureSourceId) &&
                _sourcesById.TryGetValue(CaptureSourceId, out RenderDebugSourceRecord source))
            {
                source.ClearTextureData(capturedOnly: true);
            }

            CaptureSourceId = string.Empty;
            ViewMode = RenderDebugViewMode.Live;
            _captureReceivedData = false;
            Changed?.Invoke();
        }

        public bool PublishTextureData(in RenderDebugTextureData data)
        {
            if (_disposed || !TryGetStage(data.SourceId, data.StageId, out RenderDebugStageRecord stage))
            {
                WarnOnce(
                    $"unregistered-publication:{data.SourceId}:{data.StageId}",
                    $"Render Debug ignored texture data for unregistered stage '{data.SourceId}/{data.StageId}'.");
                return false;
            }

            if (!data.IsValid)
                return false;

            stage.Publish(data);
            if (ViewMode == RenderDebugViewMode.CapturePending && data.IsCaptured &&
                string.Equals(CaptureSourceId, data.SourceId, StringComparison.Ordinal))
            {
                _captureReceivedData = true;
            }

            Changed?.Invoke();
            return true;
        }

        public int PruneDestroyedSources()
        {
            int removed = 0;
            for (int i = _orderedSources.Count - 1; i >= 0; i--)
            {
                RenderDebugSourceRecord source = _orderedSources[i];
                if (source.IsOwnerAlive())
                    continue;

                if (UnregisterSource(source.DebugId))
                    removed++;
            }

            return removed;
        }

        public void ClearTextureData()
        {
            for (int i = 0; i < _orderedSources.Count; i++)
                _orderedSources[i].ClearTextureData(capturedOnly: false);
            Changed?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _requestedStages.Clear();
            _sourcesById.Clear();
            _orderedSources.Clear();
            _loggedWarnings.Clear();
            SourceUnregistered = null;
            Changed = null;
        }

        private void RemoveRequestsForSource(string sourceId)
        {
            _requestedStages.RemoveWhere(key => string.Equals(key.SourceId, sourceId, StringComparison.Ordinal));
        }

        private void WarnOnce(string key, string message)
        {
            if (_loggedWarnings.Add(key))
                _warningLogger?.Invoke(message);
        }

        private static int CompareSources(RenderDebugSourceRecord left, RenderDebugSourceRecord right)
        {
            int displayName = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            return displayName != 0
                ? displayName
                : string.Compare(left.DebugId, right.DebugId, StringComparison.Ordinal);
        }
    }
}
