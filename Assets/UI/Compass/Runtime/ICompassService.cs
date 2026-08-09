using System;
using System.Collections.Generic;

namespace SAS.UI.Compass
{
    public interface ICompassService
    {
        event Action<ICompassMarker> MarkerRegistered;
        event Action<ICompassMarker> MarkerUnregistered;

        IReadOnlyList<ICompassMarker> Markers { get; }

        bool Register(ICompassMarker marker);
        bool Unregister(ICompassMarker marker);
    }
}
