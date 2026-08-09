using System;
using System.Collections.Generic;
using SAS.UI.InfiniteScroll;
using UnityEngine;

namespace SAS.UI.Compass
{
    public sealed class CompassPresenter : IInfiniteScrollItemBinder, IDisposable
    {
        private readonly ICompassHeadingProvider headingProvider;
        private readonly ICompassObserverProvider observerProvider;
        private readonly ICompassService compassService;
        private readonly ICompassView view;
        private readonly CompassVisualSettings settings;
        private readonly CompassDirectionSet directionSet;
        private readonly List<MarkerBinding> markerBindings = new List<MarkerBinding>();

        private float previousWrappedHeading;
        private double continuousHeading;
        private bool hasHeading;
        private bool isInitialized;

        public CompassPresenter(ICompassHeadingProvider headingProvider, ICompassObserverProvider observerProvider, ICompassService compassService, ICompassView view, CompassVisualSettings settings)
        {
            this.headingProvider = headingProvider ?? throw new ArgumentNullException(nameof(headingProvider));
            this.observerProvider = observerProvider ?? throw new ArgumentNullException(nameof(observerProvider));
            this.compassService = compassService ?? throw new ArgumentNullException(nameof(compassService));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            directionSet = settings.DirectionSet != null ? settings.DirectionSet : throw new ArgumentException("Compass visual settings require a direction set.", nameof(settings));
        }

        public double ContinuousHeading => continuousHeading;

        public void Initialize()
        {
            if (isInitialized)
                return;

            if (directionSet.Count < 1)
                throw new InvalidOperationException("The Compass direction set must contain at least one direction.");

            view.Initialize(settings, this);
            compassService.MarkerRegistered += OnMarkerRegistered;
            compassService.MarkerUnregistered += OnMarkerUnregistered;

            IReadOnlyList<ICompassMarker> markers = compassService.Markers;
            for (int i = 0; i < markers.Count; i++)
                AddMarker(markers[i]);

            isInitialized = true;
        }

        public void Tick()
        {
            if (!isInitialized)
                return;

            float wrappedHeading = CompassMath.NormalizeHeading(headingProvider.Heading);
            if (!hasHeading)
            {
                previousWrappedHeading = wrappedHeading;
                continuousHeading = wrappedHeading;
                hasHeading = true;
            }
            else
            {
                continuousHeading += Mathf.DeltaAngle(previousWrappedHeading, wrappedHeading);
                previousWrappedHeading = wrappedHeading;
            }

            view.SetDirectionPosition(continuousHeading / directionSet.AnglePerDirection);
            UpdateMarkers(wrappedHeading);
        }

        public void Bind(RectTransform itemView, long logicalIndex)
        {
            int directionIndex = InfiniteScrollMath.Mod(logicalIndex, directionSet.Count);
            CompassDirectionDefinition direction = directionSet.GetDirection(directionIndex);
            view.BindDirection(itemView, direction.Label);
        }

        public void Dispose()
        {
            if (!isInitialized)
                return;

            compassService.MarkerRegistered -= OnMarkerRegistered;
            compassService.MarkerUnregistered -= OnMarkerUnregistered;

            for (int i = markerBindings.Count - 1; i >= 0; i--)
                view.RemoveMarker(markerBindings[i].ViewHandle);

            markerBindings.Clear();
            view.Clear();
            hasHeading = false;
            isInitialized = false;
        }

        private void UpdateMarkers(float wrappedHeading)
        {
            Vector3 observerPosition = observerProvider.Position;
            for (int i = markerBindings.Count - 1; i >= 0; i--)
            {
                MarkerBinding binding = markerBindings[i];
                ICompassMarker marker = binding.Marker;
                if (marker == null || marker is UnityEngine.Object unityMarker && unityMarker == null)
                {
                    RemoveMarkerAt(i);
                    continue;
                }

                if (!marker.IsAvailable)
                {
                    CompassMarkerPresentation hiddenPresentation = new CompassMarkerPresentation(0f, 0f, false);
                    view.SetMarker(binding.ViewHandle, in hiddenPresentation);
                    continue;
                }

                Vector3 markerPosition = marker.Position;
                float distance = Vector3.Distance(observerPosition, markerPosition);
                float markerHeading = CompassMath.WorldPositionToHeading(observerPosition, markerPosition);
                float relativeAngle = CompassMath.GetRelativeAngle(markerHeading, wrappedHeading);
                float maxVisibleDistance = marker.MaxVisibleDistance;
                bool isWithinDistance = maxVisibleDistance <= 0f || distance <= maxVisibleDistance;
                bool isVisible = isWithinDistance && CompassMath.IsVisible(relativeAngle, settings.VisibleHalfAngle);
                CompassMarkerPresentation presentation = new CompassMarkerPresentation(
                    CompassMath.GetLocalX(relativeAngle, settings.UnitsPerDegree), distance, isVisible);
                view.SetMarker(binding.ViewHandle, in presentation);
            }
        }

        private void OnMarkerRegistered(ICompassMarker marker)
        {
            AddMarker(marker);
        }

        private void OnMarkerUnregistered(ICompassMarker marker)
        {
            for (int i = markerBindings.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(markerBindings[i].Marker, marker))
                    continue;

                RemoveMarkerAt(i);
                return;
            }
        }

        private void AddMarker(ICompassMarker marker)
        {
            if (marker == null)
                return;

            for (int i = 0; i < markerBindings.Count; i++)
            {
                if (ReferenceEquals(markerBindings[i].Marker, marker))
                    return;
            }

            markerBindings.Add(new MarkerBinding(marker, view.AddMarker(marker)));
            markerBindings.Sort(MarkerBindingComparer.Instance);
        }

        private void RemoveMarkerAt(int index)
        {
            view.RemoveMarker(markerBindings[index].ViewHandle);
            markerBindings.RemoveAt(index);
        }

        private readonly struct MarkerBinding
        {
            public MarkerBinding(ICompassMarker marker, int viewHandle)
            {
                Marker = marker;
                ViewHandle = viewHandle;
            }

            public ICompassMarker Marker { get; }
            public int ViewHandle { get; }
        }

        private sealed class MarkerBindingComparer : IComparer<MarkerBinding>
        {
            public static readonly MarkerBindingComparer Instance = new MarkerBindingComparer();

            public int Compare(MarkerBinding left, MarkerBinding right)
            {
                return left.Marker.Priority.CompareTo(right.Marker.Priority);
            }
        }
    }
}
