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

    public static void RightMouseFilter( bool instant, PointerEventData e, Action func )
    {
        if( instant && e.button == PointerEventData.InputButton.Right )
            func();
        else
            InputPriority.Instance.Request( () => e.button == PointerEventData.InputButton.Right, "SearchPageButton", 1, func );
    }
}