using UnityEngine;

namespace SAS.UI.Compass
{
    public interface ICompassObserverProvider
    {
        Vector3 Position { get; }
    }
}
