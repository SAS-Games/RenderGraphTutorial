using SAS.UI.InfiniteScroll;
using UnityEngine;

namespace SAS.UI.Compass
{
    public interface ICompassView
    {
        void Initialize(CompassVisualSettings settings, IInfiniteScrollItemBinder directionBinder);
        void SetDirectionPosition(double logicalPosition);
        void BindDirection(RectTransform itemView, string label);
        int AddMarker(ICompassMarker marker);
        void RemoveMarker(int handle);
        void SetMarker(int handle, in CompassMarkerPresentation presentation);
        void Clear();
    }
}
