using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BinderPage : EventReceiverInstance, ISavableComponent
{
    [SerializeField] GameObject mainMenuPage = null;
    [SerializeField] GameObject bindersList = null;
    [SerializeField] GameObject binderEntryPrefab = null;
    [SerializeField] Button editButton = null;
    [SerializeField] Button deleteButton = null;
    [SerializeField] Color selectedEntryColour = new Color();

    private List<BinderData> binderData = new List<BinderData>();
    private int? currentlySelectedBinderIdx;

    protected override void Start()
    {
        base.Start();

        SaveGameSystem.AddSaveableComponent( this );

        //foreach(var save in SaveGameSystem.GetSaveGames() )
        //{
        //    if( !SaveGameSystem.LoadGame( save ) )
        //        Debug.LogError( "Failed to load save file: " + save );
        //    else
        //        EventSystem.Instance.TriggerEvent( new BinderLoadedEvent() );
        //}

        mainMenuPage.SetActive( true );
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
        EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent()
        { 
            page = PageType.BinderPage,
            binder = binderData[currentlySelectedBinderIdx.Value]
        } );
    }

    private GameObject GetSelectedBinder()
    {
        return currentlySelectedBinderIdx.HasValue ? bindersList.transform.GetChild( currentlySelectedBinderIdx.Value + 1 ).gameObject : null;
    }

    public void DeleteBinder()
    {
        if( currentlySelectedBinderIdx == null )
            return;

        GetSelectedBinder().Destroy();
        currentlySelectedBinderIdx = null;
        editButton.interactable = false;
        deleteButton.interactable = false;
    }

    public void NewBinder()
    {
        var newBinder = Instantiate( binderEntryPrefab );
        newBinder.transform.SetParent(bindersList.transform);

        binderData.Add( new BinderData()
        {
            name = "New binder",
            dateCreated = DateTime.Now,
            pageCount = Constants.DefaultStartingNumPages,
            pageWidth = Constants.DefaultStartingPageWidth,
            pageHeight = Constants.DefaultStartingPageHeight,
            cardList = new List<CardData>(),
        } );

        binderData.Back().cardList.Resize( Constants.DefaultStartingNumCards );
        UpdateBinderUIEntry( newBinder, binderData.Back() );

        int thisIdx = binderData.Count - 1;
        newBinder.GetComponent<EventDispatcher>().OnPointerUpEvent += ( PointerEventData e ) =>
        {
            bool unselect = currentlySelectedBinderIdx == thisIdx;
            if( currentlySelectedBinderIdx != null || unselect )
                GetSelectedBinder().GetComponent<Image>().color = Color.clear;
            if( !unselect )
                newBinder.GetComponent<Image>().color = selectedEntryColour;
            currentlySelectedBinderIdx = unselect ? null : thisIdx;
            editButton.interactable = !unselect;
            deleteButton.interactable = !unselect;
        };
    }

    private void UpdateBinderUIEntry(GameObject entry, BinderData data)
    {
        var texts = entry.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = data.name;
        texts[1].text = data.pageCount.ToString();
        texts[2].text = string.Format( "{0}x{1}", data.pageWidth, data.pageHeight );
        texts[3].text = data.dateCreated.ToShortDateString();

        var images = entry.GetComponentsInChildren<Image>();
        // Preview icon
        images[1].gameObject.Destroy();
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
