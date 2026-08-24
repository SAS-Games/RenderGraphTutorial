using UnityEditor;
using UnityEngine;

namespace SAS.RenderDebugging.Editor
{
    internal static class RenderDebuggerStyles
    {
        private static GUIStyle _sourceButton;
        private static GUIStyle _selectedSourceButton;
        private static GUIStyle _stageCard;
        private static GUIStyle _selectedStageCard;
        private static GUIStyle _centeredMiniLabel;
        private static GUIStyle _statusLive;
        private static GUIStyle _statusCaptured;

        public static GUIStyle SourceButton => _sourceButton ??= CreateSourceButton(false);
        public static GUIStyle SelectedSourceButton => _selectedSourceButton ??= CreateSourceButton(true);
        public static GUIStyle StageCard => _stageCard ??= CreateStageCard(false);
        public static GUIStyle SelectedStageCard => _selectedStageCard ??= CreateStageCard(true);

        public static GUIStyle CenteredMiniLabel => _centeredMiniLabel ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        public static GUIStyle StatusLive => _statusLive ??= CreateStatusStyle(new Color(0.35f, 0.85f, 0.45f));
        public static GUIStyle StatusCaptured => _statusCaptured ??= CreateStatusStyle(new Color(0.35f, 0.65f, 1f));

        private static GUIStyle CreateSourceButton(bool selected)
        {
            GUIStyle style = new(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 24f,
                fontStyle = selected ? FontStyle.Bold : FontStyle.Normal
            };
            return style;
        }

        private static GUIStyle CreateStageCard(bool selected)
        {
            GUIStyle style = new(GUI.skin.button)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                fontStyle = selected ? FontStyle.Bold : FontStyle.Normal,
                padding = new RectOffset(6, 6, 6, 6)
            };
            return style;
        }

        private static GUIStyle CreateStatusStyle(Color color)
        {
            GUIStyle style = new(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = color }
            };
            return style;
        }
    }
}
