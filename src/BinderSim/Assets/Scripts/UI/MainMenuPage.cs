using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuPage : EventReceiverInstance
{
    [SerializeField] GameObject mainMenuPage;

    public void EditBinder()
    {
        EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent() { page = PageType.BinderPage } );
    }

    public void DeleteBinder()
    {
        EventSystem.Instance.AddSubscriber( this );
    }

    public void NewBinder()
    {
        EventSystem.Instance.AddSubscriber( this );
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is PageChangeRequestEvent pageChangeRequest )
        {
            switch( pageChangeRequest.page )
            {
                case PageType.MainMenu:
                    mainMenuPage.SetActive( true );
                    break;
                case PageType.BinderPage:
                    mainMenuPage.SetActive( false );
                    break;
            }
        }
    }
}
