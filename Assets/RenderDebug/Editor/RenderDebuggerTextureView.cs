using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SAS.RenderDebugging.Editor
{
    internal enum RenderDebugComparisonMode
    {
        Off,
        SideBySide,
        Difference
    }

    internal sealed class RenderDebuggerTextureView : IDisposable
    {
        private static readonly string[] ChannelNames = { "RGB", "R", "G", "B", "A" };
        private readonly Material _previewMaterial;
        private Vector2 _oneToOneScroll;
        private bool _probePending;
        private bool _hasProbe;
        private Vector2 _probeUv;
        private Vector2Int _probePixel;
        private Color _probeColor;

        public RenderDebuggerTextureView()
        {
            Shader shader = Shader.Find("Hidden/SAS/RenderDebug/Preview");
            if (shader != null)
            {
                _previewMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        public Material PreviewMaterial => _previewMaterial;
        public int Channel { get; set; }
        public float Exposure { get; set; }
        public bool OneToOne { get; set; }
        public RenderDebugComparisonMode ComparisonMode { get; set; }

        public void Draw(
            Rect rect,
            RenderDebugStageRecord stageA,
            RenderDebugStageRecord stageB,
            RenderDebugViewMode viewMode)
        {
            DrawToolbar(new Rect(rect.x, rect.y, rect.width, 22f));
            Rect previewRect = new(rect.x, rect.y + 24f, rect.width, Mathf.Max(1f, rect.height - 68f));
            Rect probeRect = new(rect.x, previewRect.yMax + 2f, rect.width, 40f);

            Texture textureA = GetUsableTexture(stageA, viewMode);
            Texture textureB = GetUsableTexture(stageB, viewMode);

            if (_previewMaterial == null)
            {
                GUI.Label(previewRect, "Preview shader was not found.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (textureA == null)
            {
                GUI.Box(previewRect, GUIContent.none);
                GUI.Label(previewRect, "No data this frame", EditorStyles.centeredGreyMiniLabel);
                DrawProbe(probeRect);
                return;
            }

            _previewMaterial.SetFloat("_Channel", Channel);
            _previewMaterial.SetFloat("_Exposure", Exposure);
            _previewMaterial.SetTexture("_CompareTex", textureB != null ? textureB : Texture2D.blackTexture);

            if (ComparisonMode == RenderDebugComparisonMode.SideBySide && textureB != null)
            {
                float halfWidth = (previewRect.width - 6f) * 0.5f;
                Rect left = new(previewRect.x, previewRect.y, halfWidth, previewRect.height);
                Rect right = new(left.xMax + 6f, previewRect.y, halfWidth, previewRect.height);
                _previewMaterial.SetFloat("_ViewMode", 0f);
                Rect drawnA = DrawTexture(left, textureA);
                DrawTexture(right, textureB);
                HandleProbe(drawnA, textureA);
                GUI.Label(new Rect(left.x + 4f, left.y + 4f, 64f, 18f), "Stage A", EditorStyles.miniBoldLabel);
                GUI.Label(new Rect(right.x + 4f, right.y + 4f, 64f, 18f), "Stage B", EditorStyles.miniBoldLabel);
            }
            else
            {
                _previewMaterial.SetFloat(
                    "_ViewMode",
                    ComparisonMode == RenderDebugComparisonMode.Difference && textureB != null ? 1f : 0f);
                Rect drawn = DrawTexture(previewRect, textureA);
                HandleProbe(drawn, textureA);
            }

            DrawProbe(probeRect);
        }

        public void Dispose()
        {
            if (_previewMaterial != null)
                UnityEngine.Object.DestroyImmediate(_previewMaterial);
        }

        private void DrawToolbar(Rect rect)
        {
            GUILayout.BeginArea(rect, EditorStyles.toolbar);
            GUILayout.BeginHorizontal();
            Channel = GUILayout.Toolbar(Channel, ChannelNames, EditorStyles.toolbarButton, GUILayout.Width(190f));
            GUILayout.Space(8f);
            GUILayout.Label("Exposure", GUILayout.Width(56f));
            Exposure = GUILayout.HorizontalSlider(Exposure, -8f, 8f, GUILayout.Width(140f));
            GUILayout.Label(Exposure.ToString("+0.0;-0.0;0.0"), GUILayout.Width(38f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Toggle(!OneToOne, "Fit", EditorStyles.toolbarButton, GUILayout.Width(38f)))
                OneToOne = false;
            if (GUILayout.Toggle(OneToOne, "1:1", EditorStyles.toolbarButton, GUILayout.Width(38f)))
                OneToOne = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private Rect DrawTexture(Rect viewport, Texture texture)
        {
            GUI.Box(viewport, GUIContent.none);
            if (!OneToOne)
            {
                Rect fitted = FitRect(viewport, texture.width, texture.height);
                EditorGUI.DrawPreviewTexture(fitted, texture, _previewMaterial, ScaleMode.StretchToFill);
                return fitted;
            }

            Rect content = new(
                0f,
                0f,
                Mathf.Max(viewport.width - 16f, texture.width),
                Mathf.Max(viewport.height - 16f, texture.height));
            _oneToOneScroll = GUI.BeginScrollView(viewport, _oneToOneScroll, content);
            Rect imageRect = new(
                Mathf.Max(0f, (content.width - texture.width) * 0.5f),
                Mathf.Max(0f, (content.height - texture.height) * 0.5f),
                texture.width,
                texture.height);
            EditorGUI.DrawPreviewTexture(imageRect, texture, _previewMaterial, ScaleMode.StretchToFill);
            GUI.EndScrollView();

            // Probe coordinates in 1:1 mode account for the scroll offset.
            return new Rect(
                viewport.x + imageRect.x - _oneToOneScroll.x,
                viewport.y + imageRect.y - _oneToOneScroll.y,
                imageRect.width,
                imageRect.height);
        }

        private void HandleProbe(Rect imageRect, Texture texture)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 ||
                !imageRect.Contains(current.mousePosition) || _probePending)
            {
                return;
            }

            float u = Mathf.InverseLerp(imageRect.xMin, imageRect.xMax, current.mousePosition.x);
            float v = 1f - Mathf.InverseLerp(imageRect.yMin, imageRect.yMax, current.mousePosition.y);
            int x = Mathf.Clamp(Mathf.FloorToInt(u * texture.width), 0, texture.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * texture.height), 0, texture.height - 1);
            _probeUv = new Vector2(u, v);
            _probePixel = new Vector2Int(x, y);
            _probePending = true;

            try
            {
                AsyncGPUReadback.Request(
                    texture,
                    0,
                    x,
                    1,
                    y,
                    1,
                    0,
                    1,
                    TextureFormat.RGBAFloat,
                    OnProbeComplete);
            }
            catch (Exception)
            {
                _probePending = false;
                _hasProbe = false;
            }

            current.Use();
        }

        private void OnProbeComplete(AsyncGPUReadbackRequest request)
        {
            _probePending = false;
            if (request.hasError)
            {
                _hasProbe = false;
                return;
            }

            var data = request.GetData<Color>();
            if (data.Length == 0)
                return;

            _probeColor = data[0];
            _hasProbe = true;
        }

        private void DrawProbe(Rect rect)
        {
            string text;
            if (_probePending)
            {
                text = "Pixel probe: reading…";
            }
            else if (_hasProbe)
            {
                text = $"Pixel {_probePixel.x}, {_probePixel.y}   UV {_probeUv.x:F4}, {_probeUv.y:F4}   " +
                       $"R {_probeColor.r:F5}   G {_probeColor.g:F5}   B {_probeColor.b:F5}   A {_probeColor.a:F5}";
            }
            else
            {
                text = "Click the preview to probe one pixel.";
            }

            GUI.Label(rect, text, EditorStyles.centeredGreyMiniLabel);
        }

        private static Texture GetUsableTexture(RenderDebugStageRecord stage, RenderDebugViewMode viewMode)
        {
            if (stage == null || !stage.HasTextureData ||
                stage.TextureData.Texture.dimension != TextureDimension.Tex2D)
            {
                return null;
            }

            if (viewMode == RenderDebugViewMode.Captured)
                return stage.TextureData.IsCaptured ? stage.TextureData.Texture : null;

            return !stage.TextureData.IsCaptured && stage.TextureData.FrameIndex == Time.frameCount
                ? stage.TextureData.Texture
                : null;
        }

        private static Rect FitRect(Rect viewport, float width, float height)
        {
            float scale = Mathf.Min(viewport.width / Mathf.Max(1f, width), viewport.height / Mathf.Max(1f, height));
            float fittedWidth = width * scale;
            float fittedHeight = height * scale;
            return new Rect(
                viewport.x + (viewport.width - fittedWidth) * 0.5f,
                viewport.y + (viewport.height - fittedHeight) * 0.5f,
                fittedWidth,
                fittedHeight);
        }
    }
}
