using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SAS.UI.InfiniteScroll
{
    public enum InfiniteScrollLayoutMode
    {
        Horizontal,
        Vertical,
        HorizontalGrid,
        VerticalGrid
    }

    [DisallowMultipleComponent]
    public sealed class InfiniteScrollRect : ScrollRect
    {
        private const double MaxExactInteger = 9007199254740991d;

        [Header("Infinite Scroll")] 
        [SerializeField] private InfiniteScrollLayoutMode m_LayoutMode = InfiniteScrollLayoutMode.Horizontal;
        [SerializeField, Min(1f)] private float m_ItemSpacing = 100f;

        [SerializeField, Min(1f)] private float m_CrossAxisSpacing = 100f;
        [SerializeField, Min(1)] private int m_CrossAxisCount = 1;
        [SerializeField, Min(2)] private int m_ExtraBufferGroups = 2;
        [SerializeField, Min(3)] private int m_PoolSize = 9;
        [SerializeField] private RectTransform m_ItemPrefab;
        [SerializeField] private bool m_AllowUserInteraction = true;

        private readonly List<ScrollItem> _scrollItems = new List<ScrollItem>();
        private readonly List<ScrollItem> _recycleBuffer = new List<ScrollItem>();

        private IInfiniteScrollItemBinder _binder;
        private LayoutGroup _layoutGroup;
        private Vector2 _mainAxisDirection = Vector2.right;
        private Vector2 _crossAxisDirection = Vector2.down;
        private double _logicalPosition;
        private long _baseLogicalIndex;
        private long _centerLogicalIndex;
        private double _dragStartLogicalPosition;
        private float _dragStartContentPosition;
        private float _effectiveItemSpacing = 100f;
        private float _effectiveCrossAxisSpacing = 100f;
        private int _effectiveCrossAxisCount = 1;
        private bool _usesLayoutGroup;
        private bool _isInitialized;
        private bool _isRebuilding;

        public event Action<double> PositionChanged;
        public event Action<long> CenterIndexChanged;

        public double LogicalPosition => _logicalPosition;
        public long CenterLogicalIndex => _centerLogicalIndex;
        public float FractionToNextItem => (float)(_logicalPosition - Math.Floor(_logicalPosition));
        public bool mAllowUserInteraction => m_AllowUserInteraction;
        public InfiniteScrollLayoutMode LayoutMode => m_LayoutMode;
        public int CrossAxisCount => _effectiveCrossAxisCount;
        public int PhysicalItemCount => _scrollItems.Count;

        public void Configure(RectTransform targetViewport, RectTransform targetContent, float targetItemSpacing, int targetPoolSize, bool userInteraction)
        {
            Configure(targetViewport, targetContent, InfiniteScrollLayoutMode.Horizontal, targetItemSpacing, targetItemSpacing, 1, targetPoolSize, userInteraction);
        }

        public void Configure(RectTransform targetViewport, RectTransform targetContent, InfiniteScrollLayoutMode layoutMode, float mainAxisSpacing, float crossAxisSpacing, int crossAxisCount, int targetPoolSize, bool userInteraction)
        {
            viewport = targetViewport;
            content = targetContent;
            m_LayoutMode = layoutMode;
            m_ItemSpacing = Mathf.Max(1f, mainAxisSpacing);
            m_CrossAxisSpacing = Mathf.Max(1f, crossAxisSpacing);
            m_CrossAxisCount = Mathf.Max(1, crossAxisCount);
            m_PoolSize = Mathf.Max(3, targetPoolSize);
            m_AllowUserInteraction = userInteraction;
            ApplyScrollRectSettings();

            if (_isInitialized)
                RebuildInitializedPool();
        }

        public void Initialize(IInfiniteScrollItemBinder itemBinder)
        {
            if (_isInitialized)
            {
                SetBinder(itemBinder);
                return;
            }

            EnsureContentIsAssigned();
            _binder = itemBinder;
            PrepareContentRect();
            ResolveLayoutConfiguration();
            CollectItems();
            CompleteInitialization();
        }

        public void Initialize(IInfiniteScrollItemBinder itemBinder, IReadOnlyList<RectTransform> itemViews)
        {
            if (itemViews == null)
                throw new ArgumentNullException(nameof(itemViews));
            EnsureContentIsAssigned();
            _binder = itemBinder;
            PrepareContentRect();
            ResolveLayoutConfiguration();
            _scrollItems.Clear();

            for (int i = 0; i < itemViews.Count; i++)
            {
                RectTransform itemView = itemViews[i];
                if (itemView == null)
                    throw new ArgumentException("The physical item list contains a null view.", nameof(itemViews));

                _scrollItems.Add(new ScrollItem(itemView));
            }

            CompleteInitialization();
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

            ValidateLogicalPosition(position);
            if (_logicalPosition == position)
                return;

            _logicalPosition = position;
            ApplyPosition(true);
        }

        public void ScrollToLogicalPosition(double position)
        {
            SetPosition(position);
        }

        public void RefreshLayout()
        {
            if (_isInitialized && !_isRebuilding)
                RebuildInitializedPool();
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

            if (!_isInitialized)
                Initialize(_binder);

            _dragStartLogicalPosition = _logicalPosition;
            _dragStartContentPosition = GetMainAxisCoordinate(content.anchoredPosition);
            base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (!m_AllowUserInteraction)
                return;

            base.OnDrag(eventData);
            float currentContentPosition = GetMainAxisCoordinate(content.anchoredPosition);
            float contentDelta = currentContentPosition - _dragStartContentPosition;
            SetPositionFromInteraction(_dragStartLogicalPosition - contentDelta / _effectiveItemSpacing);
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

            if (!_isInitialized)
                Initialize(_binder);

            float previousContentPosition = GetMainAxisCoordinate(content.anchoredPosition);
            base.OnScroll(eventData);
            float currentContentPosition = GetMainAxisCoordinate(content.anchoredPosition);
            float contentDelta = currentContentPosition - previousContentPosition;
            SetPositionFromInteraction(_logicalPosition - contentDelta / _effectiveItemSpacing);
        }

        protected override void Awake()
        {
            base.Awake();
            ApplyScrollRectSettings();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (_isInitialized && !_isRebuilding)
                RebuildInitializedPool();
        }
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            m_ItemSpacing = Mathf.Max(1f, m_ItemSpacing);
            m_CrossAxisSpacing = Mathf.Max(1f, m_CrossAxisSpacing);
            m_CrossAxisCount = Mathf.Max(1, m_CrossAxisCount);
            m_ExtraBufferGroups = Mathf.Max(2, m_ExtraBufferGroups);
            m_PoolSize = Mathf.Max(3, m_PoolSize);
            ApplyScrollRectSettings();
        }
#endif

        private void ApplyPosition(bool notify)
        {
            long newBaseIndex = FloorToLong(_logicalPosition);
            if (newBaseIndex != _baseLogicalIndex)
                RecycleTo(newBaseIndex);

            double fraction = _logicalPosition - newBaseIndex;
            float referencePosition = GetBaseItemMainAxisPosition();
            float contentMainAxisPosition = -referencePosition - (float)(fraction * _effectiveItemSpacing);
            Vector2 contentPosition = content.anchoredPosition;
            SetAxisCoordinate(ref contentPosition, _mainAxisDirection, contentMainAxisPosition);
            SetAxisCoordinate(ref contentPosition, _crossAxisDirection, 0f);
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
            double difference = Math.Abs((double)newBaseIndex - _baseLogicalIndex);
            if (difference >= PoolGroupCount)
            {
                RebindAll(newBaseIndex);
                return;
            }

            while (_baseLogicalIndex < newBaseIndex)
            {
                RecycleFirstGroupToEnd();
                _baseLogicalIndex++;
            }

            while (_baseLogicalIndex > newBaseIndex)
            {
                RecycleLastGroupToStart();
                _baseLogicalIndex--;
            }

            PositionPhysicalItems();
        }

        private void RecycleFirstGroupToEnd()
        {
            _recycleBuffer.Clear();
            for (int i = 0; i < _effectiveCrossAxisCount; i++)
                _recycleBuffer.Add(_scrollItems[i]);

            _scrollItems.RemoveRange(0, _effectiveCrossAxisCount);
            long newGroupIndex = checked(_scrollItems[_scrollItems.Count - 1].LogicalGroupIndex + 1L);

            for (int i = 0; i < _recycleBuffer.Count; i++)
            {
                ScrollItem item = _recycleBuffer[i];
                BindItem(item, newGroupIndex, i);
                _scrollItems.Add(item);
            }
        }

        private void RecycleLastGroupToStart()
        {
            int removalIndex = _scrollItems.Count - _effectiveCrossAxisCount;
            _recycleBuffer.Clear();
            for (int i = 0; i < _effectiveCrossAxisCount; i++)
                _recycleBuffer.Add(_scrollItems[removalIndex + i]);

            _scrollItems.RemoveRange(removalIndex, _effectiveCrossAxisCount);
            long newGroupIndex = checked(_scrollItems[0].LogicalGroupIndex - 1L);

            for (int i = 0; i < _recycleBuffer.Count; i++)
                BindItem(_recycleBuffer[i], newGroupIndex, i);

            _scrollItems.InsertRange(0, _recycleBuffer);
        }

        private void RebindAll(long newBaseIndex)
        {
            _baseLogicalIndex = newBaseIndex;
            int leftBufferCount = PoolGroupCount / 2;
            long firstLogicalGroupIndex = checked(_baseLogicalIndex - leftBufferCount);

            for (int i = 0; i < _scrollItems.Count; i++)
            {
                long logicalGroupIndex = checked(firstLogicalGroupIndex + i / _effectiveCrossAxisCount);
                BindItem(_scrollItems[i], logicalGroupIndex, i % _effectiveCrossAxisCount);
            }

            PositionPhysicalItems();
        }

        private void BindItem(ScrollItem item, long logicalGroupIndex, int crossAxisIndex)
        {
            item.LogicalGroupIndex = logicalGroupIndex;
            item.CrossAxisIndex = crossAxisIndex;
            item.LogicalIndex = checked(logicalGroupIndex * _effectiveCrossAxisCount + crossAxisIndex);
            _binder?.Bind(item.View, item.LogicalIndex);
        }

        private void PositionPhysicalItems()
        {
            for (int i = 0; i < _scrollItems.Count; i++)
            {
                ScrollItem item = _scrollItems[i];
                item.View.SetSiblingIndex(i);

                if (_usesLayoutGroup)
                    continue;

                float mainPosition = (float)(item.LogicalGroupIndex - _baseLogicalIndex) * _effectiveItemSpacing;
                float crossPosition = (item.CrossAxisIndex - (_effectiveCrossAxisCount - 1) * 0.5f) *
                                      _effectiveCrossAxisSpacing;
                item.View.anchoredPosition = _mainAxisDirection * mainPosition + _crossAxisDirection * crossPosition;
            }

            ForceLayoutRebuild();
        }

        private void CollectItems()
        {
            _scrollItems.Clear();

            if (m_ItemPrefab != null && m_ItemPrefab.parent == content)
                m_ItemPrefab.gameObject.SetActive(false);

            for (int i = 0; i < content.childCount; i++)
            {
                RectTransform child = content.GetChild(i) as RectTransform;
                if (child != null && child != m_ItemPrefab)
                    _scrollItems.Add(new ScrollItem(child));
            }
        }

        private void EnsurePoolCapacity(int minimumGroupCount)
        {
            int crossAxisCount = Mathf.Max(1, _effectiveCrossAxisCount);
            int remainder = _scrollItems.Count % crossAxisCount;
            if (remainder != 0 && m_ItemPrefab == null)
            {
                throw new InvalidOperationException($"InfiniteScrollRect grid pools must contain a multiple of {crossAxisCount} views.  The current pool contains {_scrollItems.Count} views and no Item Prefab can complete the group.");
            }

            int configuredGroups = Mathf.CeilToInt(m_PoolSize / (float)crossAxisCount);
            int targetGroups = Mathf.Max(3, minimumGroupCount);
            if (m_ItemPrefab != null)
                targetGroups = Mathf.Max(targetGroups, configuredGroups);

            long targetItemCountLong = (long)targetGroups * crossAxisCount;
            if (remainder != 0)
                targetItemCountLong = Math.Max(targetItemCountLong, (long)_scrollItems.Count + crossAxisCount - remainder);
            if (targetItemCountLong > int.MaxValue)
                throw new InvalidOperationException("The requested InfiniteScrollRect pool exceeds Unity collection limits.");

            int targetItemCount = (int)targetItemCountLong;
            if (_scrollItems.Count < targetItemCount && m_ItemPrefab == null)
                throw new InvalidOperationException($"InfiniteScrollRect needs at least {targetItemCount} physical views to cover the viewport, but only {_scrollItems.Count} were supplied. Add views or assign an Item Prefab.");

            while (_scrollItems.Count < targetItemCount)
            {
                RectTransform item = Instantiate(m_ItemPrefab, content);
                item.gameObject.SetActive(true);
                _scrollItems.Add(new ScrollItem(item));
            }

            m_PoolSize = _scrollItems.Count;
        }

        private void ConfigureTransforms()
        {
            ApplyScrollRectSettings();
            PrepareContentRect();

            for (int i = 0; i < _scrollItems.Count; i++)
            {
                RectTransform item = _scrollItems[i].View;
                item.SetParent(content, false);
                item.gameObject.SetActive(true);
                item.SetSiblingIndex(i);

                if (_usesLayoutGroup)
                    continue;

                item.anchorMin = new Vector2(0.5f, 0.5f);
                item.anchorMax = new Vector2(0.5f, 0.5f);
                item.pivot = new Vector2(0.5f, 0.5f);
            }

            if (_usesLayoutGroup)
                ValidateLayoutChildrenAreManaged();

            ForceLayoutRebuild();
        }

        private void CompleteInitialization()
        {
            _isRebuilding = true;
            try
            {
                EnsurePoolCapacity(3);
                ConfigureTransforms();
                UpdateLayoutMetrics();
                EnsurePoolCapacity(GetMinimumPoolGroupCount());
                ConfigureTransforms();
                UpdateLayoutMetrics();
                ValidatePool();

                _isInitialized = true;
                ValidateLogicalPosition(_logicalPosition);
                RebindAll(FloorToLong(_logicalPosition));
                ApplyPosition(false);
            }
            catch
            {
                _isInitialized = false;
                throw;
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        private void RebuildInitializedPool()
        {
            if (_isRebuilding)
                return;

            _isRebuilding = true;
            try
            {
                EnsureContentIsAssigned();
                PrepareContentRect();
                ResolveLayoutConfiguration();
                EnsurePoolCapacity(3);
                ConfigureTransforms();
                UpdateLayoutMetrics();
                EnsurePoolCapacity(GetMinimumPoolGroupCount());
                ConfigureTransforms();
                UpdateLayoutMetrics();
                ValidatePool();
                ValidateLogicalPosition(_logicalPosition);
                RebindAll(FloorToLong(_logicalPosition));
                ApplyPosition(false);
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        private void SetPositionFromInteraction(double position)
        {
            double limit = GetLogicalPositionLimit();
            _logicalPosition = Math.Max(-limit, Math.Min(limit, position));
            ApplyPosition(true);
        }

        private void PrepareContentRect()
        {
            RectTransform targetViewport = ResolveViewport();
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = targetViewport.rect.size;
            content.anchoredPosition = Vector2.zero;
        }

        private RectTransform ResolveViewport()
        {
            if (viewport != null)
                return viewport;

            RectTransform ownRectTransform = transform as RectTransform;
            if (ownRectTransform == null)
                throw new InvalidOperationException("InfiniteScrollRect requires a RectTransform viewport.");

            return ownRectTransform;
        }

        private void EnsureContentIsAssigned()
        {
            if (content == null)
                throw new InvalidOperationException("InfiniteScrollRect requires an assigned Content RectTransform.");
        }

        private void ResolveLayoutConfiguration()
        {
            _effectiveItemSpacing = Mathf.Max(1f, m_ItemSpacing);
            _effectiveCrossAxisSpacing = Mathf.Max(1f, m_CrossAxisSpacing);
            _effectiveCrossAxisCount = IsGridMode(m_LayoutMode) ? Mathf.Max(1, m_CrossAxisCount) : 1;
            SetManualAxisDirections();

            _layoutGroup = content.GetComponent<LayoutGroup>();
            _usesLayoutGroup = _layoutGroup != null && _layoutGroup.enabled;
            if (!_usesLayoutGroup)
                return;

            if (_layoutGroup is HorizontalLayoutGroup horizontalLayout)
            {
                m_LayoutMode = InfiniteScrollLayoutMode.Horizontal;
                _effectiveCrossAxisCount = 1;
                _mainAxisDirection = horizontalLayout.reverseArrangement ? Vector2.left : Vector2.right;
                _crossAxisDirection = Vector2.down;
                return;
            }

            if (_layoutGroup is VerticalLayoutGroup verticalLayout)
            {
                m_LayoutMode = InfiniteScrollLayoutMode.Vertical;
                _effectiveCrossAxisCount = 1;
                _mainAxisDirection = verticalLayout.reverseArrangement ? Vector2.up : Vector2.down;
                _crossAxisDirection = Vector2.right;
                return;
            }

            if (_layoutGroup is GridLayoutGroup gridLayout)
            {
                ResolveGridLayoutConfiguration(gridLayout);
                return;
            }

            throw new InvalidOperationException($"InfiniteScrollRect does not support the active layout group type {_layoutGroup.GetType().Name}.");
        }

        private void ResolveGridLayoutConfiguration(GridLayoutGroup gridLayout)
        {
            bool startsHorizontally = gridLayout.startAxis == GridLayoutGroup.Axis.Horizontal;
            if (startsHorizontally && gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                throw new InvalidOperationException("A vertically scrolling GridLayoutGroup must use Flexible or Fixed Column Count constraint.");
            }

            if (!startsHorizontally && gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                throw new InvalidOperationException("A horizontally scrolling GridLayoutGroup must use Flexible or Fixed Row Count constraint.");
            }

            m_LayoutMode = startsHorizontally ? InfiniteScrollLayoutMode.VerticalGrid : InfiniteScrollLayoutMode.HorizontalGrid;
            _effectiveCrossAxisCount = ResolveGridCrossAxisCount(gridLayout, startsHorizontally);
            m_CrossAxisCount = _effectiveCrossAxisCount;
            _effectiveItemSpacing = startsHorizontally ? gridLayout.cellSize.y + gridLayout.spacing.y : gridLayout.cellSize.x + gridLayout.spacing.x;
            _effectiveCrossAxisSpacing = startsHorizontally ? gridLayout.cellSize.x + gridLayout.spacing.x : gridLayout.cellSize.y + gridLayout.spacing.y;

            if (_effectiveItemSpacing <= 0f || _effectiveCrossAxisSpacing <= 0f)
                throw new InvalidOperationException("GridLayoutGroup cell size plus spacing must be greater than zero on both axes.");

            bool startsOnRight = gridLayout.startCorner == GridLayoutGroup.Corner.UpperRight || gridLayout.startCorner == GridLayoutGroup.Corner.LowerRight;
            bool startsOnBottom = gridLayout.startCorner == GridLayoutGroup.Corner.LowerLeft || gridLayout.startCorner == GridLayoutGroup.Corner.LowerRight;
            Vector2 horizontalDirection = startsOnRight ? Vector2.left : Vector2.right;
            Vector2 verticalDirection = startsOnBottom ? Vector2.up : Vector2.down;
            _mainAxisDirection = startsHorizontally ? verticalDirection : horizontalDirection;
            _crossAxisDirection = startsHorizontally ? horizontalDirection : verticalDirection;
            m_ItemSpacing = _effectiveItemSpacing;
            m_CrossAxisSpacing = _effectiveCrossAxisSpacing;
        }

        private int ResolveGridCrossAxisCount(GridLayoutGroup gridLayout, bool startsHorizontally)
        {
            if (startsHorizontally && gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                return Mathf.Max(1, gridLayout.constraintCount);
            if (!startsHorizontally && gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
                return Mathf.Max(1, gridLayout.constraintCount);

            if (startsHorizontally)
            {
                float pitch = gridLayout.cellSize.x + gridLayout.spacing.x;
                if (pitch <= 0f)
                    throw new InvalidOperationException("GridLayoutGroup horizontal cell size plus spacing must be greater than zero.");

                float width = content.rect.width - gridLayout.padding.horizontal;
                return Mathf.Max(1, Mathf.FloorToInt((width + gridLayout.spacing.x + 0.001f) / pitch));
            }

            float verticalPitch = gridLayout.cellSize.y + gridLayout.spacing.y;
            if (verticalPitch <= 0f)
                throw new InvalidOperationException("GridLayoutGroup vertical cell size plus spacing must be greater than zero.");

            float height = content.rect.height - gridLayout.padding.vertical;
            return Mathf.Max(1, Mathf.FloorToInt((height + gridLayout.spacing.y + 0.001f) / verticalPitch));
        }

        private void SetManualAxisDirections()
        {
            if (IsHorizontalMode(m_LayoutMode))
            {
                _mainAxisDirection = Vector2.right;
                _crossAxisDirection = Vector2.down;
            }
            else
            {
                _mainAxisDirection = Vector2.down;
                _crossAxisDirection = Vector2.right;
            }
        }

        private void ValidateLayoutChildrenAreManaged()
        {
            for (int i = 0; i < content.childCount; i++)
            {
                RectTransform child = content.GetChild(i) as RectTransform;
                if (child == null || child == m_ItemPrefab || !child.gameObject.activeSelf)
                    continue;

                LayoutElement layoutElement = child.GetComponent<LayoutElement>();
                if (layoutElement != null && layoutElement.ignoreLayout)
                    continue;

                bool isManaged = false;
                for (int itemIndex = 0; itemIndex < _scrollItems.Count; itemIndex++)
                {
                    if (_scrollItems[itemIndex].View == child)
                    {
                        isManaged = true;
                        break;
                    }
                }

                if (!isManaged)
                    throw new InvalidOperationException($"The active {_layoutGroup.GetType().Name} contains unmanaged child '{child.name}'. Pass every layout child to Initialize or mark decorative children Ignore Layout.");
            }
        }

        private void ValidatePool()
        {
            if (_scrollItems.Count < _effectiveCrossAxisCount * 3)
                throw new InvalidOperationException("InfiniteScrollRect requires at least three complete physical item groups.");
            if (_scrollItems.Count % _effectiveCrossAxisCount != 0)
                throw new InvalidOperationException("InfiniteScrollRect contains an incomplete physical grid group.");

            int requiredItemCount = GetMinimumPoolGroupCount() * _effectiveCrossAxisCount;
            if (_scrollItems.Count < requiredItemCount)
                throw new InvalidOperationException($"InfiniteScrollRect requires {requiredItemCount} physical views to cover the viewport, but only {_scrollItems.Count} are available.");
        }

        private int GetMinimumPoolGroupCount()
        {
            float viewportLength = IsHorizontalMode(m_LayoutMode) ? ResolveViewport().rect.width : ResolveViewport().rect.height;
            int visibleGroupCount = Mathf.CeilToInt(viewportLength / _effectiveItemSpacing);
            return Mathf.Max(3, visibleGroupCount + m_ExtraBufferGroups);
        }

        private void UpdateLayoutMetrics()
        {
            if (!_usesLayoutGroup)
            {
                _effectiveItemSpacing = Mathf.Max(1f, m_ItemSpacing);
                _effectiveCrossAxisSpacing = Mathf.Max(1f, m_CrossAxisSpacing);
                return;
            }

            ForceLayoutRebuild();
            if (PoolGroupCount < 2)
                return;

            Vector2 firstCenter = GetLocalCenter(_scrollItems[0].View);
            Vector2 secondCenter = GetLocalCenter(_scrollItems[_effectiveCrossAxisCount].View);
            float measuredSpacing = Vector2.Dot(secondCenter - firstCenter, _mainAxisDirection);
            if (measuredSpacing <= 0.01f)
                throw new InvalidOperationException("The active LayoutGroup did not produce a positive item stride along the scrolling axis.");

            _effectiveItemSpacing = measuredSpacing;
            m_ItemSpacing = measuredSpacing;
            float tolerance = Mathf.Max(0.5f, measuredSpacing * 0.01f);

            for (int groupIndex = 2; groupIndex < PoolGroupCount; groupIndex++)
            {
                int previousIndex = (groupIndex - 1) * _effectiveCrossAxisCount;
                int currentIndex = groupIndex * _effectiveCrossAxisCount;
                Vector2 previousCenter = GetLocalCenter(_scrollItems[previousIndex].View);
                Vector2 currentCenter = GetLocalCenter(_scrollItems[currentIndex].View);
                float stride = Vector2.Dot(currentCenter - previousCenter, _mainAxisDirection);
                if (Mathf.Abs(stride - measuredSpacing) > tolerance)
                    throw new InvalidOperationException("InfiniteScrollRect requires uniformly sized items along the scrolling axis. Use fixed item sizes and spacing in the attached LayoutGroup.");
            }
        }

        private float GetBaseItemMainAxisPosition()
        {
            if (!_usesLayoutGroup)
                return 0f;

            int baseGroupSlot = PoolGroupCount / 2;
            int baseItemIndex = baseGroupSlot * _effectiveCrossAxisCount;
            return Vector2.Dot(GetLocalCenter(_scrollItems[baseItemIndex].View), _mainAxisDirection);
        }

        private Vector2 GetLocalCenter(RectTransform item)
        {
            Vector3 worldCenter = item.TransformPoint(item.rect.center);
            return content.InverseTransformPoint(worldCenter);
        }

        private void ForceLayoutRebuild()
        {
            if (_usesLayoutGroup)
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void ApplyScrollRectSettings()
        {
            bool usesHorizontalAxis = IsHorizontalMode(m_LayoutMode);
            horizontal = usesHorizontalAxis;
            vertical = !usesHorizontalAxis;
            movementType = MovementType.Unrestricted;
            inertia = false;
            horizontalScrollbar = null;
            verticalScrollbar = null;
        }

        private void ValidateLogicalPosition(double position)
        {
            double limit = GetLogicalPositionLimit();
            if (position < -limit || position > limit)
                throw new ArgumentOutOfRangeException(nameof(position), $"Logical position must remain between {-limit:R} and {limit:R} so indices stay exact and overflow-safe.");
        }

        private double GetLogicalPositionLimit()
        {
            int crossAxisCount = Mathf.Max(1, _effectiveCrossAxisCount);
            int poolGroupCount = Mathf.Max(3, PoolGroupCount);
            return Math.Floor((MaxExactInteger - crossAxisCount) / crossAxisCount) - poolGroupCount - 2d;
        }

        private float GetMainAxisCoordinate(Vector2 position)
        {
            return Vector2.Dot(position, _mainAxisDirection);
        }

        private static void SetAxisCoordinate(ref Vector2 position, Vector2 direction, float coordinate)
        {
            if (Mathf.Abs(direction.x) > 0.5f)
                position.x = direction.x * coordinate;
            else
                position.y = direction.y * coordinate;
        }

        private static bool IsHorizontalMode(InfiniteScrollLayoutMode layoutMode)
        {
            return layoutMode == InfiniteScrollLayoutMode.Horizontal || layoutMode == InfiniteScrollLayoutMode.HorizontalGrid;
        }

        private static bool IsGridMode(InfiniteScrollLayoutMode layoutMode)
        {
            return layoutMode == InfiniteScrollLayoutMode.HorizontalGrid || layoutMode == InfiniteScrollLayoutMode.VerticalGrid;
        }

        private static long FloorToLong(double value)
        {
            return checked((long)Math.Floor(value));
        }

        private int PoolGroupCount => _effectiveCrossAxisCount > 0 ? _scrollItems.Count / _effectiveCrossAxisCount : 0;

        private sealed class ScrollItem
        {
            public ScrollItem(RectTransform view)
            {
                View = view;
            }

            public RectTransform View { get; }
            public long LogicalGroupIndex { get; set; }
            public long LogicalIndex { get; set; }
            public int CrossAxisIndex { get; set; }
        }
    }
}