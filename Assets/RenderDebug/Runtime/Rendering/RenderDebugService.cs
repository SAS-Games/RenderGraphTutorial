using System;
using UnityEngine;

namespace SAS.RenderDebugging
{
    /// <summary>
    /// Process-local bridge between independent render systems and the Editor viewer.
    /// The singleton is limited to debug metadata and transient debug-owned resources.
    /// </summary>
    public static class RenderDebugService
    {
        private static RenderDebugSession _session;
        private static int _generation;

        public static IRenderDebugContext Context => Session.Context;
        public static RenderDebugRegistry Registry => Session.Registry;
        public static int Generation => _generation;
        public static bool IsEnabled => _session != null && _session.IsLiveWorkEnabled;

        /// <summary>
        /// Raised when the last viewer closes or the debug session resets, so integrations can release optional materials.
        /// </summary>
        public static event Action ViewerResourcesReleased;

        public static void RetainViewer()
        {
            Session.RetainViewer();
        }

        public static void ReleaseViewer()
        {
            if (_session != null && _session.ReleaseViewer())
                ViewerResourcesReleased?.Invoke();
        }

        public static bool BeginCapture(string sourceId)
        {
            return Session.BeginCapture(sourceId);
        }

        public static void ReturnToLive()
        {
            _session?.ReturnToLive();
        }

        public static void Tick()
        {
            _session?.Tick();
        }

        /// <summary>Releases all debug state during subsystem/domain lifecycle transitions.</summary>
        public static void Reset()
        {
            ViewerResourcesReleased?.Invoke();
            ViewerResourcesReleased = null;
            _session?.Dispose();
            _session = null;
            _generation++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForRuntimeSubsystem()
        {
            Reset();
        }

        private static RenderDebugSession Session => _session ??= new RenderDebugSession();
    }
}
