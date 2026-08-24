using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SAS.RenderDebugging.Editor
{
    public sealed class RenderDebuggerWindow : EditorWindow
    {
        private const float SourcePanelWidth = 220f;
        private const float SequenceHeight = 158f;
        private const float MetadataHeight = 116f;

        private RenderDebuggerTextureView _textureView;
        private Vector2 _sourceScroll;
        private Vector2 _stageScroll;
        private string _selectedSourceId = string.Empty;
        private string _selectedStageId = string.Empty;
        private string _comparisonStageId = string.Empty;
        private int _serviceGeneration;
        private bool _viewerRetained;

        [MenuItem("Window/Analysis/Shader & Render Debugger")]
        public static void Open()
        {
            RenderDebuggerWindow window = GetWindow<RenderDebuggerWindow>();
            window.titleContent = new GUIContent("Render Debugger");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Render Debugger");
            minSize = new Vector2(760f, 520f);
            _textureView ??= new RenderDebuggerTextureView();
            RetainViewer();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _textureView?.Dispose();
            _textureView = null;
            if (_viewerRetained)
            {
                RenderDebugService.ReleaseViewer();
                _viewerRetained = false;
            }
        }

        private void OnEditorUpdate()
        {
            if (_serviceGeneration != RenderDebugService.Generation)
                RetainViewer();

            RenderDebugService.Tick();
            Repaint();
        }

        private void OnGUI()
        {
            if (_textureView == null)
                _textureView = new RenderDebuggerTextureView();

            RenderDebugRegistry registry = RenderDebugService.Registry;
            ResolveSelection(registry);

            Rect toolbarRect = new(0f, 0f, position.width, 24f);
            DrawMainToolbar(toolbarRect, registry);

            Rect contentRect = new(0f, toolbarRect.yMax, position.width, position.height - toolbarRect.height);
            Rect sourceRect = new(contentRect.x, contentRect.y, SourcePanelWidth, contentRect.height);
            Rect rightRect = new(
                sourceRect.xMax + 1f,
                contentRect.y,
                Mathf.Max(1f, contentRect.width - SourcePanelWidth - 1f),
                contentRect.height);

            DrawSources(sourceRect, registry);
            DrawRightPanel(rightRect, registry);
            SyncRequests(registry);
        }

        private void DrawMainToolbar(Rect rect, RenderDebugRegistry registry)
        {
            GUILayout.BeginArea(rect, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Shader & Render Debugger", EditorStyles.boldLabel, GUILayout.Width(190f));

            string status = registry.ViewMode switch
            {
                RenderDebugViewMode.CapturePending => "CAPTURE PENDING",
                RenderDebugViewMode.Captured => "CAPTURED",
                _ => "LIVE"
            };
            GUIStyle statusStyle = registry.ViewMode == RenderDebugViewMode.Live
                ? RenderDebuggerStyles.StatusLive
                : RenderDebuggerStyles.StatusCaptured;
            GUILayout.Label(status, statusStyle, GUILayout.Width(130f));
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_selectedSourceId)))
            {
                if (registry.ViewMode == RenderDebugViewMode.Live)
                {
                    if (GUILayout.Button("Capture Frame", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                        RenderDebugService.BeginCapture(_selectedSourceId);
                }
                else if (registry.ViewMode == RenderDebugViewMode.CapturePending)
                {
                    GUILayout.Label("Waiting for the source to render…", EditorStyles.miniLabel);
                    if (GUILayout.Button("Cancel", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                        RenderDebugService.ReturnToLive();
                }
                else if (GUILayout.Button("Return To Live", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    RenderDebugService.ReturnToLive();
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawSources(Rect rect, RenderDebugRegistry registry)
        {
            GUI.Box(rect, GUIContent.none);
            Rect titleRect = new(rect.x + 8f, rect.y + 7f, rect.width - 16f, 20f);
            GUI.Label(titleRect, "Render Sources", EditorStyles.boldLabel);

            Rect scrollRect = new(rect.x + 5f, titleRect.yMax + 4f, rect.width - 10f, rect.height - 36f);
            Rect contentRect = new(0f, 0f, scrollRect.width - 16f, Mathf.Max(scrollRect.height, registry.Sources.Count * 26f));
            _sourceScroll = GUI.BeginScrollView(scrollRect, _sourceScroll, contentRect);

            float y = 0f;
            for (int i = 0; i < registry.Sources.Count; i++)
            {
                RenderDebugSourceRecord source = registry.Sources[i];
                bool selected = string.Equals(source.DebugId, _selectedSourceId, StringComparison.Ordinal);
                if (GUI.Button(
                        new Rect(0f, y, contentRect.width, 24f),
                        source.DisplayName,
                        selected ? RenderDebuggerStyles.SelectedSourceButton : RenderDebuggerStyles.SourceButton))
                {
                    SelectSource(source);
                }
                y += 26f;
            }

            GUI.EndScrollView();

            if (registry.Sources.Count == 0)
            {
                GUI.Label(
                    new Rect(rect.x + 14f, rect.y + 48f, rect.width - 28f, 110f),
                    "No sources are registered.\n\nAdd RENDER_DEBUG to Scripting Define Symbols and register a render source.",
                    RenderDebuggerStyles.CenteredMiniLabel);
            }
        }

        private void DrawRightPanel(Rect rect, RenderDebugRegistry registry)
        {
            if (!registry.TryGetSource(_selectedSourceId, out RenderDebugSourceRecord source))
            {
                GUI.Label(rect, "Select a registered render source.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Rect sequenceLabel = new(rect.x + 8f, rect.y + 5f, rect.width - 16f, 20f);
            GUI.Label(sequenceLabel, "Stage Sequence", EditorStyles.boldLabel);
            Rect sequenceRect = new(rect.x + 5f, sequenceLabel.yMax + 2f, rect.width - 10f, SequenceHeight - 27f);
            _selectedStageId = RenderDebuggerStageView.Draw(
                sequenceRect,
                source,
                _selectedStageId,
                registry.ViewMode,
                _textureView.PreviewMaterial,
                ref _stageScroll);

            Rect comparisonRect = new(rect.x + 5f, rect.y + SequenceHeight, rect.width - 10f, 22f);
            DrawComparisonToolbar(comparisonRect, source);

            float previewTop = comparisonRect.yMax + 2f;
            float previewHeight = Mathf.Max(120f, rect.yMax - previewTop - MetadataHeight);
            Rect previewRect = new(rect.x + 5f, previewTop, rect.width - 10f, previewHeight);
            source.TryGetStage(_selectedStageId, out RenderDebugStageRecord selectedStage);
            source.TryGetStage(_comparisonStageId, out RenderDebugStageRecord comparisonStage);
            _textureView.Draw(previewRect, selectedStage, comparisonStage, registry.ViewMode);

            Rect metadataRect = new(rect.x + 5f, previewRect.yMax + 3f, rect.width - 10f, MetadataHeight - 5f);
            DrawMetadata(metadataRect, selectedStage, registry.ViewMode);
        }

        private void DrawComparisonToolbar(Rect rect, RenderDebugSourceRecord source)
        {
            GUILayout.BeginArea(rect, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Compare", GUILayout.Width(55f));
            _textureView.ComparisonMode = (RenderDebugComparisonMode)EditorGUILayout.EnumPopup(
                _textureView.ComparisonMode,
                EditorStyles.toolbarPopup,
                GUILayout.Width(92f));

            using (new EditorGUI.DisabledScope(_textureView.ComparisonMode == RenderDebugComparisonMode.Off))
            {
                string[] labels = new string[source.Stages.Count];
                int selectedIndex = 0;
                for (int i = 0; i < source.Stages.Count; i++)
                {
                    labels[i] = source.Stages[i].Descriptor.DisplayName;
                    if (string.Equals(source.Stages[i].Descriptor.Id, _comparisonStageId, StringComparison.Ordinal))
                        selectedIndex = i;
                }

                if (labels.Length > 0)
                {
                    selectedIndex = EditorGUILayout.Popup(selectedIndex, labels, EditorStyles.toolbarPopup, GUILayout.MinWidth(120f));
                    _comparisonStageId = source.Stages[selectedIndex].Descriptor.Id;
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static void DrawMetadata(
            Rect rect,
            RenderDebugStageRecord stage,
            RenderDebugViewMode viewMode)
        {
            GUI.Box(rect, GUIContent.none);
            if (stage == null)
                return;

            float y = rect.y + 6f;
            GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 18f), "Metadata / Channels", EditorStyles.boldLabel);
            y += 21f;

            if (!string.IsNullOrEmpty(stage.Descriptor.Description))
            {
                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 18f), stage.Descriptor.Description, EditorStyles.miniLabel);
                y += 18f;
            }

            bool usable = stage.HasTextureData &&
                          (viewMode != RenderDebugViewMode.Captured || stage.TextureData.IsCaptured);
            if (usable)
            {
                RenderDebugTextureData data = stage.TextureData;
                RenderDebugTextureMetadata metadata = data.Metadata;
                string camera = string.IsNullOrEmpty(data.CameraName) ? "Unknown camera" : data.CameraName;
                GUI.Label(
                    new Rect(rect.x + 8f, y, rect.width - 16f, 18f),
                    $"{metadata.Width} × {metadata.Height}   {metadata.GraphicsFormat}   {metadata.Dimension}   MSAA {metadata.MsaaSamples}   Frame {data.FrameIndex}   {camera}",
                    EditorStyles.miniLabel);
                y += 18f;
            }
            else
            {
                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 18f), "No data this frame", EditorStyles.miniLabel);
                y += 18f;
            }

            IReadOnlyList<RenderDebugChannelInfo> channels = stage.Descriptor.Channels;
            if (channels.Count > 0)
            {
                string text = string.Empty;
                for (int i = 0; i < channels.Count; i++)
                {
                    if (i > 0)
                        text += "     ";
                    text += $"{channels[i].Channel}: {channels[i].Meaning}";
                }
                GUI.Label(new Rect(rect.x + 8f, y, rect.width - 16f, 34f), text, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void ResolveSelection(RenderDebugRegistry registry)
        {
            if (registry.TryGetSource(_selectedSourceId, out RenderDebugSourceRecord selectedSource))
            {
                if (!selectedSource.TryGetStage(_selectedStageId, out _) && selectedSource.Stages.Count > 0)
                    _selectedStageId = selectedSource.Stages[0].Descriptor.Id;
                if (!selectedSource.TryGetStage(_comparisonStageId, out _) && selectedSource.Stages.Count > 0)
                    _comparisonStageId = selectedSource.Stages[Mathf.Min(1, selectedSource.Stages.Count - 1)].Descriptor.Id;
                return;
            }

            if (registry.Sources.Count == 0)
            {
                _selectedSourceId = string.Empty;
                _selectedStageId = string.Empty;
                _comparisonStageId = string.Empty;
                return;
            }

            SelectSource(registry.Sources[0]);
        }

        private void SelectSource(RenderDebugSourceRecord source)
        {
            _selectedSourceId = source.DebugId;
            _selectedStageId = source.Stages.Count > 0 ? source.Stages[0].Descriptor.Id : string.Empty;
            _comparisonStageId = source.Stages.Count > 1 ? source.Stages[1].Descriptor.Id : _selectedStageId;
            _stageScroll = Vector2.zero;
        }

        private void SyncRequests(RenderDebugRegistry registry)
        {
            registry.ClearStageRequests();
            if (registry.ViewMode != RenderDebugViewMode.Live || string.IsNullOrEmpty(_selectedSourceId))
                return;

            if (!string.IsNullOrEmpty(_selectedStageId))
                registry.SetStageRequested(_selectedSourceId, _selectedStageId, true);
            if (_textureView.ComparisonMode != RenderDebugComparisonMode.Off &&
                !string.IsNullOrEmpty(_comparisonStageId))
            {
                registry.SetStageRequested(_selectedSourceId, _comparisonStageId, true);
            }
        }

        private void RetainViewer()
        {
            RenderDebugService.RetainViewer();
            _serviceGeneration = RenderDebugService.Generation;
            _viewerRetained = true;
        }
    }
}
