using System;

namespace SAS.RenderDebugging
{
    internal sealed class RenderDebugSession : IDisposable
    {
        private int _viewerCount;
        private bool _disposed;

        public RenderDebugSession()
        {
            Registry = new RenderDebugRegistry();
            CaptureService = new RenderDebugTextureCaptureService();
            Context = new RenderDebugContext(this);
            Registry.SourceUnregistered += CaptureService.ReleaseSource;
        }

        public RenderDebugRegistry Registry { get; }
        public RenderDebugTextureCaptureService CaptureService { get; }
        public IRenderDebugContext Context { get; }
        public bool IsLiveWorkEnabled => !_disposed && _viewerCount > 0 &&
                                         Registry.ViewMode != RenderDebugViewMode.Captured;

        public void RetainViewer()
        {
            if (!_disposed)
                _viewerCount++;
        }

        public bool ReleaseViewer()
        {
            if (_disposed || _viewerCount <= 0)
                return false;

            _viewerCount--;
            if (_viewerCount != 0)
                return false;

            Registry.ClearStageRequests();
            Registry.ReturnToLive();
            Registry.ClearTextureData();
            CaptureService.ReleaseAll();
            return true;
        }

        public bool BeginCapture(string sourceId)
        {
            if (_disposed || _viewerCount == 0)
                return false;

            CaptureService.ReleaseCaptured();
            return Registry.BeginCapture(sourceId);
        }

        public void ReturnToLive()
        {
            if (_disposed)
                return;

            CaptureService.ReleaseCaptured();
            Registry.ReturnToLive();
        }

        public void Tick()
        {
            if (_disposed)
                return;

            Registry.PruneDestroyedSources();
            Registry.CompletePendingCapture();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Registry.SourceUnregistered -= CaptureService.ReleaseSource;
            CaptureService.Dispose();
            Registry.Dispose();
        }
    }
}
