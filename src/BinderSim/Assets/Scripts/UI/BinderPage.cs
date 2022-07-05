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
    [SerializeField] Color selectedEntryColour = new();

    private List<BinderDataRuntime> binderData = new();
    private int? currentlySelectedBinderIdx;

    private System.Random rng = new();

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

        // Debug skip menu
        NewBinder();
        binderData.Back().binderUI.GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( null );
        EditBinder();
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        writer.Write( binderData.Count );

        foreach( var binder in binderData )
        {
            writer.Write( binder.data.name );
            writer.Write( binder.data.dateCreated.ToString() );
            writer.Write( binder.data.pageCount );
            writer.Write( binder.data.pageWidth );
            writer.Write( binder.data.pageHeight );
            writer.Write( binder.data.imagePath );
            writer.Write( binder.data.cardList.Count );

            foreach( var (page, cardList) in binder.data.cardList )
            {
                writer.Write( page );
                foreach( var card in cardList )
                {
                    writer.Write( card.cardId );
                }
            }
        }
    }

    void ISavableComponent.Deserialise( int saveVersion, BinaryReader reader )
    {
        int count = reader.ReadInt32();

        for( int i = 0; i < count; ++i )
        {
            var newBinder = new BinderData()
            {
                name = reader.ReadString(),
                dateCreated = DateTime.Parse( reader.ReadString() ),
                pageCount = reader.ReadInt32(),
                pageWidth = reader.ReadInt32(),
                pageHeight = reader.ReadInt32(),
                imagePath = reader.ReadString(),
            };

            var cardCount = reader.ReadInt32();

            for( int j = 0; j < count; ++j )
            {


            }
        }
    }

    public void EditBinder()
    {
        EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent()
        { 
            page = PageType.CardPage,
            binder = binderData[currentlySelectedBinderIdx.Value].data
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

        binderData.Add( new BinderDataRuntime()
        {
            data = new BinderData()
            {
                id = rng.NextLong(),
                name = "New binder",
                dateCreated = DateTime.Now,
                pageCount = Constants.DefaultStartingNumPages,
                pageWidth = Constants.DefaultStartingPageWidth,
                pageHeight = Constants.DefaultStartingPageHeight,
                cardList = new Dictionary<int, List<CardDataRuntime>>(),
            },
            binderUI = newBinder,
        } );

        UpdateBinderUIEntry( binderData.Back() );

        int thisIdx = binderData.Count - 1;
        newBinder.GetComponent<EventDispatcher>().OnPointerUpEvent += ( PointerEventData ) =>
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

    private void UpdateBinderUIEntry( BinderDataRuntime binder )
    {
        var texts = binder.binderUI.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = binder.data.name;
        texts[1].text = binder.data.pageCount.ToString();
        texts[2].text = string.Format( "{0}x{1}", binder.data.pageWidth, binder.data.pageHeight );
        texts[3].text = binder.data.dateCreated.ToShortDateString();

        var images = binder.binderUI.GetComponentsInChildren<Image>();
        // Preview icon
        if( images.Length > 1 && images[1].gameObject != null )
            images[1].gameObject.Destroy();
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is PageChangeRequestEvent pageChangeRequest )
        {
            switch( pageChangeRequest.page )
            {
                case PageType.BinderPage:
                    mainMenuPage.SetActive( true );
                    break;
                case PageType.CardPage:
                    mainMenuPage.SetActive( false );
                    break;
            }
        }
        else if( e is BinderLoadedEvent )
        {

        }
        else if( e is BinderDataUpdateEvent binderUpdateEvent )
        {
            foreach( var binder in binderData )
            {
                if( binder.data.id == binderUpdateEvent.binder.id )
                {
                    UpdateBinderUIEntry( binder );
                    break;
                }
            }
        }
    }

    public void LoadFromDragonShieldTxtFile()
    {

    }
}
