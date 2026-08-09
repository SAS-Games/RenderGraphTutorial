using UnityEngine;

namespace SAS.UI.Compass
{
    [CreateAssetMenu(fileName = "CompassVisualSettings", menuName = "SAS/UI/Compass Visual Settings")]
    public sealed class CompassVisualSettings : ScriptableObject
    {
        [field: Header("Directions")]
        [field: SerializeField] public CompassDirectionSet DirectionSet { get; private set; }
        [field: SerializeField, Min(3)] public int DirectionPoolSize { get; private set; } = 9;
        [field: SerializeField, Min(0.1f)] public float UnitsPerDegree { get; private set; } = 6f;
        [field: SerializeField, Min(1f)] public float MinorTickInterval { get; private set; } = 5f;

        [field: Header("Layout")]
        [field: SerializeField, Min(1f)] public float VisibleHalfAngle { get; private set; } = 65f;
        [field: SerializeField] public Vector2 ViewportSize { get; private set; } = new Vector2(780f, 92f);
        [field: SerializeField, Range(0f, 0.45f)] public float EdgeFadeFraction { get; private set; } = 0.12f;
        [field: SerializeField] public float DirectionVerticalOffset { get; private set; } = 7f;

        [field: Header("Scale")]
        [field: SerializeField, Min(1f)] public float MinorTickHeight { get; private set; } = 7f;
        [field: SerializeField, Min(1f)] public float MajorTickHeight { get; private set; } = 15f;
        [field: SerializeField, Min(1f)] public float TickWidth { get; private set; } = 2f;

        [field: Header("Typography")]
        [field: SerializeField] public TMPro.TMP_FontAsset Font { get; private set; }
        [field: SerializeField, Min(1f)] public float DirectionFontSize { get; private set; } = 22f;
        [field: SerializeField, Min(1f)] public float MarkerFontSize { get; private set; } = 12f;

        [field: Header("Appearance")]
        [field: SerializeField] public Color ScaleColor { get; private set; } = new Color(1f, 1f, 1f, 0.9f);
        [field: SerializeField] public Color CenterIndicatorColor { get; private set; } = new Color(1f, 0.78f, 0.18f, 1f);
        [field: SerializeField] public Color BackgroundColor { get; private set; } = new Color(0f, 0f, 0f, 0.2f);

        [field: Header("Markers")] 
        [field: SerializeField] public bool ShowMarkerDistance { get; private set; } = true;
        [field: SerializeField] public float MarkerVerticalOffset { get; private set; } = -20f;

        [SerializeField] private CompassMarkerVisualSettings[] markerVisuals = new CompassMarkerVisualSettings[0];

        public CompassMarkerVisualSettings GetMarkerVisual(CompassMarkerType markerType)
        {
            if (markerVisuals == null)
                return null;

            for (int i = 0; i < markerVisuals.Length; i++)
            {
                CompassMarkerVisualSettings visual = markerVisuals[i];
                if (visual != null && visual.Type == markerType)
                    return visual;
            }

            return null;
        }

        private void OnValidate()
        {
            DirectionPoolSize = Mathf.Max(3, DirectionPoolSize);
            UnitsPerDegree = Mathf.Max(0.1f, UnitsPerDegree);
            MinorTickInterval = Mathf.Clamp(MinorTickInterval, 1f, 180f);
            VisibleHalfAngle = Mathf.Clamp(VisibleHalfAngle, 1f, 180f);
            ViewportSize = new Vector2(Mathf.Max(1f, ViewportSize.x), Mathf.Max(1f, ViewportSize.y));
        }
    }
}
