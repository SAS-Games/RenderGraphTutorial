using UnityEngine;

namespace SAS.UI.InfiniteScroll
{
    public interface IInfiniteScrollItemBinder
    {
        void Bind(RectTransform itemView, long logicalIndex);
    }
}
