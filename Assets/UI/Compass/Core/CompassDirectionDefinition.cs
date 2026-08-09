using System;
using UnityEngine;

namespace SAS.UI.Compass
{
    [Serializable]
    public struct CompassDirectionDefinition
    {
        [SerializeField] private string label;
        [SerializeField, Range(0f, 360f)] private float heading;

        public CompassDirectionDefinition(string label, float heading)
        {
            this.label = label;
            this.heading = heading;
        }

        public string Label => label;
        public float Heading => heading;
    }
}
