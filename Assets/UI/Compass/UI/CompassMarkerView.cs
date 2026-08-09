using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SAS.UI.Compass
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public sealed class CompassMarkerView : MonoBehaviour
    {
        [SerializeField] private RectTransform m_RectTransform;
        [SerializeField] private Image m_Icon;
        [SerializeField] private TMP_Text m_NameLabel;
        [SerializeField] private TMP_Text m_DistanceLabel;

        private float _verticalOffset;
        private bool _showDistance;

        public void Initialize(RectTransform targetRectTransform, Image targetIcon, TMP_Text targetNameLabel, TMP_Text targetDistanceLabel)
        {
            m_RectTransform = targetRectTransform;
            m_Icon = targetIcon;
            m_NameLabel = targetNameLabel;
            m_DistanceLabel = targetDistanceLabel;
        }

        public void Configure(ICompassMarker marker, CompassVisualSettings settings)
        {
            CacheComponents();

            CompassMarkerVisualSettings visual = settings.GetMarkerVisual(marker.Type);
            m_Icon.sprite = visual != null ? visual.Sprite : null;
            m_Icon.color = visual != null ? visual.Color : GetDefaultColor(marker.Type);
            m_Icon.raycastTarget = false;
            m_RectTransform.sizeDelta = visual != null ? visual.Size : new Vector2(24f, 24f);

            m_NameLabel.text = marker.Label;
            if (settings.Font != null)
                m_NameLabel.font = settings.Font;
            m_NameLabel.fontSize = settings.MarkerFontSize;
            m_NameLabel.color = m_Icon.color;
            m_NameLabel.alignment = TextAlignmentOptions.Center;
            m_NameLabel.raycastTarget = false;

            if (settings.Font != null)
                m_DistanceLabel.font = settings.Font;
            m_DistanceLabel.fontSize = settings.MarkerFontSize;
            m_DistanceLabel.color = m_Icon.color;
            m_DistanceLabel.alignment = TextAlignmentOptions.Center;
            m_DistanceLabel.raycastTarget = false;

            _verticalOffset = settings.MarkerVerticalOffset;
            _showDistance = settings.ShowMarkerDistance;
            m_DistanceLabel.gameObject.SetActive(_showDistance);
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        public void SetPresentation(in CompassMarkerPresentation presentation)
        {
            m_RectTransform.anchoredPosition = new Vector2(presentation.LocalX, _verticalOffset);
            if (_showDistance)
                m_DistanceLabel.SetText("{0:0} m", presentation.Distance);

            if (gameObject.activeSelf != presentation.IsVisible)
                gameObject.SetActive(presentation.IsVisible);
        }

        private static Color GetDefaultColor(CompassMarkerType markerType)
        {
            switch (markerType)
            {
                case CompassMarkerType.Objective:
                    return new Color(1f, 0.78f, 0.18f, 1f);
                case CompassMarkerType.Enemy:
                    return new Color(1f, 0.25f, 0.2f, 1f);
                case CompassMarkerType.PointOfInterest:
                    return new Color(0.25f, 0.75f, 1f, 1f);
                default:
                    return Color.white;
            }
        }

        private void CacheComponents()
        {
            if (m_RectTransform == null)
                m_RectTransform = (RectTransform)transform;
            if (m_Icon == null)
                m_Icon = GetComponent<Image>();
        }
    }
}
