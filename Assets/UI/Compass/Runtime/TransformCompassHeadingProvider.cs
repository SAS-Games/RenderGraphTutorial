using UnityEngine;

namespace SAS.UI.Compass
{
    [DisallowMultipleComponent]
    public sealed class TransformCompassHeadingProvider : MonoBehaviour, ICompassHeadingProvider, ICompassObserverProvider
    {
        [SerializeField] private Transform headingSource;
        [SerializeField] private Transform observer;

        public float Heading
        {
            get
            {
                Transform source = headingSource != null ? headingSource : transform;
                Vector3 forward = Vector3.ProjectOnPlane(source.forward, Vector3.up);
                return forward.sqrMagnitude > 0.0001f
                    ? CompassMath.DirectionToHeading(forward)
                    : CompassMath.NormalizeHeading(source.eulerAngles.y);
            }
        }

        public Vector3 Position => observer != null ? observer.position : transform.position;

        public void SetHeadingSource(Transform source)
        {
            headingSource = source;
        }

        public void SetObserver(Transform observerTransform)
        {
            observer = observerTransform;
        }
    }
}
