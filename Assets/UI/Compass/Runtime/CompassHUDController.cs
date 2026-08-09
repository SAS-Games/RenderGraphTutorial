using UnityEngine;

namespace SAS.UI.Compass
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TransformCompassHeadingProvider))]
    [RequireComponent(typeof(CompassMarkerRegistry))]
    [RequireComponent(typeof(CompassView))]
    public sealed class CompassHUDController : MonoBehaviour
    {
        [SerializeField] private TransformCompassHeadingProvider headingProvider;
        [SerializeField] private CompassMarkerRegistry markerRegistry;
        [SerializeField] private CompassView compassView;
        [SerializeField] private CompassVisualSettings visualSettings;

        private CompassPresenter presenter;

        public ICompassService Service => markerRegistry != null ? markerRegistry.Service : null;

        public void Configure(Transform headingSource, Transform observer, CompassVisualSettings settings)
        {
            CacheComponents();
            visualSettings = settings;
            headingProvider.SetHeadingSource(headingSource);
            headingProvider.SetObserver(observer);
        }

        public void SetHeadingSource(Transform source)
        {
            if (headingProvider != null)
                headingProvider.SetHeadingSource(source);
        }

        public void SetObserver(Transform observer)
        {
            if (headingProvider != null)
                headingProvider.SetObserver(observer);
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            if (!CanInitialize())
                return;

            presenter = new CompassPresenter(headingProvider, headingProvider, markerRegistry.Service, compassView, visualSettings);
            presenter.Initialize();
        }

        private void LateUpdate()
        {
            presenter?.Tick();
        }

        private void OnDisable()
        {
            presenter?.Dispose();
            presenter = null;
        }

        private void CacheComponents()
        {
            if (headingProvider == null)
                headingProvider = GetComponent<TransformCompassHeadingProvider>();
            if (markerRegistry == null)
                markerRegistry = GetComponent<CompassMarkerRegistry>();
            if (compassView == null)
                compassView = GetComponent<CompassView>();
        }

        private bool CanInitialize()
        {
            if (headingProvider != null && markerRegistry != null && compassView != null &&
                visualSettings != null && visualSettings.DirectionSet != null && visualSettings.DirectionSet.Count > 0)
                return true;

            Debug.LogError("Compass HUD requires a heading provider, marker registry, view, visual settings, and a non-empty direction set.", this);
            enabled = false;
            return false;
        }
    }
}
