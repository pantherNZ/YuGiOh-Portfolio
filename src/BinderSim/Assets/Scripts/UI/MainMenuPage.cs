using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuPage : EventReceiverInstance, ISavableComponent
{
    [SerializeField] GameObject mainMenuPage = null;
    [SerializeField] GameObject bindersList = null;
    [SerializeField] GameObject binderEntryPrefab = null;
    [SerializeField] Button editButton = null;
    [SerializeField] Button deleteButton = null;
    [SerializeField] Color selectedEntryColour = new Color();

    private List<BinderData> binderData = new List<BinderData>();
    private Image currentlySelectedBinder = null;

    protected override void Start()
    {
        base.Start();

        SaveGameSystem.AddSaveableComponent( this );

        foreach(var save in SaveGameSystem.GetSaveGames() )
        {
            if( !SaveGameSystem.LoadGame( save ) )
                Debug.LogError( "Failed to load save file: " + save );
            else
                EventSystem.Instance.TriggerEvent( new BinderLoadedEvent() );
        }
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        throw new NotImplementedException();
    }

    void ISavableComponent.Deserialise( int saveVersion, BinaryReader reader )
    {
        throw new NotImplementedException();
    }

    public void EditBinder()
    {
        EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent() { page = PageType.BinderPage } );
    }

    public void DeleteBinder()
    {
        if( currentlySelectedBinder == null )
            return;

        currentlySelectedBinder.gameObject.Destroy();
    }

    public void NewBinder()
    {
        var newBinder = Instantiate( binderEntryPrefab );
        newBinder.transform.SetParent(bindersList.transform);

        var texts = newBinder.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = "New binder";
        texts[1].text = Constants.DefaultStartingNumPages.ToString();
        texts[2].text = string.Format( "{0}x{1}", Constants.DefaultStartingPageWidth, Constants.DefaultStartingPageHeight );
        texts[2].text = DateTime.Now.ToShortDateString();

        var images = newBinder.GetComponentsInChildren<Image>();
        // Preview icon
        images[1].gameObject.Destroy();

        newBinder.GetComponent<EventDispatcher>().OnPointerUpEvent += ( PointerEventData e ) =>
        {
            bool unselect = currentlySelectedBinder == images[0];
            if( currentlySelectedBinder != null || unselect )
                currentlySelectedBinder.color = new Color( 0.0f, 0.0f, 0.0f, 0.0f );
            if( !unselect )
                images[0].color = selectedEntryColour;
            currentlySelectedBinder = unselect ? null : images[0];
            editButton.interactable = !unselect;
            deleteButton.interactable = !unselect;
        };
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
        else if( e is BinderLoadedEvent )
        {

        }
        else if( e is BinderDataUpdateEvent )
        {

        }
    }
}
