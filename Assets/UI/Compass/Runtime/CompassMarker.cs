using System;
using UnityEngine;

namespace SAS.UI.Compass
{
    [DisallowMultipleComponent]
    public sealed class CompassMarker : MonoBehaviour, ICompassMarker
    {
        [field: Header("Registration")]
        [field: SerializeField] public CompassMarkerRegistry Registry { get; private set; }
        [field: SerializeField, Tooltip("Stable unique key used by the marker registry. Generated automatically when empty.")]
        public string MarkerId { get; private set; }

        [field: Header("Marker")]
        [field: SerializeField] public Transform PositionSource { get; private set; }
        [field: SerializeField] public CompassMarkerType Type { get; private set; } = CompassMarkerType.PointOfInterest;
        [field: SerializeField] public string MarkerLabel { get; private set; }
        [field: SerializeField] public int Priority { get; private set; }
        [field: SerializeField, Min(0f)] public float MaxVisibleDistance { get; private set; }
        [field: SerializeField] public bool IsAvailable { get; private set; } = true;

        private bool isRegistered;

        public string Id => MarkerId;
        public Vector3 Position => PositionSource != null ? PositionSource.position : transform.position;
        public string Label => string.IsNullOrWhiteSpace(MarkerLabel) ? gameObject.name : MarkerLabel;
        public bool IsRegistered => isRegistered;

        public void SetRegistry(CompassMarkerRegistry targetRegistry)
        {
            if (Registry == targetRegistry)
            {
                if (isActiveAndEnabled)
                    TryRegister();
                return;
            }

            Unregister();
            Registry = targetRegistry;

            if (isActiveAndEnabled)
                TryRegister();
        }

        public void SetAvailable(bool available)
        {
            IsAvailable = available;
        }

        public void SetLabel(string markerLabel)
        {
            MarkerLabel = markerLabel;
        }

        public void SetPositionSource(Transform source)
        {
            PositionSource = source;
        }

        private void OnEnable()
        {
            EnsureId();

            if (Registry == null)
                Registry = FindFirstObjectByType<CompassMarkerRegistry>(FindObjectsInactive.Include);

            if (!TryRegister())
                Debug.LogWarning($"Compass marker '{name}' could not register. Assign a registry and ensure marker ID '{MarkerId}' is unique.", this);
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnValidate()
        {
            EnsureId();
            MaxVisibleDistance = Mathf.Max(0f, MaxVisibleDistance);
        }

        private bool TryRegister()
        {
            if (isRegistered)
                return true;
            if (Registry == null)
                return false;

            isRegistered = Registry.Register(this);
            return isRegistered;
        }

        private void Unregister()
        {
            if (!isRegistered)
                return;

            if (Registry != null)
                Registry.Unregister(this);

            isRegistered = false;
        }

        private void EnsureId()
        {
            if (string.IsNullOrWhiteSpace(MarkerId))
                MarkerId = Guid.NewGuid().ToString("N");
        }
    }
}
