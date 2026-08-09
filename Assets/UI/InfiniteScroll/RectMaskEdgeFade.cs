using UnityEngine;
using UnityEngine.UI;

namespace SAS.UI.InfiniteScroll
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(RectMask2D))]
    public sealed class RectMaskEdgeFade : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.45f)] private float horizontalFadeFraction = 0.12f;
        [SerializeField, Range(0f, 0.45f)] private float verticalFadeFraction;

        private RectTransform rectTransform;
        private RectMask2D rectMask;

        public void Configure(float horizontalFraction, float verticalFraction = 0f)
        {
            horizontalFadeFraction = Mathf.Clamp(horizontalFraction, 0f, 0.45f);
            verticalFadeFraction = Mathf.Clamp(verticalFraction, 0f, 0.45f);
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
            horizontalFadeFraction = Mathf.Clamp(horizontalFadeFraction, 0f, 0.45f);
            verticalFadeFraction = Mathf.Clamp(verticalFadeFraction, 0f, 0.45f);
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
            if (rectTransform == null)
                rectTransform = (RectTransform)transform;

            if (rectMask == null)
                rectMask = GetComponent<RectMask2D>();
        }

        private void ApplySoftness()
        {
            if (rectTransform == null || rectMask == null)
                return;

            Rect rect = rectTransform.rect;
            int horizontalSoftness = Mathf.RoundToInt(rect.width * horizontalFadeFraction);
            int verticalSoftness = Mathf.RoundToInt(rect.height * verticalFadeFraction);
            rectMask.softness = new Vector2Int(horizontalSoftness, verticalSoftness);
        }
    }
}
