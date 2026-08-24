using UnityEditor;
using UnityEngine;

namespace SAS.RenderDebugging.Editor
{
    internal static class RenderDebuggerStageView
    {
        private const float CardWidth = 154f;
        private const float CardHeight = 132f;

        public static string Draw(
            Rect rect,
            RenderDebugSourceRecord source,
            string selectedStageId,
            RenderDebugViewMode viewMode,
            Material previewMaterial,
            ref Vector2 scrollPosition)
        {
            if (source == null || source.Stages.Count == 0)
            {
                GUI.Label(rect, "This source has no registered stages.", EditorStyles.centeredGreyMiniLabel);
                return selectedStageId;
            }

            float contentWidth = source.Stages.Count * (CardWidth + 28f) + 8f;
            Rect contentRect = new(0f, 0f, Mathf.Max(rect.width - 16f, contentWidth), CardHeight + 12f);
            scrollPosition = GUI.BeginScrollView(rect, scrollPosition, contentRect, false, true);

            float x = 4f;
            for (int i = 0; i < source.Stages.Count; i++)
            {
                RenderDebugStageRecord stage = source.Stages[i];
                bool selected = string.Equals(stage.Descriptor.Id, selectedStageId, System.StringComparison.Ordinal);
                Rect cardRect = new(x, 4f, CardWidth, CardHeight);
                GUIContent content = new(string.Empty, stage.Descriptor.Description);
                if (GUI.Button(cardRect, content, selected
                        ? RenderDebuggerStyles.SelectedStageCard
                        : RenderDebuggerStyles.StageCard))
                {
                    selectedStageId = stage.Descriptor.Id;
                }

                Rect titleRect = new(cardRect.x + 6f, cardRect.y + 5f, cardRect.width - 12f, 33f);
                GUI.Label(titleRect, stage.Descriptor.DisplayName, RenderDebuggerStyles.CenteredMiniLabel);

                Rect thumbnailRect = new(cardRect.x + 8f, cardRect.y + 40f, cardRect.width - 16f, 68f);
                bool hasUsableData = HasUsableData(stage, viewMode);
                if (hasUsableData && previewMaterial != null &&
                    stage.TextureData.Texture.dimension == UnityEngine.Rendering.TextureDimension.Tex2D)
                {
                    previewMaterial.SetFloat("_Channel", 0f);
                    previewMaterial.SetFloat("_Exposure", 0f);
                    previewMaterial.SetFloat("_ViewMode", 0f);
                    EditorGUI.DrawPreviewTexture(
                        thumbnailRect,
                        stage.TextureData.Texture,
                        previewMaterial,
                        ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.Box(thumbnailRect, GUIContent.none);
                    GUI.Label(
                        thumbnailRect,
                        stage.HasTextureData ? "No data this frame" : "No data",
                        RenderDebuggerStyles.CenteredMiniLabel);
                }

                string footer = string.IsNullOrEmpty(stage.Descriptor.Group)
                    ? stage.Descriptor.Type.ToString()
                    : stage.Descriptor.Group;
                GUI.Label(
                    new Rect(cardRect.x + 4f, cardRect.yMax - 21f, cardRect.width - 8f, 17f),
                    footer,
                    RenderDebuggerStyles.CenteredMiniLabel);

                x += CardWidth + 4f;
                if (i < source.Stages.Count - 1)
                {
                    GUI.Label(
                        new Rect(x, cardRect.y + 55f, 24f, 24f),
                        "→",
                        EditorStyles.boldLabel);
                    x += 24f;
                }
            }

            GUI.EndScrollView();
            return selectedStageId;
        }

        private static bool HasUsableData(RenderDebugStageRecord stage, RenderDebugViewMode viewMode)
        {
            if (!stage.HasTextureData)
                return false;

            if (viewMode == RenderDebugViewMode.Captured)
                return stage.TextureData.IsCaptured;

            return !stage.TextureData.IsCaptured && stage.TextureData.FrameIndex == Time.frameCount;
        }
    }
}
