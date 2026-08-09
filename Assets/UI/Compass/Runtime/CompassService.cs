using System;
using System.Collections.Generic;

namespace SAS.UI.Compass
{
    public sealed class CompassService : ICompassService, IDisposable
    {
        private readonly List<ICompassMarker> markers = new List<ICompassMarker>();
        private readonly Dictionary<string, ICompassMarker> markersById = new Dictionary<string, ICompassMarker>();

        public event Action<ICompassMarker> MarkerRegistered;
        public event Action<ICompassMarker> MarkerUnregistered;

        public IReadOnlyList<ICompassMarker> Markers => markers;

        public bool Register(ICompassMarker marker)
        {
            if (marker == null || string.IsNullOrWhiteSpace(marker.Id) || markersById.ContainsKey(marker.Id))
                return false;

            markers.Add(marker);
            markersById.Add(marker.Id, marker);
            MarkerRegistered?.Invoke(marker);
            return true;
        }

        public bool Unregister(ICompassMarker marker)
        {
            if (marker == null || !markersById.TryGetValue(marker.Id, out ICompassMarker registeredMarker))
                return false;

            if (!ReferenceEquals(marker, registeredMarker))
                return false;

            markersById.Remove(marker.Id);
            markers.Remove(marker);
            MarkerUnregistered?.Invoke(marker);
            return true;
        }

        public void Dispose()
        {
            for (int i = markers.Count - 1; i >= 0; i--)
                MarkerUnregistered?.Invoke(markers[i]);

            markers.Clear();
            markersById.Clear();
            MarkerRegistered = null;
            MarkerUnregistered = null;
        }
    }
}
