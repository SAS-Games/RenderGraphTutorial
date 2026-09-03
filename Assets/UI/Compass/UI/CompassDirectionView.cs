using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SAS.UI.Compass
{
    [DisallowMultipleComponent]
    public sealed class CompassDirectionView : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_Label;
        private readonly List<Image> _ticks = new List<Image>();
        public TMP_Text Label => m_Label;

        public void Initialize(TMP_Text targetLabel, CompassVisualSettings settings, float itemSpacing, float anglePerDirection)
        {
            m_Label = targetLabel;
            m_Label.name = "Direction Label";

            if (settings.Font != null)
                m_Label.font = settings.Font;
            m_Label.fontSize = settings.DirectionFontSize;
            m_Label.fontStyle = FontStyles.Bold;
            m_Label.color = settings.ScaleColor;
            m_Label.alignment = TextAlignmentOptions.Center;
            m_Label.raycastTarget = false;

            RectTransform labelRect = m_Label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition =
                new Vector2(0f, settings.DirectionVerticalOffset + settings.MajorTickHeight + 12f);
            labelRect.sizeDelta = new Vector2(80f, settings.DirectionFontSize + 8f);

            ConfigureTicks(settings, itemSpacing, anglePerDirection);
        }

        public void SetLabel(string text)
        {
            m_Label.text = text;
        }

        private void ConfigureTicks(CompassVisualSettings settings, float itemSpacing, float anglePerDirection)
        {
            int intervalCount = Mathf.Max(1, Mathf.RoundToInt(anglePerDirection / settings.MinorTickInterval));

            if (_ticks.Count == 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child.TryGetComponent(out Image existingTick))
                        _ticks.Add(existingTick);
                }
            }

            while (_ticks.Count < intervalCount)
            {
                RectTransform tickRect = CreateRectTransform("Minor Tick", transform);
                _ticks.Add(tickRect.gameObject.AddComponent<Image>());
            }

            for (int i = 0; i < _ticks.Count; i++)
            {
                Image image = _ticks[i];
                bool isVisible = i < intervalCount;
                image.gameObject.SetActive(isVisible);
                if (!isVisible)
                    continue;

                bool isMajor = i == 0;
                image.name = isMajor ? "Major Tick" : $"Minor Tick {i:00}";
                RectTransform tickRect = image.rectTransform;
                tickRect.anchorMin = tickRect.anchorMax = new Vector2(0.5f, 0.5f);
                tickRect.pivot = new Vector2(0.5f, 0.5f);
                tickRect.anchoredPosition =
                    new Vector2(itemSpacing * i / intervalCount, settings.DirectionVerticalOffset);
                tickRect.sizeDelta = new Vector2(
                    settings.TickWidth,
                    isMajor ? settings.MajorTickHeight : settings.MinorTickHeight);

                image.color = settings.ScaleColor;
                image.raycastTarget = false;
            }
        }

        private static RectTransform CreateRectTransform(string objectName, Transform parent)
        {
            GameObject instance = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = (RectTransform)instance.transform;
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }
    }
}