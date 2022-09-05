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
using System.Collections.ObjectModel;
using System.Text;

public class BinderPage : EventReceiverInstance, ISavableComponent
{
    public static BinderPage Instance { get; private set; }

    [SerializeField] GameObject mainMenuPage = null;
    [SerializeField] GameObject bindersList = null;
    [SerializeField] GameObject binderEntryPrefab = null;
    [SerializeField] GameObject importDialogPanel = null;
    [SerializeField] Button editButton = null;
    [SerializeField] Button deleteButton = null;
    [SerializeField] Button exportButton = null;
    [SerializeField] Color selectedEntryColour = new();

    private List<CardDataRuntime> inventory = new();
    public List<CardDataRuntime> Inventory { get => inventory; private set { } }

    private List<BinderDataRuntime> binderData = new();
    public ReadOnlyCollection<BinderDataRuntime> BinderData { get => binderData.AsReadOnly(); private set { } }

    private int? currentSelectedBinderIdx;
    private int currentBinderSavingIndex;

    private ImportData savedImportedData;

    private System.Random rng = new();

    const int maxCardsPerRequest = 40;

    protected override void Start()
    {
        base.Start();

        Instance = this;
        SaveGameSystem.AddSaveableComponent( this );

        foreach(var save in SaveGameSystem.GetSaveGames() )
        {
            if( save.EndsWith( "_backup" ) )
                continue;

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
            binder = binderData[currentSelectedBinderIdx.Value]
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

        // Delete the save file (but create a backup first)
        currentBinderSavingIndex = currentSelectedBinderIdx.Value;
        var binderName = binderData[currentBinderSavingIndex].data.name;
        SaveGameSystem.SaveGame( binderName + "_backup" );
        SaveGameSystem.DeleteGame( binderName );

        GetSelectedBinder().Destroy();
        binderData.RemoveAt( currentSelectedBinderIdx.Value );

        for( int i = currentSelectedBinderIdx.Value; i < binderData.Count; ++i )
            binderData[i].index--;

        // Fix card in inventory indices
        foreach( var card in inventory )
        {
            if( card.insideBinderIdx == null )
                continue;

            if( card.insideBinderIdx.Value == currentSelectedBinderIdx.Value )
                card.insideBinderIdx = null;
            else if( card.insideBinderIdx.Value > currentSelectedBinderIdx.Value )
                card.insideBinderIdx = card.insideBinderIdx.Value - 1;
        }

        var index = currentSelectedBinderIdx.Value;
        currentSelectedBinderIdx = null;
        exportButton.interactable = false;
        editButton.interactable = false;
        deleteButton.interactable = false;

        // Select next entry
        if( binderData.Count > 0 )
        {
            var binder = binderData[Mathf.Min( index, binderData.Count - 1 )];
            binder.binderUI.GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( null );
        }
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
        images[^1].color = Color.clear;

        binder.index = binderData.Count - 1;

        binder.binderUI.GetComponent<EventDispatcher>().OnPointerUpEvent += ( PointerEventData ) =>
        {
            bool unselect = currentSelectedBinderIdx == binder.index;
            if( currentSelectedBinderIdx != null || unselect )
                GetSelectedBinder().GetComponent<Image>().color = Color.clear;
            if( !unselect )
                binder.binderUI.GetComponent<Image>().color = selectedEntryColour;
            currentSelectedBinderIdx = unselect ? null : binder.index;
            editButton.interactable = !unselect;
            deleteButton.interactable = !unselect;
            exportButton.interactable = !unselect;
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

    public void ImportFromDragonShieldTxtFile()
    {
        LoadFromDragonShieldTxtFile( ShowImportDialog );
    }

    public void LoadFromDragonShieldTxtFile( Action<ImportData> onSearchCompleteCallback )
    {
        var extensions = new[] { new ExtensionFilter( "Text Files", "txt" ) };
        StandaloneFileBrowser.OpenFilePanelAsync( "Open File", "", extensions, false, ( paths ) =>
        {
            foreach( var path in paths )
                StartCoroutine( OnFileUpload( new Uri( path ), onSearchCompleteCallback ) );
        } );
    }

    private IEnumerator OnFileUpload( Uri uri, Action<ImportData> onSearchCompleteCallback )
    {
        if( Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor )
        {
            var data = File.ReadAllText( uri.AbsolutePath );
            FileLoadedRoutine( uri, data, onSearchCompleteCallback );
        }
        else
        {
            var loader = new UnityWebRequest( uri );
            yield return loader.SendWebRequest();
            FileLoadedRoutine( uri, loader.downloadHandler.text, onSearchCompleteCallback );
        }
    }

    private int importRequestsComplete = 0;
    private float totalImportedValue = 0.0f;

    private class ImportCardExtraData
    {
        public string setCode;
        public CardCondition condition;
        public int count;
    }

    private void FileLoadedRoutine( Uri uri, string fileData, Action<ImportData> onSearchCompleteCallback )
    {
        ImportData importData = new();
        importData.name = Path.GetFileNameWithoutExtension( uri.AbsolutePath );

        var lines = fileData.Split( new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries );
        StringBuilder builder = new();
        var numCards = 0;
        importRequestsComplete = 0;
        totalImportedValue = 0.0f;
        Dictionary<string, List<ImportCardExtraData>> importedCardData = new();

        foreach( var line in lines )
            if( line.Length > 0 && line.Split( ',' ).Length >= 8 )
                ++numCards;

        var requestsTotal = numCards / maxCardsPerRequest + ( numCards % maxCardsPerRequest > 0 ? 1 : 0 );

        foreach( var line in lines )
        {
            if( line.Length == 0 )
                continue;

            var data = line.Split( ',' );

            if( line.Split( ',' ).Length < 8 )
                continue;

            var cardName = string.Join( ",", data.Skip( 1 ).Take( data.Length - 7 ) ).Trim();
            var conditionStr = data[^5].Trim();
            CardCondition condition = CardCondition.NearMint;
            if( Enum.TryParse( typeof( CardCondition ), conditionStr, true, out var c ) )
                condition = ( CardCondition )c;

            var extraData = new ImportCardExtraData()
            {
                condition = condition,
                setCode = data[^6].Trim(),
                count = int.Parse(data[0].Trim())
            };

            if( importedCardData.TryGetValue( cardName.ToLower(), out var items ) )
            {
                items.Add( extraData );
            }
            else
            {
                importedCardData.Add( cardName.ToLower(), new List<ImportCardExtraData>{ extraData } );
            }

            builder.Append( builder.Length > 0 ? "|" : String.Empty );
            builder.Append( cardName );

            if( ++importData.count % maxCardsPerRequest == 0 || importData.count == numCards )
            {
                var request = builder.ToString();
                var debugURI = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?name={0}", request );
                StartCoroutine( APICallHandler.Instance.SendCardSearchRequest( request, true, ( json ) =>
                {
                    Root jsonData = JsonConvert.DeserializeObject<Root>( json );

                    foreach( var card in jsonData.data )
                    {
                        if( !importedCardData.ContainsKey( card.name.ToLower() ) )
                        {
                            Debug.LogError( String.Format( "Failed to find return json card in list: {0}\nRequest: {1}\n", card.name, debugURI ) );
                            continue;
                        }

                        var importedCards = importedCardData[card.name.ToLower()];

                        foreach( var importedCard in importedCards )
                        {
                            // Find index for from set id
                            var cardIndex = card.card_sets.FindIndex( ( x ) =>
                            {
                                if( x.set_code.StartsWith( importedCard.setCode ) )
                                    return true;

                                var withoutSuffix = x.set_code.StartsWith( importedCard.setCode[..^1] );
                                var suffix = importedCard.setCode[^1];
                                var setSuffix = x.set_code[( importedCard.setCode.Length - 1 )..];
                                return withoutSuffix && setSuffix.Contains( suffix );
                            } );

                            if( cardIndex == -1 )
                            {
                                Debug.LogError( String.Format( "Failed to find card index from set ID: {0} (card: {1})\nSets: {2}", 
                                    card.name, importedCard.setCode, string.Join( ", ", card.card_sets.Select( ( x ) => x.set_code ) ) ) );
                                cardIndex = 0;
                            }

                            if( float.TryParse( card.card_sets[cardIndex].set_price, out var value ) )
                                totalImportedValue += value;

                            importData.cards.Add( new CardDataRuntime()
                            {
                                name = card.name,
                                cardId = card.id,
                                cardIndex = cardIndex,
                                cardAPIData = card.DeepCopy(),
                                condition = importedCard.condition,
                                count = importedCard.count,
                            } );
                        }
                    }

                    if( ++importRequestsComplete == requestsTotal )
                    {
                        importData.totalValue = totalImportedValue;
                        onSearchCompleteCallback( importData );
                    }
                } ) );

                builder = new();
            }
        }
    }

    private void ShowImportDialog( ImportData importData )
    {
        savedImportedData = importData;
        importDialogPanel.SetActive( true );

        var texts = importDialogPanel.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = importData.name;
        texts[1].text = string.Format( "{0} cards", importData.count );
        texts[2].text = string.Format( "Total Value: ${0:0.00}", importData.totalValue );

        var dropDown = importDialogPanel.GetComponentInChildren<TMPro.TMP_Dropdown>();
        List<string> options = new();

        foreach( var( val, str ) in Utility.GetEnumValues<ImportData.Options>().Zip( ImportData.optionStrings ) )
        {
            if( val == ImportData.Options.AddToExistingBinderX )
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
                    var newBinder = NewBinderInternal( savedImportedData.name );
                    newBinder.AddCards( savedImportedData.cards );
                    inventory.AddRange( savedImportedData.cards );
                    break;
                }
            case ImportData.Options.AddToInventory:
                {
                    inventory.AddRange( savedImportedData.cards );
                    break;
                }
            case ImportData.Options.ReplaceInventory:
                {
                    inventory = savedImportedData.cards;
                    break;
                }
            case ImportData.Options.AddToExistingBinderX:
                {
                    int binderIndex = dropDown.value - ( int )ImportData.Options.AddToExistingBinderX;
                    binderData[binderIndex].AddCards( savedImportedData.cards );
                    inventory.AddRange( savedImportedData.cards );
                    break;
                }
        }

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
        writer.Write( ( UInt16 ) binder.data.pageCount );
        writer.Write( ( byte )binder.data.pageWidth );
        writer.Write( ( byte )binder.data.pageHeight );
        writer.Write( binder.data.imagePath ?? String.Empty );

        int gaps = 0;
        foreach( var cardList in binder.data.cardList )
        {
            foreach( var card in cardList )
            {
                if( card == null )
                {
                    ++gaps;
                }
                else
                {
                    if( gaps > 0 )
                    {
                        writer.Write( -gaps );
                        gaps = 0;
                    }

                    writer.Write( card.cardId );
                    writer.Write( ( byte )( ( ( byte )card.condition << 5 ) | Mathf.Min( 64, card.count ) ) );
                    writer.Write( ( byte )( ( card.cardIndex << 4 ) | card.imageIndex ) );
                }
            }
        }

        if( gaps > 0 )
            writer.Write( -gaps );
    }

    void ISavableComponent.Deserialise( int saveVersion, BinaryReader reader )
    {
        var id = reader.ReadInt64();
        var name = reader.ReadString();
        var dateCreated = DateTime.Parse( reader.ReadString() );
        var pageCount = reader.ReadUInt16();
        var pageWidth = reader.ReadByte();
        var pageHeight = reader.ReadByte();
        var imagePath = reader.ReadString();

        var newBinderUI = Instantiate( binderEntryPrefab );
        newBinderUI.transform.SetParent( bindersList.transform );

        var newBinder = new BinderData( id, name, pageCount, pageWidth, pageHeight, imagePath );
        var bindexIndex = BinderData.Count;

        binderData.Add( new BinderDataRuntime()
        {
            data = newBinder,
            binderUI = newBinderUI,
        } );

        UpdateBinderUIEntry( binderData.Back() );

        var request = "https://db.ygoprodeck.com/api/v7/cardinfo.php?id=";
        StringBuilder uri = new();
        int numCards = 0;
        Dictionary<int, List<CardDataRuntime>> idToCardData = new();
        int gaps = 0;

        for( int page = 0; page < pageCount; ++page )
        {
            for( int card = 0; card < pageWidth * pageHeight; ++card )
            {
                if( gaps > 0 )
                {
                    gaps--;
                }
                else
                {
                    var cardId = reader.ReadInt32();

                    if( cardId < 0 )
                    {
                        gaps = Mathf.Abs( cardId ) - 1;
                        continue;
                    }

                    if( cardId != 0 )
                    {
                        uri.Append( uri.Length > 0 ? ", " : String.Empty );
                        uri.Append( cardId );

                        var countAndCondition = reader.ReadByte();
                        var cardAndImageIndices = reader.ReadByte();

                        var deserialisedCard = new CardDataRuntime()
                        {
                            cardId = page * pageWidth * pageHeight + card,
                            count = countAndCondition & 63,
                            condition = ( CardCondition )( countAndCondition >> 5 ),
                            cardIndex = cardAndImageIndices >> 4,
                            imageIndex = cardAndImageIndices & 15
                        };

                        var pageIdx = deserialisedCard.cardId / ( pageWidth * pageHeight );
                        var cardIdx = Utility.Mod( deserialisedCard.cardId, pageWidth * pageHeight );
                        if( pageIdx >= newBinder.cardList.Count || cardIdx >= newBinder.cardList[pageIdx].Count )
                        {
                            Debug.Assert( false );
                        }

                        if( idToCardData.TryGetValue( cardId, out var items ) )
                        {
                            items.Add( deserialisedCard );
                        }
                        else
                        {
                            idToCardData.Add( cardId, new List<CardDataRuntime> { deserialisedCard } );
                        }

                        ++numCards;
                    }
                }

                if( uri.Length > 0 && // At least one card
                    ( numCards % maxCardsPerRequest == 0 || // Either at X cards, push a request
                    ( page == pageCount - 1 && card == ( pageWidth * pageHeight ) - 1 ) ) ) // Or on the last card of the loop
                {
                    StartCoroutine( APICallHandler.Instance.SendGetRequest( request + uri.ToString(), true, ( json ) =>
                    {
                        Root data = JsonConvert.DeserializeObject<Root>( json );

                        foreach( var card in data.data )
                        {
                            if( !idToCardData.TryGetValue( card.id, out List<CardDataRuntime> newCards ) )
                            {
                                Debug.LogError( "Failed to find return json card in list: " + card.name );
                                continue;
                            }

                            foreach( var newCard in newCards )
                            {
                                var pageIdx = newCard.cardId / ( pageWidth * pageHeight );
                                var cardIdx = Utility.Mod( newCard.cardId, pageWidth * pageHeight );
                                newCard.cardId = card.id;
                                newCard.name = card.name;
                                newCard.cardAPIData = card.DeepCopy();
                                //Debug.Assert( pageIdx < newBinder.cardList.Count && cardIdx < newBinder.cardList[pageIdx].Count );
                                if( pageIdx >= newBinder.cardList.Count || cardIdx >= newBinder.cardList[pageIdx].Count )
                                {
                                    Debug.Assert( false );
                                }
                                newBinder.cardList[pageIdx][cardIdx] = newCard;
                                newCard.insideBinderIdx = bindexIndex;
                                inventory.Add( newCard );
                            }
                        }
                    } ) );

                    uri = new();
                }
            }
        }
    }

    private const int maxChars = 91;
    private const int asciiCharStart = 33;

    public void ExportAsString( TMPro.TMP_InputField text )
    {
        if( currentSelectedBinderIdx == null )
            return;

        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter( memoryStream );
        currentBinderSavingIndex = currentSelectedBinderIdx.Value;
        writer.Write( ( byte )SaveGameSystem.currentVersion );
        ( this as ISavableComponent ).Serialise( writer );
        text.text = GetStringFromBytes( memoryStream.ToArray() );
    }

    public void CopyTextToClipBoard( TMPro.TMP_InputField text)
    {
        GUIUtility.systemCopyBuffer = text.text;

        if( Application.platform == RuntimePlatform.WebGLPlayer )
            WebGLCopyAndPasteAPI.passCopyToBrowser( GUIUtility.systemCopyBuffer );
    }

    private static string GetStringFromBytes( Byte[] bts )
    {
        StringBuilder str = new();

        foreach( var bt in bts )
        {
            if( bt <= maxChars )
            {
                str.Append( ( char )( bt + asciiCharStart ) );
            }
            else if( bt <= maxChars * 2 )
            {
                str.Append( 'A' );
                str.Append( ( char )( bt - maxChars + asciiCharStart ) );
            }
            else
            {
                str.Append( 'B' );
                str.Append( ( char )( bt - maxChars * 2 + asciiCharStart ) );
            }

            str[^1] = FixChar( str[^1], false );
        }

        return str.ToString();
    }

    public void ImportFromString( TMPro.TMP_InputField text )
    {
        if( text.text.Length == 0 )
            return;

        try
        {
            var bytes = GetBytesFromString( text.text );
            using var memoryStream = new MemoryStream( bytes, writable: false );
            using var reader = new BinaryReader( memoryStream );
            var version = reader.ReadByte();
            ( this as ISavableComponent ).Deserialise( version, reader );
        }
        catch( Exception e )
        {
            Debug.LogError( "ImportFromString failed: \n" + e.Message );
        }
    }

    private static Byte[] GetBytesFromString( string str )
    {
        List<Byte> bts = new();

        for( int idx = 0; idx < str.Length; ++idx )
        {
            var c = str[idx];

            if( c == 'A' )
            {
                c = str[++idx];
                bts.Add( ( byte )( FixChar( c, true ) + maxChars - asciiCharStart ) );
            }
            else if( c == 'B' )
            {
                c = str[++idx];
                bts.Add( ( byte )( FixChar( c, true ) + maxChars * 2 - asciiCharStart ) );
            }
            else
            {
                bts.Add( ( byte )( FixChar( c, true ) - asciiCharStart ) );
            }
        }

        return bts.ToArray();
    }

    private static char FixChar( char c, bool import )
    {
        if( import )
        {
            if( c == '|' )
                return 'A';
            else if( c == '}' )
                return 'B';
        }
        else
        {
            if( c == 'A' )
                return '|';
            else if( c == 'B' )
                return '}';
        }
        return c;
    }
}
