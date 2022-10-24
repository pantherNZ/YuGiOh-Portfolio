using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardPage : EventReceiverInstance
{
    [SerializeField] GameObject cardsPage = null;
    [SerializeField] AdvancedGridLayout cardsDisplayGridLeft = null;
    [SerializeField] AdvancedGridLayout cardsDisplayGridRight = null;
    [SerializeField] Texture2D defaultCardImage = null;
    [SerializeField] Button prevPageButton = null;
    [SerializeField] Button nextPageButton = null;
    [SerializeField] Button firstPageButton = null;
    [SerializeField] Button lastPageButton = null;
    [SerializeField] TMPro.TextMeshProUGUI binderNameText = null;
    [SerializeField] TMPro.TextMeshProUGUI numPagesText = null;
    [SerializeField] TMPro.TextMeshProUGUI numCardsText = null;
    [SerializeField] TMPro.TextMeshProUGUI percentFullText = null;
    [SerializeField] Image percentFullImage = null;
    [SerializeField] TMPro.TextMeshProUGUI currentPageTextLeft = null;
    [SerializeField] TMPro.TextMeshProUGUI currentPageTextRight = null;
    [SerializeField] GameObject dragCardGhostPrefab = null;
    [SerializeField] GameObject modifyPageButtonsLeft = null;
    [SerializeField] GameObject modifyPageButtonsRight = null;
    [SerializeField] GameObject clearCardDropLocation = null;

    private BinderDataRuntime currentbinder;
    private int width;
    private int height;
    private int currentPage;
    private int? currentModifyCardIdx;
    private bool openFullScreenSearch;

    // Drag data
    private GameObject dragging;
    private Vector2 dragOffset;
    private int dragCardIdx;

    protected override void Start()
    {
        base.Start();
        cardsPage.SetActive( false );

        prevPageButton.onClick.AddListener( PrevPage );
        nextPageButton.onClick.AddListener( NextPage );
        firstPageButton.onClick.AddListener( () => ChangePage( 0 ) );
        lastPageButton.onClick.AddListener( () => ChangePage( currentbinder.data.pageCount ) );

        width = Constants.Instance.DefaultStartingPageWidth;
        height = Constants.Instance.DefaultStartingPageHeight;

        clearCardDropLocation.SetActive( false );
    }

    public void SaveAndExit()
    {
        EventSystem.Instance.TriggerEvent( new SaveGameEvent() { } );
        EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent() { page = PageType.BinderPage } );
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is PageChangeRequestEvent pageChangeRequest )
        {
            switch( pageChangeRequest.page )
            {
                case PageType.BinderPage:
                    cardsPage.SetActive( false );
                    break;
                case PageType.CardPage:
                    if( e is CloseSearchPageEvent cancelRequest && cancelRequest.fromFullscreen != null )
                        openFullScreenSearch = cancelRequest.fromFullscreen.Value;

                    cardsPage.SetActive( true );
                    if( e is OpenCardPageEvent openPageRequest )
                        LoadBinder( openPageRequest.binder );
                    break;
            }
        }
        else if( e is CardSelectedEvent cardSelectedEvent )
        {
            LoadCard( cardSelectedEvent );
        }
        else if( e is BinderLoadedEvent binderLoadedEvent )
        {
            //LoadBinder( binderLoadedEvent.data );
        }
        else if( e is CardImageLoadedEvent cardImageLoadedEvent )
        {
            if( currentbinder == null )
                return;

            if( currentPage < currentbinder.data.pageCount )
            {
                var foundCard = currentbinder.data.cardList[currentPage].FindIndex( x => x == cardImageLoadedEvent.card );
                if( foundCard != -1 )
                {
                    var cardUIEntry = cardsDisplayGridRight.transform.GetChild( foundCard );
                    cardUIEntry.GetComponent<Image>().sprite = Utility.CreateSprite( cardImageLoadedEvent.card.largeImage );
                }

            }
            
            if( currentPage > 0 )
            {
                var foundCard = currentbinder.data.cardList[currentPage - 1].FindIndex( x => x == cardImageLoadedEvent.card );
                if( foundCard != -1 )
                {
                    var cardUIEntry = cardsDisplayGridLeft.transform.GetChild( foundCard );
                    cardUIEntry.GetComponent<Image>().sprite = Utility.CreateSprite( cardImageLoadedEvent.card.largeImage );
                }
            }
        }
    }

    private void LoadBinder( BinderDataRuntime binder )
    {
        currentbinder = binder;
        UpdateHeaderInfo();
        ChangePage( 0 );
    }

    private void UpdateHeaderInfo()
    {
        binderNameText.text = currentbinder.data.name;
        numPagesText.text = String.Format( "{0} Pages", currentbinder.data.pageCount );
        var numCards = currentbinder.data.NumCards;
        var maxCards = currentbinder.data.MaxCards;
        numCardsText.text = String.Format( "{0}/{1} Cards", numCards, maxCards );
        percentFullText.text = String.Format( "{0}%\nFull", Mathf.CeilToInt( 100.0f * numCards / maxCards ) );
        percentFullImage.fillAmount = numCards / ( float )maxCards;
    }

    private string GetCurrentPageSizeString()
    {
        return string.Format( "{0}x{1}", currentbinder.data.pageWidth, currentbinder.data.pageHeight );
    }

    private int? FindNextEmptyCardSlot()
    {
        for( int pageIdx = Mathf.Max( 0, currentPage - 1 ); pageIdx <= currentPage && pageIdx < currentbinder.data.cardList.Count; ++pageIdx )
        {
            for( int cardIdx = 0; cardIdx < currentbinder.data.pageWidth * currentbinder.data.pageHeight; ++cardIdx )
            {
                if( currentbinder.data.cardList[pageIdx][cardIdx] == null )
                    return Utility.Mod( pageIdx + 1, 2 ) * currentbinder.data.pageWidth * currentbinder.data.pageHeight + cardIdx;
            }
        }

        return null;
    }

    private void LoadCard( CardSelectedEvent e )
    {
        var data = e.card;

        // From drag and drop - calculate mouse pos to determine which card to replace
        if( e.fromDragDrop )
        {
            currentModifyCardIdx = null;

            for( int i = 0; i < currentbinder.data.pageWidth * currentbinder.data.pageHeight; ++i )
            {
                if( SearchStopDraggingCollisionCheck( currentPage, i ) )
                {
                    currentModifyCardIdx = GetIndexFromPageAndPos( currentPage, i );
                    break;
                }

                if( SearchStopDraggingCollisionCheck( currentPage - 1, i ) )
                {
                    currentModifyCardIdx = GetIndexFromPageAndPos( currentPage - 1, i );
                    break;
                }
            }

            // No match/target card overlap found
            if( currentModifyCardIdx == null )
                return;
        }
        // Null card means we just want to add card to next valid empty card slot
        else if( currentModifyCardIdx == null )
        {
            currentModifyCardIdx = FindNextEmptyCardSlot();

            // TODO: Handle properly
            if( currentModifyCardIdx == null )
            {
                Debug.LogWarning( "No empty slot found to add card to on this page" );
                return;
            }
        }

        if( data != null )
        {
            // Add to inventory if a new card
            if( data.insideBinderIdx == null && !e.fromInventory )
            {
                BinderPage.Instance.Inventory.Add( data );
            }
            // Otherwise remove from binder
            else if( data.insideBinderIdx != null )
            {
                int foundIdx = -1;
                int foundPage = BinderPage.Instance.BinderData[data.insideBinderIdx.Value].data.cardList.FindIndex( ( cardPage ) =>
                {
                    foreach( var (idx, card) in cardPage.Enumerate() )
                    {
                        if( card == data )
                        {
                            foundIdx = idx;
                            return true;
                        }
                    }
                    return false;
                } );

                if( data.insideBinderIdx != null )
                {
                    // If this is in the current binder, we need to update the UI (and clear the card)
                    if( BinderPage.Instance.BinderData[data.insideBinderIdx.Value] == currentbinder )
                    {
                        var prevIdx = currentModifyCardIdx;
                        currentModifyCardIdx = GetIndexFromPageAndPos( foundPage, foundIdx );
                        LoadCard( new CardSelectedEvent() { card = null } );
                        currentModifyCardIdx = prevIdx;
                    }
                    else
                    {
                        // Clear this card from the old binder
                        BinderPage.Instance.BinderData[data.insideBinderIdx.Value].data.cardList[foundPage][foundIdx] = null;
                    }
                }
            }
        }

        var (page, pos) = GetPageAndPosFromIndex( currentModifyCardIdx.Value );
        var grid = GetGrid( page );
        var image = grid.transform.GetChild( pos ).GetComponent<Image>();

        image.sprite = Utility.CreateSprite( data == null ? defaultCardImage : data.smallImages[data.imageIndex] );
        var prevCard = currentbinder.data.cardList[page][pos];
        currentbinder.data.cardList[page][pos] = data;
        UpdateHeaderInfo();
        currentModifyCardIdx = null;

        if( data != null )
        {
            if( !e.fromDragDrop && FindNextEmptyCardSlot() == null )
                EventSystem.Instance.TriggerEvent( new PageFullEvent() );

            data.insideBinderIdx = BinderPage.Instance.BinderData.IndexOf( currentbinder );
        }

        // Set previous card to not being inside a binder
        if( prevCard != null )
        {
            BinderPage.Instance.Inventory.Find( ( x ) => x.cardId == prevCard.cardId ).insideBinderIdx = null;
        }

        EventSystem.Instance.TriggerEvent( new SaveGameEvent() { } );
    }

    private void PopulateGrid()
    {
        // Setup page (and secondary/adjacent page)
        if( currentPage > 0 )
            SetupGrid( currentPage - 1 );

        if( currentPage < currentbinder.data.pageCount - 1 )
            SetupGrid( currentPage );

        // Show/hide page depending on first/last page
        foreach( Transform child in cardsDisplayGridLeft.transform )
            child.gameObject.SetActive( currentPage > 0 );
        foreach( Transform child in cardsDisplayGridRight.transform )
            child.gameObject.SetActive( currentPage < currentbinder.data.pageCount - 1 );

        // Show/hide next/prev buttons depending on first/last page
        prevPageButton.gameObject.SetActive( currentPage > 0 );
        nextPageButton.gameObject.SetActive( currentPage < currentbinder.data.pageCount - 1 );

        // Show/hide first/last buttons depending on first/last page
        firstPageButton.gameObject.SetActive( currentPage > 0 );
        lastPageButton.gameObject.SetActive( currentPage < currentbinder.data.pageCount - 1 );

        // Show/hide modify buttons depending on first/last page
        modifyPageButtonsLeft.SetActive( currentPage > 0 );
        modifyPageButtonsRight.SetActive( currentPage < currentbinder.data.pageCount - 1 );
    }

    private AdvancedGridLayout GetGrid( int page )
    {
        return page == currentPage ? cardsDisplayGridRight : cardsDisplayGridLeft;
    }

    private void SetupGrid( int page )
    {
        var grid = GetGrid( page );

        // TODO: Resetup grid X/Y
        if( width != currentbinder.data.pageWidth || height != currentbinder.data.pageHeight )
        {

        }
        else
        {
            // Reset/load images based on the stored data
            foreach( var (pos, card ) in Utility.Enumerate( currentbinder.data.cardList[page] ) )
            {
                var texture = defaultCardImage;
                
                if( card != null )
                {
                    if( card.largeImage != null )
                    {
                        texture = card.largeImage;
                    }
                    else if( card.smallImages != null && card.imageIndex < card.smallImages.Length )
                    {
                        texture = card.smallImages[card.imageIndex];
                    }
                    else if( !card.largeImageRequested )
                    {
                        card.largeImageRequested = true;
                        var cardPos = pos;
                        var cardPage = currentPage;

                        // Load card images if not loaded yet (happens when we load a binder for the first time)
                        // TODO Handle art variations
                        StartCoroutine( APICallHandler.Instance.DownloadImage( card.cardAPIData.card_images[card.imageIndex].image_url, true, ( texture ) =>
                        {
                            // TODO: Save/cache image
                            card.largeImage = texture;
                            card.largeImageRequested = false;

                            if( cardPage == currentPage )
                                grid.transform.GetChild( cardPos ).GetComponent<Image>().sprite = Utility.CreateSprite( texture );
                        } ) );
                    }
                }

                grid.transform.GetChild( pos ).GetComponent<Image>().sprite = Utility.CreateSprite( texture );
            }

            // Setup buttons
            for( int i = 0; i < grid.transform.childCount; ++i )
            {
                int pos = i;
                var dispatcher = grid.transform.GetChild( i ).GetComponent<EventDispatcher>();
                dispatcher.OnDoubleClickEvent = ( e ) => UIUtility.LeftMouseFilter( false, e, () => OpenSearchPanel( page, pos ) );
                dispatcher.OnBeginDragEvent = ( e ) => UIUtility.LeftMouseFilter( false, e, () => StartDragging( page, pos ) );
            }
        }
    }

    private void StartDragging( int page, int pos )
    {
        // Can't move empty cards
        if( currentbinder.data.cardList[page][pos] == null )
        if( currentbinder.data.cardList[page][pos] == null )
            return;

        var grid = GetGrid( page );
        var cardToCopy = grid.transform.GetChild( pos );
        dragCardIdx = GetIndexFromPageAndPos( page, pos );

        dragOffset = cardToCopy.transform.position.ToVector2() - Utility.GetMouseOrTouchPos();

        dragging = Instantiate( dragCardGhostPrefab, cardsPage.transform.parent );
        ( dragging.transform as RectTransform ).anchoredPosition = Utility.GetMouseOrTouchPos() + dragOffset;
        var texture = cardToCopy.GetComponent<Image>().mainTexture as Texture2D;
        dragging.GetComponent<Image>().sprite = Utility.CreateSprite( texture );
        grid.transform.GetChild( pos ).GetComponent<Image>().sprite = Utility.CreateSprite( defaultCardImage );
        var worldRect = ( cardToCopy.transform as RectTransform ).GetWorldRect();
        ( dragging.transform as RectTransform ).sizeDelta = new Vector2( worldRect.width, worldRect.height );

        clearCardDropLocation.SetActive( true );
    }

    private void StopDragging()
    {
        if( dragging == null )
            return;

        var (page, pos) = GetPageAndPosFromIndex( dragCardIdx );
        var otherpageIdx = GetOtherPageIndex( page );
        var grid = GetGrid( page );
        var otherGrid = grid == cardsDisplayGridLeft ? cardsDisplayGridRight : cardsDisplayGridLeft;

        dragging.Destroy();
        clearCardDropLocation.SetActive( false );

        for( int i = 0; i < currentbinder.data.pageWidth * currentbinder.data.pageHeight; ++i )
        {
            if( grid.isActiveAndEnabled &&
                StopDraggingCollisionCheck( grid.transform.GetChild( i ).gameObject, page, pos, page, i ) )
                return;

            if( otherGrid.isActiveAndEnabled &&
                StopDraggingCollisionCheck( otherGrid.transform.GetChild( i ).gameObject, page, pos, otherpageIdx, i ) )
                return;
        }

        var worldRect = ( clearCardDropLocation.transform as RectTransform ).GetWorldRect();
        if( worldRect.Contains( ( dragging.transform as RectTransform ).GetWorldRect().center ) )
        {
            currentModifyCardIdx = dragCardIdx;
            LoadCard( new CardSelectedEvent() { card = null } );
            return;
        }

        // If not clicked on anything, revert texture back to what it was at the start of the drag (and destroy the dragging obj)
        var originTexture = dragging.GetComponent<Image>().mainTexture as Texture2D;
        grid.transform.GetChild( pos ).GetComponent<Image>().sprite = Utility.CreateSprite( originTexture );
    }

    private void Update()
    {
        if( dragging != null )
        {
            ( dragging.transform as RectTransform ).anchoredPosition = Utility.GetMouseOrTouchPos() + dragOffset;
            if( Utility.IsMouseUpOrTouchEnd() )
                StopDragging();
        }
    }

    private int GetOtherPageIndex( int page )
    {
        return page - 1 + Utility.Mod( page, 2 ) * 2;
    }

    private int GetIndexFromPageAndPos( int page, int pos )
    { 
        // Add pageSize to the idx to indicate we are modifying the right page (or don't if left page)
        return Utility.Mod( page + 1, 2 ) * currentbinder.data.pageWidth * currentbinder.data.pageHeight + pos;
    }

    private Pair<int, int> GetPageAndPosFromIndex( int idx )
    {
        return new Pair<int, int>(
            idx >= ( currentbinder.data.pageWidth * currentbinder.data.pageHeight ) ? currentPage : currentPage - 1,
            Utility.Mod( idx, currentbinder.data.pageWidth * currentbinder.data.pageHeight )
        );
    }

    private bool StopDraggingCollisionCheck( GameObject card, int pageFrom, int cardFrom, int pageTo, int cardTo )
    {
        if( pageFrom == pageTo && cardFrom == cardTo )
            return false;

        var worldRect = ( card.transform as RectTransform ).GetWorldRect();

        // Collision check against other cards (centre within card bounds)
        // Swap cards (need to handle swap between page)
        if( worldRect.Contains( ( dragging.transform as RectTransform ).GetWorldRect().center ) )
        {
            var originTexture = dragging.GetComponent<Image>().mainTexture as Texture2D;
            var texture = card.GetComponent<Image>().mainTexture as Texture2D;
            GetGrid( pageFrom ).transform.GetChild( cardFrom ).GetComponent<Image>().sprite = Utility.CreateSprite( texture );
            card.GetComponent<Image>().sprite = Utility.CreateSprite( originTexture );

            (currentbinder.data.cardList[pageFrom][cardFrom], currentbinder.data.cardList[pageTo][cardTo]) = 
                (currentbinder.data.cardList[pageTo][cardTo], currentbinder.data.cardList[pageFrom][cardFrom]);
            return true;
        }

        return false;
    }

    private bool SearchStopDraggingCollisionCheck( int page, int pos )
    {
        var grid = GetGrid( page );
        var card = grid.transform.GetChild( pos ).gameObject;
        var rect = ( card.transform as RectTransform ).GetSceenSpaceRect();
        return grid.isActiveAndEnabled && rect.Contains( Utility.GetMouseOrTouchPos() );
    }

    private void ChangePage( int page )
    {
        // Deliberately cap at currentbinder.pageCount (not currentbinder.pageCount - 1), because we display pages in multiples of 2
        // If we are on the last page the index will be currentbinder.pageCount but the right side won't be visible/setup
        currentPage = Mathf.Clamp( page, 0, currentbinder.data.pageCount );
        currentPageTextLeft.text = page == 0 ? string.Empty : string.Format( "Page: {0}", currentPage );
        currentPageTextRight.text = page >= currentbinder.data.pageCount ? string.Empty : string.Format( "Page: {0}", currentPage + 1 );
        PopulateGrid();
    }

    public void NextPage()
    {
        ChangePage( currentPage + 2 );
    }

    public void PrevPage()
    {
        ChangePage( currentPage - 2 );
    }

    public void OpenSearchPanelGeneric()
    {
        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent()
        {
            page = openFullScreenSearch ? PageType.SearchPageFull : PageType.SearchPage,
            behaviour = SearchPageOrigin.CardPageSearch,
            flags = ( FindNextEmptyCardSlot() == null ? SearchPageFlags.PageFull : 0 ),
        } );
    }

    public void OpenSearchPanel( int page, int pos )
    {
        currentModifyCardIdx = GetIndexFromPageAndPos( page, pos );

        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent()
        {
            page = openFullScreenSearch ? PageType.SearchPageFull : PageType.SearchPage,
            behaviour = SearchPageOrigin.CardPageSearch,
            flags = ( currentbinder.data.cardList[page][pos] == null
                ? SearchPageFlags.SettingCards
                : SearchPageFlags.ReplacingCard )
                    | ( FindNextEmptyCardSlot() == null ? SearchPageFlags.PageFull : 0 ),
            replacingCard = currentbinder.data.cardList[page][pos] != null
                ? currentbinder.data.cardList[page][pos].name
                : String.Empty
        } );
    }

    public void OpenInventory()
    {
        EventSystem.Instance.TriggerEvent( new OpenInventoryPageEvent()
        {
            behaviour = SearchPageOrigin.CardPageInventory,
            currentBinderIdx = BinderPage.Instance.BinderData.IndexOf(currentbinder),
            flags = ( FindNextEmptyCardSlot() == null ? SearchPageFlags.PageFull : 0 ),
        } );
    }

    public void SwapPage( int from, int to )
    {
        if( from == to )
            return;

        currentbinder.data.cardList.Swap( from, to );
        PopulateGrid();
        EventSystem.Instance.TriggerEvent( new SaveGameEvent() { } );
    }

    public void AddPage( bool left, int count = 1 )
    {
        for( int i = 0; i < count; ++i )
            currentbinder.data.Insert( left ? currentPage - 1 : currentPage );

        UpdateHeaderInfo();
        PopulateGrid();
        EventSystem.Instance.TriggerEvent( new SaveGameEvent() { } );
    }

    public void RemovePage( bool left )
    {
        currentbinder.data.Remove( left ? currentPage - 1 : currentPage );
        UpdateHeaderInfo();
        PopulateGrid();
        EventSystem.Instance.TriggerEvent( new SaveGameEvent() { } );
    }

    public void SwapPage( bool left, int withIndex )
    {
        currentbinder.data.Swap( left ? currentPage - 1 : currentPage, withIndex );
        PopulateGrid();
        EventSystem.Instance.TriggerEvent( new SaveGameEvent() { } );
    }

    public void MovePage( bool left, int toIndex )
    {
        currentbinder.data.Move( left ? currentPage - 1 : currentPage, toIndex );
        PopulateGrid();
        EventSystem.Instance.TriggerEvent( new SaveGameEvent() { } );
    }
}