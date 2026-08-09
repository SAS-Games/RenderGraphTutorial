using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SAS.UI.InfiniteScroll
{
    [DisallowMultipleComponent]
    public sealed class InfiniteScrollRect : ScrollRect
    {
        [FormerlySerializedAs("itemSpacing")]
        [Header("Infinite Scroll")]
        [SerializeField, Min(1f)] private float m_ItemSpacing = 100f;
        [FormerlySerializedAs("poolSize")] [SerializeField, Min(3)] private int m_PoolSize = 9;
        [FormerlySerializedAs("itemPrefab")] [SerializeField] private RectTransform m_ItemPrefab;
        [FormerlySerializedAs("allowUserInteraction")] [SerializeField] private bool m_AllowUserInteraction = true;

        private readonly List<ScrollItem> _scrollItems = new List<ScrollItem>();

        private IInfiniteScrollItemBinder _binder;
        private double _logicalPosition;
        private long _baseLogicalIndex;
        private long _centerLogicalIndex;
        private double _dragStartLogicalPosition;
        private float _dragStartContentPosition;
        private bool _isInitialized;

        public event Action<double> PositionChanged;
        public event Action<long> CenterIndexChanged;

        public double LogicalPosition => _logicalPosition;
        public long CenterLogicalIndex => _centerLogicalIndex;
        public float FractionToNextItem => (float)(_logicalPosition - Math.Floor(_logicalPosition));
        public bool mAllowUserInteraction => m_AllowUserInteraction;

        public void Configure(RectTransform targetViewport, RectTransform targetContent, float targetItemSpacing, int targetPoolSize, bool userInteraction)
        {
            viewport = targetViewport;
            content = targetContent;
            m_ItemSpacing = Mathf.Max(1f, targetItemSpacing);
            m_PoolSize = Mathf.Max(3, targetPoolSize);
            m_AllowUserInteraction = userInteraction;
            ApplyScrollRectSettings();
        }

        public void Initialize(IInfiniteScrollItemBinder itemBinder)
        {
            if (_isInitialized)
            {
                SetBinder(itemBinder);
                return;
            }

            if (content == null)
                throw new InvalidOperationException("InfiniteScrollRect requires a Content RectTransform.");

            _binder = itemBinder;
            CollectOrCreateItems();
            if (_scrollItems.Count < 3)
                throw new InvalidOperationException("InfiniteScrollRect requires at least three physical item views.");

            ConfigureTransforms();
            _isInitialized = true;
            RebindAll(FloorToLong(_logicalPosition));
            ApplyPosition(false);
        }

        public void Initialize(IInfiniteScrollItemBinder itemBinder, IReadOnlyList<RectTransform> itemViews)
        {
            if (itemViews == null)
                throw new ArgumentNullException(nameof(itemViews));
            if (itemViews.Count < 3)
                throw new ArgumentException("InfiniteScrollRect requires at least three physical item views.", nameof(itemViews));

            _binder = itemBinder;
            _scrollItems.Clear();

            for (int i = 0; i < itemViews.Count; i++)
            {
                RectTransform itemView = itemViews[i];
                if (itemView == null)
                    throw new ArgumentException("The physical item list contains a null view.", nameof(itemViews));

                _scrollItems.Add(new ScrollItem(itemView));
            }

            m_PoolSize = _scrollItems.Count;
            ConfigureTransforms();
            _isInitialized = true;
            RebindAll(FloorToLong(_logicalPosition));
            ApplyPosition(false);
        }

        public void SetBinder(IInfiniteScrollItemBinder itemBinder)
        {
            _binder = itemBinder;
            if (!_isInitialized || _binder == null)
                return;

            for (int i = 0; i < _scrollItems.Count; i++)
                _binder.Bind(_scrollItems[i].View, _scrollItems[i].LogicalIndex);
        }

        public void SetPosition(double position)
        {
            if (double.IsNaN(position) || double.IsInfinity(position))
                throw new ArgumentOutOfRangeException(nameof(position), "Logical position must be finite.");

            if (!_isInitialized)
                Initialize(_binder);

            if (Math.Abs(_logicalPosition - position) <= double.Epsilon)
                return;

            _logicalPosition = position;
            ApplyPosition(true);
        }

        public void ScrollToLogicalPosition(double position)
        {
            SetPosition(position);
        }

        public void SetAllowUserInteraction(bool allow)
        {
            m_AllowUserInteraction = allow;
            velocity = Vector2.zero;
            ApplyScrollRectSettings();
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (m_AllowUserInteraction)
                base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (!m_AllowUserInteraction)
                return;

            _dragStartLogicalPosition = _logicalPosition;
            _dragStartContentPosition = content.anchoredPosition.x;
            base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (!m_AllowUserInteraction)
                return;

            base.OnDrag(eventData);
            float contentDelta = content.anchoredPosition.x - _dragStartContentPosition;
            _logicalPosition = _dragStartLogicalPosition - contentDelta / m_ItemSpacing;
            ApplyPosition(true);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (!m_AllowUserInteraction)
                return;

            base.OnEndDrag(eventData);
            velocity = Vector2.zero;
        }

        public override void OnScroll(PointerEventData eventData)
        {
            if (!m_AllowUserInteraction)
                return;

            float previousContentPosition = content.anchoredPosition.x;
            base.OnScroll(eventData);
            float contentDelta = content.anchoredPosition.x - previousContentPosition;
            _logicalPosition -= contentDelta / m_ItemSpacing;
            ApplyPosition(true);
        }

        protected override void Awake()
        {
            base.Awake();
            ApplyScrollRectSettings();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            m_ItemSpacing = Mathf.Max(1f, m_ItemSpacing);
            m_PoolSize = Mathf.Max(3, m_PoolSize);
            ApplyScrollRectSettings();
        }

        private void ApplyPosition(bool notify)
        {
            long newBaseIndex = FloorToLong(_logicalPosition);
            if (newBaseIndex != _baseLogicalIndex)
                RecycleTo(newBaseIndex);

            double fraction = _logicalPosition - newBaseIndex;
            Vector2 contentPosition = content.anchoredPosition;
            contentPosition.x = (float)(-fraction * m_ItemSpacing);
            contentPosition.y = 0f;
            content.anchoredPosition = contentPosition;
            velocity = Vector2.zero;

            long newCenterIndex = FloorToLong(_logicalPosition + 0.5d);
            if (newCenterIndex != _centerLogicalIndex)
            {
                _centerLogicalIndex = newCenterIndex;
                CenterIndexChanged?.Invoke(_centerLogicalIndex);
            }

            if (notify)
                PositionChanged?.Invoke(_logicalPosition);
        }

        private void RecycleTo(long newBaseIndex)
        {
            long difference = newBaseIndex - _baseLogicalIndex;
            if (Math.Abs((double)difference) >= _scrollItems.Count)
            {
                RebindAll(newBaseIndex);
                return;
            }

            while (_baseLogicalIndex < newBaseIndex)
            {
                ScrollItem first = _scrollItems[0];
                _scrollItems.RemoveAt(0);
                ScrollItem last = _scrollItems[_scrollItems.Count - 1];
                first.LogicalIndex = last.LogicalIndex + 1;
                _scrollItems.Add(first);
                _binder?.Bind(first.View, first.LogicalIndex);
                _baseLogicalIndex++;
            }

            while (_baseLogicalIndex > newBaseIndex)
            {
                int lastIndex = _scrollItems.Count - 1;
                ScrollItem last = _scrollItems[lastIndex];
                _scrollItems.RemoveAt(lastIndex);
                ScrollItem first = _scrollItems[0];
                last.LogicalIndex = first.LogicalIndex - 1;
                _scrollItems.Insert(0, last);
                _binder?.Bind(last.View, last.LogicalIndex);
                _baseLogicalIndex--;
            }

            PositionPhysicalItems();
        }

        private void RebindAll(long newBaseIndex)
        {
            _baseLogicalIndex = newBaseIndex;
            int leftBufferCount = _scrollItems.Count / 2;
            long firstLogicalIndex = _baseLogicalIndex - leftBufferCount;

            for (int i = 0; i < _scrollItems.Count; i++)
            {
                ScrollItem item = _scrollItems[i];
                item.LogicalIndex = firstLogicalIndex + i;
                _binder?.Bind(item.View, item.LogicalIndex);
            }

            PositionPhysicalItems();
        }

        private void PositionPhysicalItems()
        {
            for (int i = 0; i < _scrollItems.Count; i++)
            {
                ScrollItem item = _scrollItems[i];
                Vector2 position = item.View.anchoredPosition;
                position.x = (float)(item.LogicalIndex - _baseLogicalIndex) * m_ItemSpacing;
                position.y = 0f;
                item.View.anchoredPosition = position;
                item.View.SetSiblingIndex(i);
            }
        }

        private void CollectOrCreateItems()
        {
            _scrollItems.Clear();

            for (int i = 0; i < content.childCount && _scrollItems.Count < m_PoolSize; i++)
            {
                RectTransform child = content.GetChild(i) as RectTransform;
                if (child != null && child != m_ItemPrefab)
                    _scrollItems.Add(new ScrollItem(child));
            }

            while (_scrollItems.Count < m_PoolSize && m_ItemPrefab != null)
            {
                RectTransform item = Instantiate(m_ItemPrefab, content);
                item.gameObject.SetActive(true);
                _scrollItems.Add(new ScrollItem(item));
            }
        }

        private void ConfigureTransforms()
        {
            ApplyScrollRectSettings();

            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = viewport != null ? viewport.rect.size : Vector2.zero;

            for (int i = 0; i < _scrollItems.Count; i++)
            {
                RectTransform item = _scrollItems[i].View;
                item.SetParent(content, false);
                item.anchorMin = new Vector2(0.5f, 0.5f);
                item.anchorMax = new Vector2(0.5f, 0.5f);
                item.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        private void ApplyScrollRectSettings()
        {
            horizontal = true;
            vertical = false;
            movementType = MovementType.Unrestricted;
            inertia = false;
            horizontalScrollbar = null;
            verticalScrollbar = null;
        }

        private static long FloorToLong(double value)
        {
            if (value >= long.MaxValue)
                return long.MaxValue;
            if (value <= long.MinValue)
                return long.MinValue;

            return (long)Math.Floor(value);
        }

        private sealed class ScrollItem
        {
            public ScrollItem(RectTransform view)
            {
                View = view;
            }

            public RectTransform View { get; }
            public long LogicalIndex { get; set; }
        }
    }
}
