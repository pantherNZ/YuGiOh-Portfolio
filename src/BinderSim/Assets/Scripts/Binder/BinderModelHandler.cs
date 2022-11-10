using System.Collections.Generic;
using UnityEngine;

public class BinderModelHandler : EventReceiverInstance
{
    private Material[] savedMaterials;
    private BinderDataRuntime currentBinder;
    int currentPage;

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is OpenCardPageEvent openCardRequest )
        {
            currentBinder = openCardRequest.binder;
        }
        else if( e is BinderChangeCardPage cardChange )
        {
            currentPage = cardChange.newPage;
            var prev = cardChange.previousPage;
        }
        else if( e is BinderPopulateGrid populateGrid)
        {
            currentPage = populateGrid.currentPage;
        }

        if( e is PageChangeRequestEvent pageChangeRequest && pageChangeRequest.page == PageType.CardPage )
        {
            savedMaterials = new Material[currentBinder.data.pageCount];
        }
    }
}