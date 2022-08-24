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
    [SerializeField] GameObject importDialogPanel = null;
    [SerializeField] Button editButton = null;
    [SerializeField] Button deleteButton = null;
    [SerializeField] Color selectedEntryColour = new();

    private List<CardDataRuntime> inventory = new();

    private List<BinderDataRuntime> binderData = new();
    private int? currentSelectedBinderIdx;
    private int currentBinderSavingIndex;

    private List<CardDataRuntime> savedImportedData;
    private string savedImportedName;

    private System.Random rng = new();

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
        
        mainMenuPage.SetActive( true );
        importDialogPanel.SetActive( false );

        // Debug skip menu
        //NewBinder();
        //binderData.Back().binderUI.GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( null );
        //EditBinder();
    }

    public void EditBinder()
    {
        EventSystem.Instance.TriggerEvent( new OpenCardPageEvent()
        { 
            binder = binderData[currentSelectedBinderIdx.Value].data
        } );
    }

    private GameObject GetSelectedBinder()
    {
        return currentSelectedBinderIdx.HasValue ? bindersList.transform.GetChild( currentSelectedBinderIdx.Value + 1 ).gameObject : null;
    }

    public void DeleteBinder()
    {
        if( currentSelectedBinderIdx == null )
            return;

        GetSelectedBinder().Destroy();
        binderData.RemoveAt( currentSelectedBinderIdx.Value );

        for( int i = currentSelectedBinderIdx.Value; i < binderData.Count; ++i )
            binderData[i].index--;

        currentSelectedBinderIdx = null;
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

        binder.index = binderData.Count - 1;

        binder.binderUI.GetComponent<EventDispatcher>().OnPointerUpEvent += ( PointerEventData ) =>
        {
            bool unselect = currentSelectedBinderIdx == binder.index;
            if( currentSelectedBinderIdx != null || unselect )
                GetSelectedBinder().GetComponent<Image>().color = Color.clear;
            if( !unselect )
                binder.binderUI.GetComponent<Image>().color = selectedEntryColour;
            currentSelectedBinderIdx = unselect ? null : binder.index as int?;
            editButton.interactable = !unselect;
            deleteButton.interactable = !unselect;
        };

        binder.binderUI.GetComponent<EventDispatcher>().OnDoubleClickEvent += ( PointerEventData e ) =>
        {
            if( currentSelectedBinderIdx != binder.index )
                binder.binderUI.GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( e );
            EditBinder();
        };
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

            Save();
        }
    }

    public void LoadFromDragonShieldTxtFile()
    {
        var extensions = new[] { new ExtensionFilter("Text Files", "txt" ) };
        StandaloneFileBrowser.OpenFilePanelAsync( "Open File", "", extensions, false, ( paths ) =>
        {
            foreach( var path in paths )
                StartCoroutine( OnFileUpload( new System.Uri( path ) ) );
        } );
    }

    private IEnumerator OnFileUpload( Uri uri )
    {
        if( Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor )
        {
            var data = System.IO.File.ReadAllText( uri.AbsolutePath );
            yield return FileLoadedRoutine( uri, data );
        }
        else
        {
            var loader = new UnityWebRequest( uri );
            yield return loader.SendWebRequest();
            yield return FileLoadedRoutine( uri, loader.downloadHandler.text );
        }
    }

    private IEnumerator FileLoadedRoutine( Uri uri, string fileData )
    {
        savedImportedName = Path.GetFileNameWithoutExtension( uri.AbsolutePath );
        savedImportedData = new();
        var lines = fileData.Split( new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries );
        int idx = 0;
        float totalValue = 0.0f;

        foreach( var line in lines )
        {
            if( line.Length == 0 )
                continue;

            var data = line.Split( ',' );

            if( data.Length < 8 )
                continue;

            var value = data[data.Length - 3].Trim();
            var condition = data[data.Length - 4].Trim();
            var setCode = data[data.Length - 5].Trim();
            var cardName = string.Join( ",", data.Skip( 1 ).Take( data.Length - 7 ) ).Trim();
            idx++;

            yield return APICallHandler.Instance.SendCardSearchRequest( cardName, true, ( json ) =>
            {
                Root data = JsonConvert.DeserializeObject<Root>( json );
                Debug.Assert( data.data.Count == 1 );
                var card = data.data[0];

                if( float.TryParse( card.card_prices[0].tcgplayer_price, out var value ) )
                    totalValue += value;

                savedImportedData.Add( new CardDataRuntime()
                {
                    name = card.name,
                    cardId = card.id,
                    imageId = card.card_images[0].id,
                    cardAPIData = card.DeepCopy(),
                } );
            } );
        }

        importDialogPanel.SetActive( true );

        var texts = importDialogPanel.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = savedImportedName;
        texts[1].text = string.Format("{0} cards", idx);
        texts[2].text = string.Format("Total Value: ${0}", totalValue);

        var dropDown = importDialogPanel.GetComponentInChildren<TMPro.TMP_Dropdown>();
        List<string> options = new();

        foreach( var( val, str ) in Utility.GetEnumValues<ImportData.Options>().Zip( ImportData.optionStrings ) )
        {
            if( val == ImportData.Options.AddToExistingBinder )
                options.AddRange( binderData.Select( ( x ) => string.Format( str, x.data.name ) ) );
            else
                options.Add( str );
        }
        
        dropDown.ClearOptions();
        dropDown.AddOptions( options );
        dropDown.value = 0;
    }

    public void ExecuteImport()
    {
        var dropDown = importDialogPanel.GetComponentInChildren<TMPro.TMP_Dropdown>();
        switch( ( ImportData.Options )( Mathf.Min( dropDown.value, ( int )ImportData.Options.OptionsCount - 1 ) ) )
        {
            case ImportData.Options.CreatePopulatedBinder:
                {
                    var newBinder = NewBinderInternal( savedImportedName );
                    newBinder.data.AddCards( savedImportedData );
                    break;
                }
            case ImportData.Options.AddToInventory:
                {
                    break;
                }
            case ImportData.Options.ReplaceInventory:
                {
                    break;
                }
            case ImportData.Options.AddToExistingBinder:
                {
                    int binderIndex = dropDown.value - ( int )ImportData.Options.AddToExistingBinder;
                    binderData[binderIndex].data.AddCards( savedImportedData );
                    break;
                }
        }

        savedImportedName = null;
        savedImportedData = null;
    }

    public void OpenInventory()
    {
        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent()
        {
            page = PageType.SearchPageFull,
            behaviour = SearchPageBehaviour.Inventory,
        } );
    }

    private void OnApplicationPause( bool paused )
    {
        if( paused )
            Save();
    }

    private void OnApplicationFocus( bool hasFocus )
    {
        if( !hasFocus )
            Save();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void Save()
    {
        foreach( var( idx, binder ) in Utility.Enumerate( binderData ) )
        {
            currentBinderSavingIndex = idx;
            SaveGameSystem.SaveGame( binder.data.name );
        }
    }

    void ISavableComponent.Serialise( BinaryWriter writer )
    {
        var binder = binderData[currentBinderSavingIndex];

        writer.Write( binder.data.id );
        writer.Write( binder.data.name );
        writer.Write( binder.data.dateCreated.ToString() );
        writer.Write( binder.data.pageCount );
        writer.Write( binder.data.pageWidth );
        writer.Write( binder.data.pageHeight );
        writer.Write( binder.data.imagePath ?? String.Empty );

        foreach( var cardList in binder.data.cardList )
        {
            foreach( var card in cardList )
            {
                writer.Write( card != null ? card.cardId : 0 );
            }
        }
    }

    void ISavableComponent.Deserialise( int saveVersion, BinaryReader reader )
    {
        var id = reader.ReadInt64();
        var name = reader.ReadString();
        var dateCreated = DateTime.Parse( reader.ReadString() );
        var pageCount = reader.ReadInt32();
        var pageWidth = reader.ReadInt32();
        var pageHeight = reader.ReadInt32();
        var imagePath = reader.ReadString();

        var newBinderUI = Instantiate( binderEntryPrefab );
        newBinderUI.transform.SetParent( bindersList.transform );

        var newBinder = new BinderData( id, name, pageCount, pageWidth, pageHeight, imagePath );

        binderData.Add( new BinderDataRuntime()
        {
            data = newBinder,
            binderUI = newBinderUI,
        } );

        UpdateBinderUIEntry( binderData.Back() );

        for( int page = 0; page < pageCount; ++page )
        {
            for( int card = 0; card < pageWidth * pageHeight; ++card )
            {
                var cardId = reader.ReadInt32();
                var pageIdx = page;
                var cardIdx = card;

                if( cardId == 0 )
                    continue;

                StartCoroutine( APICallHandler.Instance.SendCardSearchRequest( cardId, true, ( json ) =>
                {
                    Root data = JsonConvert.DeserializeObject<Root>( json );
                    Debug.Assert( data.data.Count == 1 );
                    var cardData = data.data[0];

                    newBinder.cardList[pageIdx][cardIdx] = new CardDataRuntime()
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
