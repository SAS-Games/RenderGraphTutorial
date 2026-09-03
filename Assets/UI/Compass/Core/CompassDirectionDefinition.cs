using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SAS.UI.Compass
{
    [Serializable]
    public struct CompassDirectionDefinition
    {
        [FormerlySerializedAs("label")] [SerializeField]
        private string m_Label;

        [FormerlySerializedAs("heading")] [SerializeField, Range(0f, 360f)]
        private float m_Heading;

        public CompassDirectionDefinition(string label, float heading)
        {
            this.m_Label = label;
            this.m_Heading = heading;
        }

        public string Label => m_Label;
        public float Heading => m_Heading;
    }
}