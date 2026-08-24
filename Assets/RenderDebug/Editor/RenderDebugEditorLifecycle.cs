using UnityEditor;

namespace SAS.RenderDebugging.Editor
{
    [InitializeOnLoad]
    internal static class RenderDebugEditorLifecycle
    {
        static RenderDebugEditorLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload += RenderDebugService.Reset;
            EditorApplication.quitting += RenderDebugService.Reset;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode ||
                change == PlayModeStateChange.ExitingPlayMode)
            {
                RenderDebugService.Reset();
            }
        }
    }
}
