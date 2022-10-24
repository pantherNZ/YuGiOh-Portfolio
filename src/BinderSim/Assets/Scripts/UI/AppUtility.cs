using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public static class AppUtility
{
    public static void LeftMouseFilter( bool instant, PointerEventData e, Action func )
    {
        if( instant && e.button == PointerEventData.InputButton.Left )
            func();
        else
            InputPriority.Instance.Request( () => e.button == PointerEventData.InputButton.Left, "SearchPageButton", 1, func );
    }

    public static void SortInventory( List<CardDataRuntime> cards )
    {
        cards.Sort( ( a, b ) =>
        {
            if( a.insideBinderIdx != b.insideBinderIdx )
                return ( a.insideBinderIdx == null ? -1 : a.insideBinderIdx.Value ).CompareTo( b.insideBinderIdx == null ? -1 : b.insideBinderIdx.Value );
            return a.name.CompareTo( b.name );
        } );
    }
}