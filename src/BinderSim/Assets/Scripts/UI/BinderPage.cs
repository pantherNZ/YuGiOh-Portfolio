using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;
using SFB;
using System.Linq;
using Newtonsoft.Json;

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
        //NewBinder();
        //binderData.Back().binderUI.GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( null );
        //EditBinder();
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        writer.Write( binderData.Count );

        foreach( var binder in binderData )
        {
            writer.Write( binder.data.id );
            writer.Write( binder.data.name );
            writer.Write( binder.data.dateCreated.ToString() );
            writer.Write( binder.data.pageCount );
            writer.Write( binder.data.pageWidth );
            writer.Write( binder.data.pageHeight );
            writer.Write( binder.data.imagePath );

            foreach( var cardList in binder.data.cardList )
            {
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
            var id = reader.ReadInt64();
            var name = reader.ReadString();
            var dateCreated = DateTime.Parse( reader.ReadString() );
            var pageCount = reader.ReadInt32();
            var pageWidth = reader.ReadInt32();
            var pageHeight = reader.ReadInt32();
            var imagePath = reader.ReadString();

            var newBinder = new BinderData( id, name, pageCount, pageWidth, pageHeight, imagePath );

            for( int page = 0; page < pageCount; ++page)
            {
                for( int card = 0; card < newBinder.pageWidth * newBinder.pageHeight; ++card )
                {
                    var cardId = reader.ReadInt32();

                    StartCoroutine( APICallHandler.Instance.SendCardSearchRequest( cardId, true, ( json ) =>
                    {
                        Root data = JsonConvert.DeserializeObject<Root>( json );
                        Debug.Assert( data.data.Count == 1 );
                        var cardData = data.data[0];

                        newBinder.cardList[page][card] = new CardDataRuntime()
                        {
                            name = cardData.name,
                            cardId = cardData.id,
                            imageId = cardData.card_images[0].id,
                            cardAPIData = cardData.DeepCopy(),
                        };
                    } ) );
                }
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
        NewBinderInternal();
    }

    private BinderDataRuntime NewBinderInternal( string startName = "New Binder")
    {
        var newBinder = Instantiate( binderEntryPrefab );
        newBinder.transform.SetParent(bindersList.transform);

        binderData.Add( new BinderDataRuntime()
        {
            data = new BinderData( rng.NextLong(), startName ),
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

        newBinder.GetComponent<EventDispatcher>().OnDoubleClickEvent += ( PointerEventData e ) =>
        {
            if( currentlySelectedBinderIdx != thisIdx )
                newBinder.GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( e );
            EditBinder();
        };

        return binderData.Back();
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

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void UploadFile(string gameObjectName, string methodName, string filter, bool multiple);
#endif

    public void LoadFromDragonShieldTxtFile()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        UploadFile(gameObject.name, "OnFileUpload", ".txt", false);
#else
        var extensions = new[] { new ExtensionFilter("Text Files", "txt" ) };
        StandaloneFileBrowser.OpenFilePanelAsync( "Open File", "", extensions, false, ( paths ) =>
        {
            foreach( var path in paths )
                OnFileUpload( new System.Uri( path ).AbsoluteUri );
        } );
       
#endif
    }

    public void OnFileUpload(string url) {
        StartCoroutine( FileLoadedRoutine( url ) );
    }

    private IEnumerator FileLoadedRoutine( string url )
    {
        var loader = new UnityWebRequest( url );
        yield return loader;

        var newBinder = NewBinderInternal( Path.GetFileNameWithoutExtension( url ) );

        foreach( var( idx, line ) in Utility.Enumerate( loader.downloadHandler.text.Split( '\n' ) ) )
        {
            if( line.Length == 0 )
                continue;

            var data = line.Split( ',' );

            if( data.Length < 8 )
                continue;

            var value = data[^3].Trim();
            var condition = data[^4].Trim();
            var setCode = data[^5].Trim();
            var cardName = string.Join( ',', data.Skip( 1 ).Take( data.Length - 7 ) );
            var cardIndex = Utility.Mod( idx, newBinder.data.pageWidth * newBinder.data.pageHeight );
            var pageIndex = idx / ( newBinder.data.pageWidth * newBinder.data.pageHeight );

            StartCoroutine( APICallHandler.Instance.SendCardSearchRequest( cardName, false, ( json ) =>
            {
                Root data = JsonConvert.DeserializeObject<Root>( json );
                Debug.Assert( data.data.Count == 1 );
                var card = data.data[0];

                newBinder.data.cardList[pageIndex][cardIndex] = new CardDataRuntime()
                {
                    name = card.name,
                    cardId = card.id,
                    imageId = card.card_images[0].id,
                    cardAPIData = card.DeepCopy(),
                };
            } ) );
        }
    }
}
