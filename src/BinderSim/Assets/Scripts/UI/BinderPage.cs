using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BinderPage : EventReceiverInstance
{
    [SerializeField] GameObject binderPage;
    [SerializeField] GameObject bindersList;

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is PageChangeRequestEvent pageChangeRequest )
        {
            switch( pageChangeRequest.page )
            {
                case PageType.MainMenu:
                    binderPage.SetActive( false );
                    break;
                case PageType.BinderPage:
                    binderPage.SetActive( true );
                    break;
            }
        }
        else if( e is PageChangeRequestEvent pageChangeRequest )
        {
        }
    }
}
