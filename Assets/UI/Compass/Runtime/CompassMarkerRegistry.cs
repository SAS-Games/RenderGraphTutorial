using UnityEngine;

namespace SAS.UI.Compass
{
    [DisallowMultipleComponent]
    public sealed class CompassMarkerRegistry : MonoBehaviour
    {
        private readonly CompassService service = new CompassService();

        public ICompassService Service => service;

        public bool Register(ICompassMarker marker)
        {
            return service.Register(marker);
        }

        public bool Unregister(ICompassMarker marker)
        {
            return service.Unregister(marker);
        }

        private void OnDestroy()
        {
            service.Dispose();
        }
    }
}
