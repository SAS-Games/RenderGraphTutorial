using UnityEngine;

namespace SAS.UI.Compass
{
    public interface ICompassMarker
    {
        string Id { get; }
        Vector3 Position { get; }
        CompassMarkerType Type { get; }
        string Label { get; }
        int Priority { get; }
        float MaxVisibleDistance { get; }
        bool IsAvailable { get; }
    }
}
