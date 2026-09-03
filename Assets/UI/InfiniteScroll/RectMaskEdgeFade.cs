using UnityEngine;
using UnityEngine.UI;

namespace SAS.UI.InfiniteScroll
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(RectMask2D))]
    public sealed class RectMaskEdgeFade : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.45f)] private float m_HorizontalFadeFraction = 0.12f;
        [SerializeField, Range(0f, 0.45f)] private float m_VerticalFadeFraction;

        private RectTransform _rectTransform;
        private RectMask2D _rectMask;

        public void Configure(float horizontalFraction, float verticalFraction = 0f)
        {
            m_HorizontalFadeFraction = Mathf.Clamp(horizontalFraction, 0f, 0.45f);
            m_VerticalFadeFraction = Mathf.Clamp(verticalFraction, 0f, 0.45f);
            CacheComponents();
            ApplySoftness();
        }

        private void OnEnable()
        {
            CacheComponents();
            ApplySoftness();
        }

        private void OnValidate()
        {
            m_HorizontalFadeFraction = Mathf.Clamp(m_HorizontalFadeFraction, 0f, 0.45f);
            m_VerticalFadeFraction = Mathf.Clamp(m_VerticalFadeFraction, 0f, 0.45f);
            CacheComponents();
            ApplySoftness();
        }

        private void OnRectTransformDimensionsChange()
        {
            CacheComponents();
            ApplySoftness();
        }

        private void CacheComponents()
        {
            if (_rectTransform == null)
                _rectTransform = (RectTransform)transform;

            if (_rectMask == null)
                _rectMask = GetComponent<RectMask2D>();
        }

        private void ApplySoftness()
        {
            if (_rectTransform == null || _rectMask == null)
                return;

            Rect rect = _rectTransform.rect;
            int horizontalSoftness = Mathf.RoundToInt(rect.width * m_HorizontalFadeFraction);
            int verticalSoftness = Mathf.RoundToInt(rect.height * m_VerticalFadeFraction);
            _rectMask.softness = new Vector2Int(horizontalSoftness, verticalSoftness);
        }
    }
}
