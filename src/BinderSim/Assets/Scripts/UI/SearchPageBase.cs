using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

public abstract class SearchPageBase : EventReceiverInstance
{
    [SerializeField] protected GameObject searchListPage = null;
    [SerializeField] protected GameObject cardList = null;
    [SerializeField] protected TMPro.TMP_InputField searchInput = null;
    [SerializeField] protected Color selectedEntryColour = new Color();
    [SerializeField] protected bool autoSearch = true;
    [SerializeField] protected int maxSearchResults = 100;
    [SerializeField] protected int autoSearchDelayMS = 500;
    [SerializeField] TMPro.TMP_Dropdown optionsDropdown = null;
    [SerializeField] TMPro.TextMeshProUGUI cardCountText = null;
    [SerializeField] TMPro.TextMeshProUGUI totalValueText = null;
    [SerializeField] protected TMPro.TextMeshProUGUI titleText = null;
    [SerializeField] Button minimiseMaximiseButton = null;
    [SerializeField] Button clearCardButton = null;
    [SerializeField] DialogBox dialog = null;

    protected List<CardDataRuntime> cardData = new List<CardDataRuntime>();
    protected InventoryStorage tempImportInventory;
    protected Dictionary<CardDataRuntime, GameObject> searchUIEntries = new Dictionary<CardDataRuntime, GameObject>();
    protected int? currentCardSelectedIdx;
    protected int? currentBinderIdx;
    protected SearchPageOrigin behaviour = SearchPageOrigin.None;
    protected SearchPageFlags flags;
    protected string replacingCardName;
    private Coroutine searchCountdownHandle;
    private InventoryData.Options? savedBehaviour;

    protected override void Start()
    {
        base.Start();

        searchListPage.SetActive( false );

        searchInput.onValueChanged.AddListener( OnSearchTextchanged );

        optionsDropdown.onValueChanged.AddListener( (_) =>
        {
            var newOption = GetDropDownOption();
            if( newOption != InventoryData.Options.TempInventory && tempImportInventory != null )
            {
                tempImportInventory = null;
                PopulateOptions();
                SetDropDownOption( newOption );
            }

            UpdateButtons();
            SearchCards();

            if( behaviour == SearchPageOrigin.CardPageSearch )
                savedBehaviour = newOption;
        } );

        clearCardButton.onClick.AddListener( () =>
        {
            EventSystem.Instance.TriggerEvent( new CardSelectedEvent()
            {
                card = null
            } );
            Cancel();
        } );
    }

    private void OnSearchTextchanged( string text )
    {
        if( searchCountdownHandle != null )
            StopCoroutine( searchCountdownHandle );

        searchCountdownHandle = StartCoroutine( TextChangedTimer() );
    }

    private IEnumerator TextChangedTimer()
    {
        yield return new WaitForSeconds( autoSearchDelayMS / 1000.0f );
        SearchCards();
    }

    public void SearchCards()
    {
        // Remove current card entries (skip/leave header)
        for( int i = ( ContainsHeader() ? 1 : 0 ); i < cardList.transform.childCount; ++i )
            cardList.transform.GetChild( i ).gameObject.Destroy();
        currentCardSelectedIdx = null;
        searchUIEntries.Clear();
        cardData.Clear();

        var search = searchInput.text.Trim().ToLower();
        SearchRequest( search );
    }

    protected abstract bool ContainsHeader();

    private void SearchRequest( string search )
    {
        SearchRequestInternal( Uri.EscapeDataString( search ) );
    }

    private void SearchRequestInternal( string search )
    {
        var dropDownIdx = GetDropDownOptionIdx();
        var filter = GetDropDownOption();

        if( filter == InventoryData.Options.SearchOnline )
        {
            if( search.Length > 0 )
            {
                List<Datum> searchResults = new List<Datum>();

                foreach( var card in APICallHandler.Instance.cardsDatabase.data )
                {
                    if( FilterCard( search, card ) )
                    {
                        searchResults.Add( card );
                    }
                }

                SortAndAddResults( CreateCardsFromSearchResult( searchResults ) );
            }
            else
            {
                if( cardCountText != null )
                    cardCountText.gameObject.SetActive( false );
                AddCard( new CardDataRuntime() { name = "No results found" } );
            }
            return;
        }

        var count = 0;
        var totalValue = 0.0f;

        var inventory = filter == InventoryData.Options.TempInventory
            ? tempImportInventory
            : BinderPage.Instance.Inventory;
        inventory.Sort();

        var results = new List<CardDataRuntime>();

        foreach( var( idx, card ) in inventory.Enumerate() )
        {
            if( card.cardAPIData == null )
                continue;
            if( filter == InventoryData.Options.AllCardsInBinders && card.insideBinderIdx == null )
                continue;
            if( filter == InventoryData.Options.UnusedCards && card.insideBinderIdx != null )
                continue;
            if( filter == InventoryData.Options.CardsInBinderX )
            {
                int binderIndex = dropDownIdx - ( int )InventoryData.Options.CardsInBinderX;
                if( card.insideBinderIdx == null || card.insideBinderIdx.Value != binderIndex )
                    continue;
            }

            var betaName = card.cardAPIData.misc_info != null && card.cardAPIData.misc_info.Count > 0 
                ? ( card.cardAPIData.misc_info[0].beta_name ?? string.Empty )
                : string.Empty;

            if( !FilterCard( search, card.cardAPIData ) )
                continue;

            count++;
            var priceStr = card.cardAPIData.card_sets != null ?
                card.cardAPIData.card_sets[card.cardIndex].set_price :
                card.cardAPIData.card_prices[0].ebay_price;
            if( float.TryParse( priceStr, out float price ) )
                totalValue += price;

            // Limit to 100 results for now
            if( idx >= maxSearchResults )
                break;

            results.Add( card );
        }

        SortAndAddResults( results );

        if( cardCountText != null )
            cardCountText.text = String.Format( "{0} Card{1}", count, count == 1 ? string.Empty : "s" );
        if( totalValueText != null ) 
            totalValueText.text = String.Format( "Total Value: ${0:0.00}", totalValue );

        if( count == 0 )
            AddCard( new CardDataRuntime() { name = "No cards found" } );
    }

    protected virtual bool FilterCard( string search, Datum card )
    {
        var betaName = card.misc_info != null && card.misc_info.Count > 0
            ? ( card.misc_info[0].beta_name ?? string.Empty )
            : string.Empty;

        if( !card.name.ToLower().Contains( search.ToLower() ) &&
            !betaName.ToLower().Contains( search.ToLower() ) )
            return false;

        return true;
    }

    protected virtual void SortAndAddResults( List<CardDataRuntime> results )
    {
        results.Sort( ( x, y ) => string.Compare( x.name, y.name, StringComparison.Ordinal ) );

        foreach( var card in results )
            AddCard( card );
    }

    protected List<CardDataRuntime> CreateCardsFromSearchResult( List<Datum> cards )
    {
        if( cards == null || cards.IsEmpty() )
        {
            cardCountText?.gameObject.SetActive( false );
            return new List<CardDataRuntime>() { new CardDataRuntime() { name = "No results found" } };
        }
        else
        {
            var results = new List<CardDataRuntime>( cards.Count );

            foreach( var (idx, card) in cards.Enumerate() )
            {
                // Limit to 100 results for now
                if( idx >= maxSearchResults )
                    break;

                results.Add( new CardDataRuntime()
                {
                    name = card.name,
                    cardId = card.id,
                    cardIndex = 0,
                    cardAPIData = card,
                    condition = CardConditions.Values.NearMint,
                    count = 1,
                } );
            }

            if( cardCountText != null )
                cardCountText.text = String.Format( "{0} Card{1}", cards.Count, cards.Count == 1 ? string.Empty : "s" );

            return results;
        }
    }

    protected GameObject GetSelectedCard()
    {
        return currentCardSelectedIdx.HasValue ? cardList.transform.GetChild( ( ContainsHeader() ? 1 : 0 ) + currentCardSelectedIdx.Value ).gameObject : null;
    }

    public void AddCard( CardDataRuntime card )
    {
        // TO DO - Handle count
        if( searchUIEntries.TryGetValue( card, out var existing ) )
        {

            return;
        }

        int thisIdx = cardData.Count;
        var newCardUIEntry = AddCardUI( card, thisIdx );
        searchUIEntries.Add( card, newCardUIEntry );
        cardData.Add( card );

        // On click
        var eventDispatcher = newCardUIEntry.GetComponentInChildren<EventDispatcher>();

        // Used for the empty 'no search results' card (not a real card - has no data)
        if( card.cardAPIData == null )
            return;

        eventDispatcher.OnPointerDownEvent += ( e ) => AppUtility.LeftMouseFilter( true, e, () =>
        {
            ToggleResultSelected( thisIdx );
        } );

        if( behaviour != SearchPageOrigin.MainPage )
        {
            eventDispatcher.OnDoubleClickEvent += ( PointerEventData e ) =>
            {
                currentCardSelectedIdx = thisIdx;
                ChooseCard();
            };
        }

        // TODO: Hover to show card image?
        eventDispatcher.OnPointerEnterEvent += ( PointerEventData e ) =>
        {

        };

        eventDispatcher.OnPointerExitEvent += ( PointerEventData e ) =>
        {

        };
    }

    protected void ToggleResultSelected( int index )
    {
        bool unselect = currentCardSelectedIdx == index;
        if( currentCardSelectedIdx != null || unselect )
            UnselectCurrentResult();
        if( !unselect )
            searchUIEntries[cardData[index]].GetComponent<SearchListEntry>().SetBackgroundColour( selectedEntryColour );
        currentCardSelectedIdx = unselect ? null : index as int?;
    }

    protected void UnselectCurrentResult()
    {
        if( GetSelectedCard() != null )
            GetSelectedCard().GetComponent<SearchListEntry>().SetBackgroundColour( Color.clear );
        currentCardSelectedIdx = null;
    }

    protected abstract GameObject AddCardUI( CardDataRuntime card, int entryIdx );

    // Called when you either double click a search result, or click to highlight and then click 'Select Card' button
    public void ChooseCard()
    {
        ChooseCardInternal( false );
    }

    protected virtual void ShowInfoMessage( string msg ) { }

    protected void ChooseCardInternal( bool fromDragDrop )
    {
        if( !fromDragDrop
            && flags.HasFlag( SearchPageFlags.PageFull )
            && !flags.HasFlag( SearchPageFlags.ReplacingCard ) )
        {
            if( flags.HasFlag( SearchPageFlags.BinderFull ) )
            {
                ShowInfoMessage( "Failed: ".Red() + "Binder full".Black() );
                return;
            }
            else if( !flags.HasFlag( SearchPageFlags.RequestedFillAllPages ) )
            {
                flags |= SearchPageFlags.RequestedFillAllPages;
                dialog.gameObject.SetActive( true );
                dialog.onConfirm += () =>
                {
                    flags |= SearchPageFlags.FillAllPages;
                    ChooseCardInternal( fromDragDrop );
                };
                return;
            }
            else if ( !flags.HasFlag( SearchPageFlags.FillAllPages ) )
            {
                ShowInfoMessage( "Failed: ".Red() + "Page full".Black() );
                return;
            }
        }

        Debug.Assert( currentCardSelectedIdx != null );

        bool fromInventory = FromInventory();

        var data = cardData[currentCardSelectedIdx.Value];

        if( !fromInventory )
            data = data.DeepCopy();

        if( data.smallImages == null )
        {
            ShowInfoMessage( "Failed: ".Red() + "Card image not loaded".Black() );
            return;
        }

        ShowInfoMessage( "Added Card: ".Blue() + data.name.Black() );

        EventSystem.Instance.TriggerEvent( new CardSelectedEvent()
        {
            card = data,
            fromDragDrop = fromDragDrop,
            fromInventory = fromInventory,
            findEmptySlotInBinder = flags.HasFlag( SearchPageFlags.FillAllPages ),
        } );

        // Only hide this page if we are selecting for a specific card
        // This intentionally will switch back to the card list if the page is now full
        // (pageFull set to true via the PageFullEvent)
        if( !fromDragDrop && ( flags.HasFlag( SearchPageFlags.ReplacingCard ) || flags.HasFlag( SearchPageFlags.SettingCards ) ) )
        {
            EventSystem.Instance.TriggerEvent( new CloseSearchPageEvent()
            {
                page = PageType.CardPage,
                fromFullscreen = behaviour == SearchPageOrigin.CardPageSearch ? ContainsHeader() as bool? : null
            } );
        }

        if( Constants.Instance.DownloadImages && Constants.Instance.DownloadLargeImages )
        {
            data.largeImageRequested = true;

            StartCoroutine( APICallHandler.Instance.DownloadImage( data.cardAPIData.card_images[data.imageIndex].image_url, true, ( texture ) =>
            {
                // TODO: Save/cache image
                data.largeImage = texture;
                data.largeImageRequested = false;

                EventSystem.Instance.TriggerEvent( new CardImageLoadedEvent()
                {
                    card = data,
                } );
            } ) );
        }

        if( fromInventory )
        {
            SearchCards();
        }
    }

    protected bool FromInventory()
    {
        return GetDropDownOption() == InventoryData.Options.AllCards
                || GetDropDownOption() == InventoryData.Options.UnusedCards
                || GetDropDownOption() == InventoryData.Options.AllCardsInBinders
                || GetDropDownOption() == InventoryData.Options.CardsInBinderX;
    }

    public void Cancel()
    {
        if( behaviour == SearchPageOrigin.MainPage )
            EventSystem.Instance.TriggerEvent( new SaveGameEvent() { } );

        EventSystem.Instance.TriggerEvent( new CloseSearchPageEvent() 
        { 
            page = behaviour == SearchPageOrigin.MainPage
                ? PageType.BinderPage 
                : PageType.CardPage,
            fromFullscreen = behaviour == SearchPageOrigin.CardPageSearch ? ContainsHeader() as bool? : null
        } );

        flags &= ~SearchPageFlags.RequestedFillAllPages;
    }

    public void ShowFullscreenSearch( bool fullscreen )
    {
        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent()
        {
            page = fullscreen ? PageType.SearchPageFull : PageType.SearchPage,
            behaviour = behaviour,
            flags = flags,
            replacingCard = replacingCardName,
            searchText = searchInput.text,
            currentBinderIdx = currentBinderIdx,
        } );
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is PageChangeRequestEvent pageChangeRequest 
            && pageChangeRequest.page != PageType.SearchPage
            && pageChangeRequest.page != PageType.SearchPageFull )
        {
            HidePage();
        }
        else if( e is PageFullEvent )
        {
            flags |= SearchPageFlags.PageFull;
        }
        else if( e is BinderFullEvent )
        {
            flags |= SearchPageFlags.BinderFull;
        }
    }

    protected virtual void HidePage()
    {
        searchListPage.SetActive( false );
    }

    protected virtual void ShowPage( OpenSearchPageEvent request, int? binderIndex )
    {
        behaviour = request.behaviour;
        flags = request.flags;
        currentBinderIdx = binderIndex;
        replacingCardName = request.replacingCard;
        searchInput.SetTextWithoutNotify( request.searchText );
        ShowPageInternal();
    }

    protected virtual void ShowPageInternal()
    {
        searchListPage.SetActive( true );
        clearCardButton.gameObject.SetActive( flags.HasFlag( SearchPageFlags.ReplacingCard ) );
        PopulateOptions();
        SetDropDownOption( GetDefaultBehaviour() );
        SearchCards();
        UpdateButtons();
    }

    protected virtual void UpdateButtons()
    {
        minimiseMaximiseButton?.gameObject.SetActive( IsMinimiseButtonActive() );
        totalValueText?.gameObject.SetActive( IsCardValueTextActive() );
        cardCountText?.gameObject.SetActive( IsCardCountTextActive() );
    }

    private void SetDropDownOption( InventoryData.Options option )
    {
        optionsDropdown.SetValueWithoutNotify( ( int )option - ( tempImportInventory == null ? 1 : 0 ) );
    }

    protected int GetDropDownOptionIdx()
    {
        return optionsDropdown.value + ( tempImportInventory == null ? 1 : 0 );
    }

    protected InventoryData.Options GetDropDownOption()
    {
        return ( InventoryData.Options )( Mathf.Min( GetDropDownOptionIdx(), ( int )InventoryData.Options.OptionsCount - 1 ) );
    }

    private void PopulateOptions()
    {
        List<string> options = new List<string>();

        foreach( var (val, str) in Utility.GetEnumValues<InventoryData.Options>().Zip( InventoryData.optionStrings ) )
        {
            if( tempImportInventory == null && val == InventoryData.Options.TempInventory )
                continue;

            if( val == InventoryData.Options.CardsInBinderX )
                options.AddRange( BinderPage.Instance.BinderData
                        .Where( ( x ) => x.data.HasCards )
                        .Select( ( x ) => string.Format( str, x.data.name ) ) );
            else
                options.Add( str );
        }

        optionsDropdown.ClearOptions();
        optionsDropdown.AddOptions( options );
    }

    public void ImportFromFile()
    {
        BinderPage.Instance.LoadFromDragonShieldTxtFile( ( importData ) =>
        {
            tempImportInventory = new InventoryStorage( importData.cards, false );
            ShowPageInternal();
        } );
    }

    InventoryData.Options GetDefaultBehaviour()
    {
        if( savedBehaviour != null )
            return savedBehaviour.Value;

        if( tempImportInventory != null )
            return InventoryData.Options.TempInventory;

        if( behaviour == SearchPageOrigin.MainPage )
            return InventoryData.Options.AllCards;

        if( behaviour == SearchPageOrigin.CardPageInventory )
            return InventoryData.Options.CardsInBinderX + currentBinderIdx.Value;

        return InventoryData.Options.SearchOnline;
    }

    bool IsMinimiseButtonActive()
    {
        return behaviour != SearchPageOrigin.MainPage;
    }

    bool IsCardCountTextActive()
    {
        return true;
    }

    bool IsCardValueTextActive()
    {
        return GetDropDownOption() != InventoryData.Options.SearchOnline;
    }
}