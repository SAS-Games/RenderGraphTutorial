namespace SAS.UI.Compass
{
    public readonly struct CompassMarkerPresentation
    {
        public CompassMarkerPresentation(float localX, float distance, bool isVisible)
        {
            LocalX = localX;
            Distance = distance;
            IsVisible = isVisible;
        }

        public float LocalX { get; }
        public float Distance { get; }
        public bool IsVisible { get; }
    }
}
