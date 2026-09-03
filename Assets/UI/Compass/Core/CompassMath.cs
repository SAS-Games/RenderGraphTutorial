using UnityEngine;

namespace SAS.UI.Compass
{
    public static class CompassMath
    {
        private const float DirectionEpsilon = 0.0001f;

        public static float NormalizeHeading(float heading)
        {
            return Mathf.Repeat(heading, 360f);
        }

        public static float GetRelativeAngle(float itemHeading, float cameraHeading)
        {
            return Mathf.DeltaAngle(cameraHeading, itemHeading);
        }

        public static float GetLocalX(float relativeAngle, float unitsPerDegree)
        {
            return relativeAngle * unitsPerDegree;
        }

        public static bool IsVisible(float relativeAngle, float visibleHalfAngle)
        {
            return Mathf.Abs(relativeAngle) <= visibleHalfAngle;
        }

        public static float DirectionToHeading(Vector3 direction)
        {
            Vector2 horizontalDirection = new Vector2(direction.x, direction.z);
            if (horizontalDirection.sqrMagnitude <= DirectionEpsilon)
                return 0f;

            return NormalizeHeading(Mathf.Atan2(horizontalDirection.x, horizontalDirection.y) * Mathf.Rad2Deg);
        }

        public static float WorldPositionToHeading(Vector3 observerPosition, Vector3 targetPosition)
        {
            return DirectionToHeading(targetPosition - observerPosition);
        }
    }
}