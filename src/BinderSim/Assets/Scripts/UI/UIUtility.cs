using System;
using UnityEngine.EventSystems;

public static class UIUtility
{
    public static void LeftMouseFilter( bool instant, PointerEventData e, Action func )
    {
        if( instant && e.button == PointerEventData.InputButton.Left )
            func();
        else
            InputPriority.Instance.Request( () => e.button == PointerEventData.InputButton.Left, "SearchPageButton", 1, func );
    }
}