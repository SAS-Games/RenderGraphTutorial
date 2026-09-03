using System;
using UnityEngine;

namespace SAS.UI.Compass
{
    [Serializable]
    public sealed class CompassMarkerVisualSettings
    {
        [SerializeField] private CompassMarkerType m_Type;
        [SerializeField] private Sprite m_Sprite;
        [SerializeField] private Color m_Color = Color.white;
        [SerializeField] private Vector2 m_Size = new Vector2(24f, 24f);

        public CompassMarkerType Type => m_Type;
        public Sprite Sprite => m_Sprite;
        public Color Color => m_Color;
        public Vector2 Size => m_Size;
    }
}
