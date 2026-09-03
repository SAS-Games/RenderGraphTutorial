using UnityEngine;
using UnityEngine.Serialization;

namespace SAS.UI.Compass
{
    [CreateAssetMenu(fileName = "CompassDirectionSet", menuName = "SAS/UI/Compass Direction Set")]
    public sealed class CompassDirectionSet : ScriptableObject
    {
        [FormerlySerializedAs("directions")] [SerializeField] private CompassDirectionDefinition[] m_Directions =
        {
            new("N", 0f),
            new("NE", 45f),
            new("E", 90f),
            new("SE", 135f),
            new("S", 180f),
            new("SW", 225f),
            new("W", 270f),
            new("NW", 315f)
        };

        public int Count => m_Directions != null ? m_Directions.Length : 0;
        public float AnglePerDirection => Count > 0 ? 360f / Count : 0f;

        public CompassDirectionDefinition GetDirection(int index)
        {
            if (index < 0 || index >= Count)
                throw new System.ArgumentOutOfRangeException(nameof(index));

            return m_Directions[index];
        }
    }
}