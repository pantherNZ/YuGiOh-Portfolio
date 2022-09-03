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
    [SerializeField] protected Color selectedEntryColour = new();
    [SerializeField] protected bool autoSearch = true;
    [SerializeField] protected int maxSearchResults = 100;
    [SerializeField] protected int autoSearchDelayMS = 500;
    [SerializeField] TMPro.TMP_Dropdown optionsDropdown = null;
    [SerializeField] TMPro.TextMeshProUGUI cardCountText = null;
    [SerializeField] TMPro.TextMeshProUGUI totalValueText = null;

    protected List<CardDataRuntime> cardData = new();
    protected List<CardDataRuntime> tempImportInventory;
    protected Dictionary<CardDataRuntime, GameObject> searchUIEntries = new();
    protected int? currentCardSelectedIdx;
    protected SearchPageBehaviour behaviour = SearchPageBehaviour.None;
    private Coroutine searchCountdown;
    private bool pageFull;

    protected override void Start()
    {
        base.Start();

        searchListPage.SetActive( false );

        searchInput.onValueChanged.AddListener( OnSearchTextchanged );

        optionsDropdown.onValueChanged.AddListener( ( _ ) =>
        {
            if( GetDropDownOption() != ( int )InventoryData.Options.TempInventory && tempImportInventory != null )
            {
                tempImportInventory = null;
                PopulateOptions();
            }

            SearchCards();
        } );
    }

    private void OnSearchTextchanged( string text )
    {
        if( searchCountdown != null )
            StopCoroutine( searchCountdown );

        searchCountdown = StartCoroutine( TextChangedTimer() );
    }

    private IEnumerator TextChangedTimer()
    {
        yield return new WaitForSeconds( autoSearchDelayMS / 1000.0f );
        SearchCards();
    }

    public void SearchCards()
    {
        // Remove current card entries (skip/leave header)
        for( int i = 0; i < cardList.transform.childCount; ++i )
            cardList.transform.GetChild( i ).gameObject.Destroy();
        currentCardSelectedIdx = null;
        searchUIEntries.Clear();
        cardData.Clear();


        bool inventoryMode = behaviour == SearchPageBehaviour.Inventory || behaviour == SearchPageBehaviour.InventoryFromCardPage;

        if( totalValueText != null )
            totalValueText.gameObject.SetActive( inventoryMode && GetDropDownOption() != InventoryData.Options.SearchOnline );
        if( cardCountText != null )
            cardCountText.gameObject.SetActive( true );

        var search = searchInput.text.Trim();
        SearchRequest( search );
    }

    void SearchRequest( string search )
    {
        var dropDownIdx = GetDropDownOptionIdx();
        var filter = GetDropDownOption();

        if( filter == InventoryData.Options.SearchOnline )
        {
            if( search.Length > 0 )
                StartCoroutine( APICallHandler.Instance.SendCardSearchRequestFuzzy( search, false, OnSearchResultReceived ) );
            else if( cardCountText != null )
                cardCountText.gameObject.SetActive( false );
            return;
        }

        var count = 0;
        var totalValue = 0.0f;

        var inventory = filter == InventoryData.Options.TempInventory
            ? tempImportInventory
            : BinderPage.Instance.Inventory;

        foreach( var( idx, card ) in inventory.Enumerate() )
        {
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
            if( search.Length > 0 && !card.name.Contains( search ) )
                continue;

            count++;
            if( float.TryParse( card.cardAPIData.card_sets[card.cardIndex].set_price, out float price ) )
                totalValue += price;

            // Limit to 100 results for now
            if( idx >= maxSearchResults )
                break;

            AddCard( card );
        }

        if( cardCountText != null )
            cardCountText.text = String.Format( "{0} Cards", count );
        if( totalValueText != null ) 
            totalValueText.text = String.Format( "Total Value: ${0:0.00}", totalValue );
    }

    void OnSearchResultReceived( string result )
    {
        try
        {
            Root data = JsonConvert.DeserializeObject<Root>( result );

            if( data.data.IsEmpty() )
            {
                AddCard( new CardDataRuntime() { name = "No results found" } );
                if( cardCountText != null ) 
                    cardCountText.gameObject.SetActive( false );
            }
            else
            {
                foreach( var (idx, card) in data.data.Enumerate() )
                {
                    // Limit to 100 results for now
                    if( idx >= maxSearchResults )
                        break;

                    AddCard( new CardDataRuntime()
                    {
                        name = card.name,
                        cardId = card.id,
                        cardIndex = 0,
                        cardAPIData = card.DeepCopy(),
                        condition = CardCondition.NearMint, // TODO
                    } );
                }

                if( cardCountText != null ) 
                    cardCountText.text = String.Format( "{0} Cards", data.data.Count );
            }
        }
        catch( Exception e )
        {
            Debug.LogError( "SearchPageBase::OnSearchResultReceived failed to deserialize json from result:" + Environment.NewLine + e.Message );
        }
    }

    protected GameObject GetSelectedCard()
    {
        return currentCardSelectedIdx.HasValue ? cardList.transform.GetChild( currentCardSelectedIdx.Value ).gameObject : null;
    }

    public void AddCard( CardDataRuntime card )
    {
        int thisIdx = cardData.Count;
        var newCardUIEntry = AddCardUI( card, thisIdx );
        searchUIEntries.Add( card, newCardUIEntry );
        cardData.Add( card );

        // On click
        var eventDispatcher = newCardUIEntry.GetComponentInChildren<EventDispatcher>();

        newCardUIEntry.GetComponentsInChildren<Image>()[1].color = Color.clear;

        eventDispatcher.OnPointerDownEvent += ( PointerEventData e ) =>
        {
            bool unselect = currentCardSelectedIdx == thisIdx;
            if( currentCardSelectedIdx != null || unselect )
                GetSelectedCard().GetComponentInChildren<Image>().color = Color.clear;
            if( !unselect )
                newCardUIEntry.GetComponentInChildren<Image>().color = selectedEntryColour;
            currentCardSelectedIdx = unselect ? null : thisIdx;
        };

        if( behaviour != SearchPageBehaviour.Inventory )
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

        GetCardPreviewImage( card );
    }

    private void GetCardPreviewImage( CardDataRuntime card )
    {
        if( Constants.Instance.DownloadImages && card.cardAPIData != null )
        {
            if( card.smallImage != null )
            {
                OnImageDownloaded( card.smallImage, card );
            }
            else
            {
                var smallImageUrl = card.cardAPIData.card_images[card.imageIndex].image_url_small;
                StartCoroutine( APICallHandler.Instance.DownloadImage( smallImageUrl, true, ( texture ) => OnImageDownloaded( texture, card ) ) );
            }
        }
    }

    private void OnImageDownloaded( Texture2D texture, CardDataRuntime cardData )
    {
        if( !searchUIEntries.ContainsKey( cardData ) )
            return;

        // Idx 1 because 0 is the UI entry background (1 is the preview card image)
        var cardPreview = searchUIEntries[cardData].GetComponentsInChildren<Image>()[1];
        cardPreview.sprite = Utility.CreateSprite( texture );
        cardPreview.color = Color.white;
        cardData.smallImage = texture;
    }

    protected abstract GameObject AddCardUI( CardDataRuntime card, int entryIdx );

    // Called when you either double click a search result, or click to highlight and then click 'Select Card' button
    public void ChooseCard()
    {
        ChooseCardInternal( false );
    }

    protected void ChooseCardInternal( bool fromDragDrop )
    {
        if( !fromDragDrop && pageFull )
            return;

        Debug.Assert( currentCardSelectedIdx != null );

        var data = cardData[currentCardSelectedIdx.Value];

        if( data.smallImage == null )
        {
            Debug.LogWarning( "Failed to choose card as the preview image hasn't finished downloading yet" );
            return;
        }

        EventSystem.Instance.TriggerEvent( new CardSelectedEvent()
        {
            card = data,
            fromDragDrop = fromDragDrop,
        } );

        // Only hide this page if we are selecting for a specific card
        // This intentionally will switch back to the card list if the page is now full
        // (pageFull set to true via the PageFullEvent)
        if( !fromDragDrop && behaviour != SearchPageBehaviour.AddingCards )
            searchListPage.SetActive( false );

        if( Constants.Instance.DownloadImages && Constants.Instance.DownloadLargeImages )
        {
            data.largeImageRequsted = true;

            StartCoroutine( APICallHandler.Instance.DownloadImage( data.cardAPIData.card_images[data.imageIndex].image_url, true, ( texture ) =>
            {
                // TODO: Save/cache image
                data.largeImage = texture;
                data.largeImageRequsted = false;

                EventSystem.Instance.TriggerEvent( new CardImageLoadedEvent()
                {
                    card = data,
                } );
            } ) );
        }
    }

    public void Cancel()
    {
        EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent() 
        { 
            page = behaviour == SearchPageBehaviour.Inventory 
                ? PageType.BinderPage 
                : PageType.CardPage 
        } );
    }

    public void ShowFullscreenSearch( bool fullscreen )
    {
        var replaceOrSet = behaviour == SearchPageBehaviour.ReplacingCard || behaviour == SearchPageBehaviour.SettingCard;

        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent()
        {
            page = fullscreen ? PageType.SearchPageFull : PageType.SearchPage,
            behaviour = ( fullscreen && replaceOrSet ) ? SearchPageBehaviour.AddingCards : behaviour
        } );
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is PageChangeRequestEvent pageChangeRequest && pageChangeRequest.page != PageType.SearchPage )
        {
            searchListPage.SetActive( false );
        }
        else if( e is PageFullEvent )
        {
            Debug.Assert( behaviour == SearchPageBehaviour.AddingCards );
            pageFull = true;
        }
    }

    protected virtual void ShowPage( OpenSearchPageEvent request, int? binderIndex )
    {
        behaviour = request.behaviour;
        pageFull = request.pageFull;
        searchListPage.SetActive( true );
        PopulateOptions();

        // Set default
        if( binderIndex != null )
        {
            SetDropDownOption( InventoryData.Options.CardsInBinderX + binderIndex.Value );
        }
        else if( tempImportInventory != null )
        {
            SetDropDownOption( InventoryData.Options.TempInventory );
        }
        else if( behaviour == SearchPageBehaviour.Inventory || behaviour == SearchPageBehaviour.InventoryFromCardPage )
        {
            SetDropDownOption( InventoryData.Options.AllCards );
        }
        else
        {
            SetDropDownOption( InventoryData.Options.SearchOnline );
        }

        SearchCards();
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
        List<string> options = new();

        foreach( var (val, str) in Utility.GetEnumValues<InventoryData.Options>().Zip( InventoryData.optionStrings ) )
        {
            if( tempImportInventory == null && val == InventoryData.Options.TempInventory )
                continue;

            if( val == InventoryData.Options.CardsInBinderX )
                options.AddRange( BinderPage.Instance.BinderData.Select( ( x ) => string.Format( str, x.data.name ) ) );
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
            tempImportInventory = importData.cards;
            ShowPage( new OpenSearchPageEvent() { behaviour = behaviour, pageFull = pageFull }, null );
        } );
    }
}