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
    protected Dictionary<CardDataRuntime, GameObject> searchUIEntries = new();
    protected int? currentCardSelectedIdx;
    protected SearchPageBehaviour behaviour = SearchPageBehaviour.None;
    private Coroutine searchCountdown;

    protected override void Start()
    {
        base.Start();

        searchListPage.SetActive( false );

        searchInput.onValueChanged.AddListener( OnSearchTextchanged );

        optionsDropdown.onValueChanged.AddListener( ( x ) =>
        {
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
            totalValueText.gameObject.SetActive( inventoryMode && optionsDropdown.value != ( int )InventoryData.Options.SearchOnline );
        if( cardCountText != null )
            cardCountText.gameObject.SetActive( true );

        var search = searchInput.text.Trim();
        SearchRequest( search );
    }

    void SearchRequest( string search )
    {
        var filter = ( InventoryData.Options )( Mathf.Min( optionsDropdown.value, ( int )InventoryData.Options.OptionsCount - 1 ) );

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

        foreach( var( idx, card ) in BinderPage.Instance.Inventory.Enumerate() )
        {
            if( filter == InventoryData.Options.AllCardsInBinders && card.insideBinderIdx == null )
                continue;
            if( filter == InventoryData.Options.UnusedCards && card.insideBinderIdx != null )
                continue;
            if( filter == InventoryData.Options.CardsInBinderX )
            {
                int binderIndex = optionsDropdown.value - ( int )InventoryData.Options.CardsInBinderX;
                if( card.insideBinderIdx == null || card.insideBinderIdx.Value != binderIndex )
                    continue;
            }
            if( search.Length > 0 && !card.name.Contains( search ) )
                continue;

                        count++;
            if( float.TryParse( card.cardAPIData.card_prices[0].tcgplayer_price, out float price ) )
                totalValue += price;

            // Limit to 100 results for now
            if( idx >= maxSearchResults )
                break;

            AddCard( card );
        }

        if( cardCountText != null )
            cardCountText.text = String.Format( "{0} Cards", count );
        if( totalValueText != null ) 
            totalValueText.text = String.Format( "Total Vaue: ${0}", totalValue );
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
                var totalValue = 0.0f;

                foreach( var (idx, card) in data.data.Enumerate() )
                {
                    // Limit to 100 results for now
                    if( idx >= maxSearchResults )
                        break;

                    AddCard( new CardDataRuntime()
                    {
                        name = card.name,
                        cardId = card.id,
                        imageId = card.card_images[0].id,
                        cardAPIData = card.DeepCopy(),
                    } );

                    if( float.TryParse( card.card_prices[0].tcgplayer_price, out float price ) )
                        totalValue += price;
                }

                if( cardCountText != null ) 
                    cardCountText.text = String.Format( "{0} Cards", data.data.Count );
                if( totalValueText != null ) 
                    totalValueText.text = String.Format( "Total Vaue: ${0}", totalValue );
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
            currentCardSelectedIdx = unselect ? null : thisIdx as int?;
        };

        // TODO: Double click to choose
        eventDispatcher.OnDoubleClickEvent += ( PointerEventData e ) =>
        {
            currentCardSelectedIdx = thisIdx;
            ChooseCard();
        };

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
                var smallImageUrl = card.cardAPIData.card_images[0].image_url_small;
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
        if( !fromDragDrop && behaviour == SearchPageBehaviour.AddingCardsPageFull )
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
        // (new behaviour will be set to AddingCardsPageFull via the PageFullEvent)
        if( !fromDragDrop && behaviour != SearchPageBehaviour.AddingCards )
            searchListPage.SetActive( false );

        if( Constants.Instance.DownloadImages && Constants.Instance.DownloadLargeImages )
        {
            data.largeImageRequsted = true;

            StartCoroutine( APICallHandler.Instance.DownloadImage( data.cardAPIData.card_images[0].image_url, true, ( texture ) =>
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
        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent()
        {
            page = fullscreen ? PageType.SearchPageFull : PageType.SearchPage,
            behaviour = fullscreen ? SearchPageBehaviour.AddingCards : behaviour
        } );
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is PageChangeRequestEvent pageChangeRequest && pageChangeRequest.page != PageType.SearchPage )
        {
            searchListPage.SetActive( false );
        }
    }

    protected virtual void ShowPage( SearchPageBehaviour newBehaviour, int? binderIndex )
    {
        behaviour = newBehaviour;

        searchListPage.SetActive( true );

        List<string> options = new();

        foreach( var (val, str) in Utility.GetEnumValues<InventoryData.Options>().Zip( InventoryData.optionStrings ) )
        {
            if( val == InventoryData.Options.CardsInBinderX )
                options.AddRange( BinderPage.Instance.BinderData.Select( ( x ) => string.Format( str, x.data.name ) ) );
            else
                options.Add( str );
        }

        optionsDropdown.ClearOptions();
        optionsDropdown.AddOptions( options );

        // Set default
        bool inventoryMode = behaviour == SearchPageBehaviour.Inventory || behaviour == SearchPageBehaviour.InventoryFromCardPage; 
        optionsDropdown.SetValueWithoutNotify(
            binderIndex != null
                ? ( int )InventoryData.Options.CardsInBinderX + binderIndex.Value
                : inventoryMode
                ? ( int )InventoryData.Options.AllCards
                : ( int )InventoryData.Options.SearchOnline );

        SearchCards();
    }
}