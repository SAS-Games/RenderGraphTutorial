using System.Collections.Generic;
using SAS.UI.InfiniteScroll;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SAS.UI.Compass
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(InfiniteScrollRect))]
    public sealed class CompassView : MonoBehaviour, ICompassView
    {
        [Header("Optional authored hierarchy")] [SerializeField]
        private InfiniteScrollRect m_DirectionScrollRect;

        [SerializeField] private RectTransform m_Viewport;
        [SerializeField] private RectTransform m_Content;
        [SerializeField] private RectTransform m_MarkerLayer;
        [SerializeField] private TMP_Text m_CenterIndicator;

        private readonly List<RectTransform> _directionItems = new();
        private readonly Dictionary<RectTransform, CompassDirectionView> _directionViews = new();
        private readonly Dictionary<int, CompassMarkerView> _activeMarkerViews = new();
        private readonly Stack<CompassMarkerView> _markerPool = new();

        private CompassVisualSettings _settings;
        private int _nextMarkerHandle;

        public void Initialize(CompassVisualSettings visualSettings, IInfiniteScrollItemBinder directionBinder)
        {
            _settings = visualSettings;
            EnsureHierarchy();
            ConfigureHierarchy();
            BuildDirectionPool();
            CacheAuthoredMarkerViews();

            float itemSpacing = _settings.DirectionSet.AnglePerDirection * _settings.UnitsPerDegree;
            m_DirectionScrollRect.Configure(m_Viewport, m_Content, itemSpacing, _directionItems.Count, false);
            m_DirectionScrollRect.Initialize(directionBinder, _directionItems);
        }

        public void SetDirectionPosition(double logicalPosition)
        {
            m_DirectionScrollRect.SetPosition(logicalPosition);
        }

        public void BindDirection(RectTransform itemView, string label)
        {
            if (_directionViews.TryGetValue(itemView, out CompassDirectionView directionView))
                directionView.SetLabel(label);
        }

        public int AddMarker(ICompassMarker marker)
        {
            CompassMarkerView markerView = _markerPool.Count > 0 ? _markerPool.Pop() : CreateMarkerView();
            markerView.transform.SetAsLastSibling();
            markerView.Configure(marker, _settings);
            markerView.gameObject.SetActive(false);

            int handle = ++_nextMarkerHandle;
            _activeMarkerViews.Add(handle, markerView);
            return handle;
        }

        public void RemoveMarker(int handle)
        {
            if (!_activeMarkerViews.TryGetValue(handle, out CompassMarkerView markerView))
                return;

            _activeMarkerViews.Remove(handle);
            markerView.gameObject.SetActive(false);
            _markerPool.Push(markerView);
        }

        public void SetMarker(int handle, in CompassMarkerPresentation presentation)
        {
            if (_activeMarkerViews.TryGetValue(handle, out CompassMarkerView markerView))
                markerView.SetPresentation(in presentation);
        }

        public void Clear()
        {
            if (_activeMarkerViews.Count == 0)
                return;

            List<int> handles = ListPool<int>.Get();
            foreach (KeyValuePair<int, CompassMarkerView> pair in _activeMarkerViews)
                handles.Add(pair.Key);

            for (int i = 0; i < handles.Count; i++)
                RemoveMarker(handles[i]);

            ListPool<int>.Release(handles);
        }

        private void EnsureHierarchy()
        {
            RectTransform root = (RectTransform)transform;
            root.sizeDelta = _settings.ViewportSize;

            if (m_DirectionScrollRect == null)
                m_DirectionScrollRect = GetComponent<InfiniteScrollRect>();

            if (m_Viewport == null)
            {
                m_Viewport = CreateRectTransform("Viewport", root);
                Image background = m_Viewport.gameObject.AddComponent<Image>();
                background.raycastTarget = true;
                m_Viewport.gameObject.AddComponent<RectMask2D>();
                m_Viewport.gameObject.AddComponent<RectMaskEdgeFade>();
            }

            if (m_Content == null)
                m_Content = CreateRectTransform("Content", m_Viewport);

            if (m_MarkerLayer == null)
                m_MarkerLayer = CreateRectTransform("Marker Layer", m_Viewport);

            if (m_CenterIndicator == null)
                m_CenterIndicator = CreateText("Center Indicator", root);
        }

        private void ConfigureHierarchy()
        {
            StretchToParent(m_Viewport);
            StretchToParent(m_MarkerLayer);
            m_MarkerLayer.gameObject.SetActive(true);

            Image background = m_Viewport.GetComponent<Image>();
            if (background != null)
                background.color = _settings.BackgroundColor;

            RectMaskEdgeFade edgeFade = m_Viewport.GetComponent<RectMaskEdgeFade>();
            if (edgeFade != null)
                edgeFade.Configure(_settings.EdgeFadeFraction, 0f);

            m_Content.anchorMin = m_Content.anchorMax = new Vector2(0.5f, 0.5f);
            m_Content.pivot = new Vector2(0.5f, 0.5f);
            m_Content.anchoredPosition = Vector2.zero;
            m_Content.sizeDelta = _settings.ViewportSize;

            RectTransform centerRect = m_CenterIndicator.rectTransform;
            centerRect.anchorMin = new Vector2(0.5f, 0.5f);
            centerRect.anchorMax = new Vector2(0.5f, 0.5f);
            centerRect.pivot = new Vector2(0.5f, 0.5f);
            centerRect.anchoredPosition = new Vector2(0f, _settings.ViewportSize.y * 0.5f - 9f);
            centerRect.sizeDelta = new Vector2(28f, 24f);
            m_CenterIndicator.text = "▼";
            if (_settings.Font != null)
                m_CenterIndicator.font = _settings.Font;
            m_CenterIndicator.fontSize = 19f;
            m_CenterIndicator.fontStyle = FontStyles.Bold;
            m_CenterIndicator.alignment = TextAlignmentOptions.Center;
            m_CenterIndicator.color = _settings.CenterIndicatorColor;
            m_CenterIndicator.raycastTarget = false;
        }

        private void BuildDirectionPool()
        {
            float anglePerDirection = _settings.DirectionSet.AnglePerDirection;
            float itemSpacing = anglePerDirection * _settings.UnitsPerDegree;
            int minimumPoolSize = Mathf.CeilToInt(_settings.ViewportSize.x / itemSpacing) + 4;
            int requiredPoolSize = Mathf.Max(_settings.DirectionPoolSize, minimumPoolSize);

            if (_directionItems.Count == 0)
            {
                for (int i = 0; i < m_Content.childCount; i++)
                {
                    RectTransform itemRect = m_Content.GetChild(i) as RectTransform;
                    if (itemRect == null || !itemRect.TryGetComponent(out CompassDirectionView directionView))
                        continue;

                    _directionItems.Add(itemRect);
                    _directionViews.Add(itemRect, directionView);
                }
            }

            while (_directionItems.Count < requiredPoolSize)
            {
                string itemName = $"Direction Slot {_directionItems.Count:00}";
                RectTransform itemRect = CreateRectTransform(itemName, m_Content);
                itemRect.sizeDelta = new Vector2(itemSpacing, _settings.ViewportSize.y);

                TMP_Text label = CreateText("Direction Label", itemRect);
                CompassDirectionView directionView = itemRect.gameObject.AddComponent<CompassDirectionView>();
                directionView.Initialize(label, _settings, itemSpacing, anglePerDirection);

                _directionItems.Add(itemRect);
                _directionViews.Add(itemRect, directionView);
            }

            for (int i = 0; i < _directionItems.Count; i++)
            {
                RectTransform itemRect = _directionItems[i];
                itemRect.name = $"Direction Slot {i:00}";
                itemRect.sizeDelta = new Vector2(itemSpacing, _settings.ViewportSize.y);
                _directionViews[itemRect].Initialize(
                    _directionViews[itemRect].Label,
                    _settings,
                    itemSpacing,
                    anglePerDirection);
            }
        }

        private CompassMarkerView CreateMarkerView()
        {
            int slotIndex = _markerPool.Count + _activeMarkerViews.Count;
            RectTransform markerRect = CreateRectTransform($"Marker Slot {slotIndex:00}", m_MarkerLayer);
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            Image icon = markerRect.gameObject.AddComponent<Image>();

            TMP_Text nameLabel = CreateText("Name", markerRect);
            RectTransform nameRect = nameLabel.rectTransform;
            nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -3f);
            nameRect.sizeDelta = new Vector2(120f, 20f);

            TMP_Text distanceLabel = CreateText("Distance", markerRect);
            RectTransform distanceRect = distanceLabel.rectTransform;
            distanceRect.anchorMin = distanceRect.anchorMax = new Vector2(0.5f, 0f);
            distanceRect.pivot = new Vector2(0.5f, 1f);
            distanceRect.anchoredPosition = new Vector2(0f, -21f);
            distanceRect.sizeDelta = new Vector2(90f, 20f);

            CompassMarkerView markerView = markerRect.gameObject.AddComponent<CompassMarkerView>();
            markerView.Initialize(markerRect, icon, nameLabel, distanceLabel);
            return markerView;
        }

        private void CacheAuthoredMarkerViews()
        {
            if (_markerPool.Count > 0 || _activeMarkerViews.Count > 0)
                return;

            for (int i = 0; i < m_MarkerLayer.childCount; i++)
            {
                Transform child = m_MarkerLayer.GetChild(i);
                if (!child.TryGetComponent(out CompassMarkerView markerView))
                    continue;

                markerView.name = $"Marker Slot {i:00}";
                markerView.gameObject.SetActive(false);
                _markerPool.Push(markerView);
            }
        }

        private static RectTransform CreateRectTransform(string objectName, Transform parent)
        {
            GameObject instance = new GameObject(objectName, typeof(RectTransform));
            RectTransform rectTransform = (RectTransform)instance.transform;
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }

        private static TMP_Text CreateText(string objectName, Transform parent)
        {
            RectTransform rectTransform = CreateRectTransform(objectName, parent);
            return rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> pool = new Stack<List<T>>();

            public static List<T> Get()
            {
                return pool.Count > 0 ? pool.Pop() : new List<T>();
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                pool.Push(list);
            }
        }
    }
}