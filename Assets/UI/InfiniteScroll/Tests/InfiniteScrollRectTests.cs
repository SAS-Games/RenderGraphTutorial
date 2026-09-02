using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace SAS.UI.InfiniteScroll.Tests
{
    public sealed class InfiniteScrollRectTests
    {
        private GameObject _root;
        private RectTransform _viewport;
        private RectTransform _content;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void ManualHorizontalScrollRecyclesAcrossPositiveAndNegativeIndices()
        {
            InfiniteScrollRect scroll = CreateScroller(new Vector2(300f, 100f));
            List<RectTransform> items = CreateItems(5, new Vector2(100f, 100f));
            RecordingBinder binder = new RecordingBinder();

            scroll.Configure(_viewport, _content, 100f, 5, false);
            scroll.Initialize(binder, items);
            scroll.SetPosition(1.25d);

            Assert.That(_content.anchoredPosition.x, Is.EqualTo(-25f).Within(0.01f));
            Assert.That(_content.anchoredPosition.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(binder.MinimumIndex, Is.EqualTo(-1));
            Assert.That(binder.MaximumIndex, Is.EqualTo(3));
        }

        [Test]
        public void ManualVerticalScrollUsesTheYAxis()
        {
            InfiniteScrollRect scroll = CreateScroller(new Vector2(100f, 300f));
            List<RectTransform> items = CreateItems(5, new Vector2(100f, 100f));

            scroll.Configure(_viewport, _content, InfiniteScrollLayoutMode.Vertical, 100f, 100f, 1, 5, false);
            scroll.Initialize(new RecordingBinder(), items);
            scroll.SetPosition(1.25d);

            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.vertical, Is.True);
            Assert.That(_content.anchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(_content.anchoredPosition.y, Is.EqualTo(25f).Within(0.01f));
        }

        [Test]
        public void ManualVerticalGridRecyclesCompleteRows()
        {
            InfiniteScrollRect scroll = CreateScroller(new Vector2(300f, 300f));
            List<RectTransform> items = CreateItems(15, new Vector2(100f, 100f));
            RecordingBinder binder = new RecordingBinder();

            scroll.Configure(_viewport, _content, InfiniteScrollLayoutMode.VerticalGrid, 100f, 100f, 3, 15, false);
            scroll.Initialize(binder, items);
            scroll.SetPosition(1.5d);

            Assert.That(scroll.CrossAxisCount, Is.EqualTo(3));
            Assert.That(_content.anchoredPosition.y, Is.EqualTo(50f).Within(0.01f));
            Assert.That(binder.MinimumIndex, Is.EqualTo(-3));
            Assert.That(binder.MaximumIndex, Is.EqualTo(11));
            Assert.That(binder.Count, Is.EqualTo(15));
        }

        [Test]
        public void ConfigureAfterInitializeRebuildsTheExistingPool()
        {
            InfiniteScrollRect scroll = CreateScroller(new Vector2(300f, 300f));
            List<RectTransform> items = CreateItems(5, new Vector2(100f, 100f));
            RecordingBinder binder = new RecordingBinder();

            scroll.Configure(_viewport, _content, 100f, 5, false);
            scroll.Initialize(binder, items);
            scroll.Configure(_viewport, _content, InfiniteScrollLayoutMode.Vertical, 100f, 100f, 1, 5, false);
            scroll.SetPosition(0.5d);

            Assert.That(scroll.LayoutMode, Is.EqualTo(InfiniteScrollLayoutMode.Vertical));
            Assert.That(scroll.vertical, Is.True);
            Assert.That(_content.anchoredPosition.y, Is.EqualTo(50f).Within(0.01f));
            Assert.That(binder.Count, Is.EqualTo(5));
        }

        [Test]
        public void HorizontalLayoutGroupRemainsThePhysicalLayoutEngine()
        {
            InfiniteScrollRect scroll = CreateScroller(new Vector2(300f, 100f));
            HorizontalLayoutGroup layout = _content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            List<RectTransform> items = CreateItems(5, new Vector2(90f, 100f));
            RecordingBinder binder = new RecordingBinder();

            scroll.Configure(_viewport, _content, 100f, 5, false);
            scroll.Initialize(binder, items);
            RectTransform zero = binder.FindView(0);
            Assert.That(WorldCenter(zero).x, Is.EqualTo(WorldCenter(_viewport).x).Within(0.01f));

            scroll.SetPosition(0.25d);
            Assert.That(WorldCenter(zero).x, Is.EqualTo(WorldCenter(_viewport).x - 25f).Within(0.01f));
            Assert.That(layout.enabled, Is.True);
        }

        [Test]
        public void GridLayoutGroupIsDetectedAndRecycledByRow()
        {
            InfiniteScrollRect scroll = CreateScroller(new Vector2(180f, 180f));
            GridLayoutGroup layout = _content.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(50f, 50f);
            layout.spacing = new Vector2(10f, 10f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.childAlignment = TextAnchor.MiddleCenter;
            List<RectTransform> items = CreateItems(15, layout.cellSize);
            RecordingBinder binder = new RecordingBinder();

            scroll.Configure(_viewport, _content, 60f, 15, false);
            scroll.Initialize(binder, items);

            Assert.That(scroll.LayoutMode, Is.EqualTo(InfiniteScrollLayoutMode.VerticalGrid));
            Assert.That(scroll.CrossAxisCount, Is.EqualTo(3));
            Assert.That(scroll.vertical, Is.True);
            Assert.That(WorldCenter(binder.FindView(0)).y, Is.EqualTo(WorldCenter(_viewport).y).Within(0.01f));

            scroll.SetPosition(1.5d);
            Assert.That(WorldCenter(binder.FindView(3)).y, Is.EqualTo(WorldCenter(_viewport).y + 30f).Within(0.01f));
            Assert.That(layout.enabled, Is.True);
        }

        [Test]
        public void InitializationRejectsPoolsThatCannotCoverTheViewport()
        {
            InfiniteScrollRect scroll = CreateScroller(new Vector2(500f, 100f));
            List<RectTransform> items = CreateItems(3, new Vector2(100f, 100f));
            scroll.Configure(_viewport, _content, 100f, 3, false);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => scroll.Initialize(new RecordingBinder(), items));
            StringAssert.Contains("cover the viewport", exception.Message);
        }

        [Test]
        public void PositionRejectsValuesThatCannotRetainExactIndices()
        {
            InfiniteScrollRect scroll = CreateScroller(new Vector2(300f, 100f));
            List<RectTransform> items = CreateItems(5, new Vector2(100f, 100f));
            scroll.Configure(_viewport, _content, 100f, 5, false);
            scroll.Initialize(new RecordingBinder(), items);

            Assert.Throws<ArgumentOutOfRangeException>(() => scroll.SetPosition(double.MaxValue));
        }

        private InfiniteScrollRect CreateScroller(Vector2 viewportSize)
        {
            _root = new GameObject("Infinite Scroll Test", typeof(RectTransform), typeof(InfiniteScrollRect));
            RectTransform rootRect = (RectTransform)_root.transform;
            rootRect.sizeDelta = viewportSize;

            _viewport = CreateRect("Viewport", rootRect, viewportSize);
            _content = CreateRect("Content", _viewport, viewportSize);
            return _root.GetComponent<InfiniteScrollRect>();
        }

        private List<RectTransform> CreateItems(int count, Vector2 size)
        {
            List<RectTransform> items = new List<RectTransform>(count);
            for (int i = 0; i < count; i++)
                items.Add(CreateRect($"Item {i}", _content, size));
            return items;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)instance.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        private static Vector3 WorldCenter(RectTransform rect)
        {
            return rect.TransformPoint(rect.rect.center);
        }

        private sealed class RecordingBinder : IInfiniteScrollItemBinder
        {
            private readonly Dictionary<RectTransform, long> _indices = new Dictionary<RectTransform, long>();

            public int Count => _indices.Count;
            public long MinimumIndex => MinimumOrMaximum(true);
            public long MaximumIndex => MinimumOrMaximum(false);

            public void Bind(RectTransform itemView, long logicalIndex)
            {
                _indices[itemView] = logicalIndex;
            }

            public RectTransform FindView(long logicalIndex)
            {
                foreach (KeyValuePair<RectTransform, long> pair in _indices)
                {
                    if (pair.Value == logicalIndex)
                        return pair.Key;
                }

                Assert.Fail($"No physical view is bound to logical index {logicalIndex}.");
                return null;
            }

            private long MinimumOrMaximum(bool minimum)
            {
                bool hasValue = false;
                long result = 0;
                foreach (long value in _indices.Values)
                {
                    if (!hasValue || minimum && value < result || !minimum && value > result)
                    {
                        result = value;
                        hasValue = true;
                    }
                }

                return result;
            }
        }
    }
}
