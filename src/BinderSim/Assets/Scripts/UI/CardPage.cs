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
    [SerializeField] Color clearCardAreaImageHighlightColour;
    [SerializeField] BinderModelHandler binderModelHandler;

    private BinderDataRuntime currentbinder;
    private int width;
    private int height;
    private int currentPage = -1;
    private int? currentModifyCardIdx;
    private bool openFullScreenSearch;
    private Image clearCardAreaImage;
    private Color clearCardAreaImageDefaultColour;

    private bool pageTurning;

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
        lastPageButton.onClick.AddListener( () => ChangePage( currentbinder.data.pageCount - ( currentbinder.data.pageCount & 1 ) ) );

        width = Constants.Instance.DefaultStartingPageWidth;
        height = Constants.Instance.DefaultStartingPageHeight;

        clearCardDropLocation.SetActive( false );
        clearCardAreaImage = clearCardDropLocation.GetComponentInChildren<Image>();
        clearCardAreaImageDefaultColour = clearCardAreaImage.color;

        binderModelHandler.onBookStateChanged += (from, to, page) =>
        {
            pageTurning = false;
            UpdateButtons();
        };

        DebugScreen.AddDebugEntry( () => String.Format( "pageTurning: {0}", pageTurning ) );
        DebugScreen.AddDebugEntry( () => String.Format( "currentPage: {0}", currentPage ) );
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
                    Hide();
                    break;
                case PageType.CardPage:
                    Show( pageChangeRequest );
                    break;
            }
        }
        else if( e is CardSelectedEvent cardSelectedEvent )
        {
            LoadCard( cardSelectedEvent );
        }
        else if( e is CardRemovedEvent cardRemovedEvent )
        {
            RemoveCardFromBinder( cardRemovedEvent.card, cardRemovedEvent.fromInventory );
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
        else if( e is BinderChangeCardPageRequest changePage )
        {
            if( changePage.nextPage )
                NextPage();
            else
                PrevPage();
        }
        else if( e is StartDraggingEvent startDragging )
        {
            StartDragging( startDragging );
        }
        else if( e is CardDoubleClickEvent doubleClick )
        {
            OpenSearchPanel( doubleClick.page, doubleClick.pos );
        }
    }

    private void Show( PageChangeRequestEvent pageChangeRequest )
    {
        cardsPage.SetActive( true );

        if( pageChangeRequest is CloseSearchPageEvent cancelRequest && cancelRequest.fromFullscreen != null )
            openFullScreenSearch = cancelRequest.fromFullscreen.Value;

        if( pageChangeRequest is OpenCardPageEvent openPageRequest )
            LoadBinder( openPageRequest.binder );
    }

    private void Hide()
    {
        cardsPage.SetActive( false );
    }

    private void LoadBinder( BinderDataRuntime binder )
    {
        currentbinder = binder;
        UpdateHeaderInfo();
        ChangePage( -1 );
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
            RemoveCardFromBinder( data, e.fromInventory );

        var (page, pos) = GetPageAndPosFromIndex( currentModifyCardIdx.Value );
        var grid = GetGrid( page );
        var image = grid.transform.GetChild( pos ).GetComponent<Image>();

        image.sprite = Utility.CreateSprite( data == null ? defaultCardImage : data.smallImages[data.imageIndex] );
        currentbinder.data.cardList[page][pos] = data;
        UpdateHeaderInfo();
        currentModifyCardIdx = null;

        if( data != null )
        {
            if( !e.fromDragDrop && FindNextEmptyCardSlot() == null )
                EventSystem.Instance.TriggerEvent( new PageFullEvent() );

            data.insideBinderIdx = BinderPage.Instance.BinderData.IndexOf( currentbinder );
            BinderPage.Instance.SortInventory();
        }

        EventSystem.Instance.TriggerEvent( new BinderDataUpdateEvent() { binder = currentbinder.data } );
    }

    private void RemoveCardFromBinder( CardDataRuntime data, bool fromInventory )
    {
        // Add to inventory if a new card
        if( data.insideBinderIdx == null && !fromInventory )
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

            if( foundIdx == -1 )
            {
                Debug.LogError( "Failed to find card idx when removing card from binder (but the card has insideBinderIdx set to this binder)" );
                return;
            }

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

                // Set previous card to not being inside a binder
                data.insideBinderIdx = null;
            }
        }

        BinderPage.Instance.SortInventory();
    }

    private void PopulateGrid()
    {
        // Setup page (and secondary/adjacent page)
        if( currentPage > 0 && currentPage < currentbinder.data.pageCount )
            SetupGrid( currentPage - 1 );

        if( currentPage >= 0 && currentPage < currentbinder.data.pageCount - 1 )
            SetupGrid( currentPage );

        UpdateButtons();

        EventSystem.Instance.QueueEvent( new BinderPopulateGrid() { currentPage = currentPage } );
    }

    private void UpdateButtons()
    {
        // Show/hide next/prev buttons depending on first/last page
        prevPageButton.gameObject.SetActive( currentPage >= 0 && !pageTurning );
        var prevTooltip = prevPageButton.gameObject.transform.GetChild( 0 ).gameObject;
        if( prevTooltip.activeSelf )
            prevTooltip.SetActive( prevPageButton.gameObject.activeSelf );

        nextPageButton.gameObject.SetActive( currentPage <= currentbinder.data.pageCount && !pageTurning );
        var nextTooltip = nextPageButton.gameObject.transform.GetChild( 0 ).gameObject;
        if( nextTooltip.activeSelf )
            nextTooltip.SetActive( nextPageButton.gameObject.activeSelf );

        // Show/hide first/last buttons depending on first/last page
        firstPageButton.gameObject.SetActive( currentPage > 0 && !pageTurning );
        lastPageButton.gameObject.SetActive( currentPage < currentbinder.data.pageCount - ( currentbinder.data.pageCount & 1 ) && !pageTurning );

        // Show/hide modify buttons depending on first/last page
        modifyPageButtonsLeft.SetActive( currentPage > 0 && currentPage < currentbinder.data.pageCount && !pageTurning );
        modifyPageButtonsRight.SetActive( currentPage >= 0 && currentPage < currentbinder.data.pageCount - 1 && !pageTurning );
    }

    private bool IsLeftPage( int page )
    {
        return page % 2 == 1;
    }

    private AdvancedGridLayout GetGrid( int page )
    {
        return IsLeftPage( page ) ? cardsDisplayGridLeft : cardsDisplayGridRight;
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
                var validCard = card != null && card.cardAPIData != null;

                if( validCard )
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

                var child = grid.transform.GetChild( pos );
                var imageComponent = child.GetComponent<Image>();
                imageComponent.material = AppUtility.GetCardMaterialFromRarity( card );
                imageComponent.sprite = Utility.CreateSprite( texture );
            }
        }
    }

    private void StartDragging( StartDraggingEvent startDraggingEvent )
    {
        var page = startDraggingEvent.page;
        var pos = startDraggingEvent.pos;

        // Can't move empty cards
        if( currentbinder.data.cardList[page][pos] == null )
            return;

        if( startDraggingEvent.doubleClick )
        {
            OpenSearchPanel( page, pos );
            return;
        }
        
        var grid = GetGrid( page );
        var cardToCopy = grid.transform.GetChild( pos );
        dragCardIdx = GetIndexFromPageAndPos( page, pos );

        dragOffset = startDraggingEvent.colliderBoundsScreen.center - Utility.GetMouseOrTouchPos();

        dragging = Instantiate( dragCardGhostPrefab, cardsPage.transform.parent );
        ( dragging.transform as RectTransform ).anchoredPosition = Utility.GetMouseOrTouchPos() + dragOffset;
        var texture = cardToCopy.GetComponent<Image>().mainTexture as Texture2D;
        dragging.GetComponent<Image>().sprite = Utility.CreateSprite( texture );
        grid.transform.GetChild( pos ).GetComponent<Image>().sprite = Utility.CreateSprite( defaultCardImage );
        ( dragging.transform as RectTransform ).sizeDelta = startDraggingEvent.colliderBoundsScreen.size;

        clearCardDropLocation.SetActive( true );
    }

    private Rect GetCardScreenSpaceRect( GameObject gridCard, bool leftPage )
    {
        return binderModelHandler.GetCardScreenSpaceRect( gridCard, leftPage );
    }

    private void StopDragging()
    {
        if( dragging == null )
            return;

        var (page, pos) = GetPageAndPosFromIndex( dragCardIdx );
        var otherPageIdx = GetOtherPageIndex( page );
        var grid = GetGrid( page );
        var otherGrid = grid == cardsDisplayGridLeft ? cardsDisplayGridRight : cardsDisplayGridLeft;

        dragging.Destroy();
        clearCardDropLocation.SetActive( false );

        for( int i = 0; i < currentbinder.data.pageWidth * currentbinder.data.pageHeight; ++i )
        {
            if( grid.IsActive() && grid.transform.childCount >= i && page >= 0 && page < currentbinder.data.cardList.Count )
                if( StopDraggingCollisionCheck( grid.transform.GetChild( i ).gameObject, page, pos, page, i ) )
                    return;

            if( otherGrid.IsActive() && otherGrid.transform.childCount >= i && otherPageIdx >= 0 && otherPageIdx < currentbinder.data.cardList.Count )
                if( StopDraggingCollisionCheck( otherGrid.transform.GetChild( i ).gameObject, page, pos, otherPageIdx, i ) )
                    return;
        }

        var worldRect = ( clearCardDropLocation.transform as RectTransform ).GetWorldRect();
        if( worldRect.Contains( ( dragging.transform as RectTransform ).GetWorldRect().center ) )
        {
            RemoveCardFromBinder( currentbinder.data.cardList[page][pos], true );
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

    public void ClearCardAreaImagePointerEnter()
    {
        clearCardAreaImage.color = clearCardAreaImageHighlightColour;
        var image = dragging.GetComponent<Image>();
        image.color = image.color.SetA( 0.5f );
    }

    public void ClearCardAreaImagePointerExit()
    {
        clearCardAreaImage.color = clearCardAreaImageDefaultColour;
        var image = dragging.GetComponent<Image>();
        image.color = image.color.SetA( 1.0f );
    }

    private int GetOtherPageIndex( int page )
    {
        return page - 1 + Utility.Mod( page, 2 ) * 2;
    }

    private int GetIndexFromPageAndPos( int page, int pos )
    { 
        // Add pageSize to the idx to indicate we are modifying the right page (or don't if left page)
        return page * currentbinder.data.pageWidth * currentbinder.data.pageHeight + pos;
    }

    private Pair<int, int> GetPageAndPosFromIndex( int idx )
    {
        return new Pair<int, int>(
            idx / ( currentbinder.data.pageWidth * currentbinder.data.pageHeight ),
            Utility.Mod( idx, currentbinder.data.pageWidth * currentbinder.data.pageHeight )
        );
    }

    private bool StopDraggingCollisionCheck( GameObject card, int pageFrom, int cardFrom, int pageTo, int cardTo )
    {
        if( pageFrom == pageTo && cardFrom == cardTo )
            return false;

        var cardRect = GetCardScreenSpaceRect( card, IsLeftPage( pageTo ) );
        var draggingRect = ( dragging.transform as RectTransform ).GetWorldRect();

        // Collision check against other cards (centre within card bounds)
        // Swap cards (need to handle swap between page)
        if( cardRect.Contains( draggingRect.center ) )
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
        var rect = GetCardScreenSpaceRect( card, IsLeftPage( page ) );
        //var rect = ( card.transform as RectTransform ).GetSceenSpaceRect();
        return grid.isActiveAndEnabled && rect.Contains( Utility.GetMouseOrTouchPos() );
    }

    private void ChangePage( int page )
    {
        // Deliberately cap at currentbinder.pageCount (not currentbinder.pageCount - 1), because we display pages in multiples of 2
        // If we are on the last page the index will be currentbinder.pageCount but the right side won't be visible/setup
        currentPage = page;// Mathf.Clamp( page, 0, currentbinder.data.pageCount );
        currentPageTextLeft.text = page == 0 ? string.Empty : string.Format( "Page: {0}", currentPage );
        currentPageTextRight.text = page >= currentbinder.data.pageCount ? string.Empty : string.Format( "Page: {0}", currentPage + 1 );
        pageTurning = true;
        PopulateGrid();

        EventSystem.Instance.TriggerEvent( new BinderChangeCardPage()
        {
            newPage = page
        } );
    }

    public void NextPage()
    {
        var page = currentPage == -1 ? 0 : Mathf.Min( currentPage + 2, currentbinder.data.pageCount + 2 );
        ChangePage( page );
    }

    public void PrevPage()
    {
        var page = Mathf.Max( -1, currentPage - 2 );
        ChangePage( page );
    }

    public void OpenSearchPanelGeneric()
    {
        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent()
        {
            page = openFullScreenSearch ? PageType.SearchPageFull : PageType.SearchPage,
            behaviour = SearchPageOrigin.CardPageSearch,
            flags = ( FindNextEmptyCardSlot() == null ? SearchPageFlags.PageFull : 0 ),
            currentBinderIdx = BinderPage.Instance.BinderData.IndexOf( currentbinder ),
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
                : String.Empty,
            currentBinderIdx = BinderPage.Instance.BinderData.IndexOf( currentbinder ),
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
        EventSystem.Instance.TriggerEvent( new BinderDataUpdateEvent() { binder = currentbinder.data } );
        PopulateGrid();
    }

    public void AddPage( bool left, int count = 1 )
    {
        for( int i = 0; i < count; ++i )
            currentbinder.data.Insert( left ? currentPage - 1 : currentPage );

        EventSystem.Instance.TriggerEvent( new BinderDataUpdateEvent() { binder = currentbinder.data } );
        UpdateHeaderInfo();
        PopulateGrid();
    }

    public void RemovePage( bool left )
    {
        currentbinder.data.Remove( left ? currentPage - 1 : currentPage );
        UpdateHeaderInfo();
        EventSystem.Instance.TriggerEvent( new BinderDataUpdateEvent() { binder = currentbinder.data } );
        if( currentPage >= currentbinder.data.pageCount )
            PrevPage();
        else
            PopulateGrid();
    }

    public void SwapPage( bool left, int withIndex )
    {
        currentbinder.data.Swap( left ? currentPage - 1 : currentPage, withIndex );
        EventSystem.Instance.TriggerEvent( new BinderDataUpdateEvent() { binder = currentbinder.data } );
        PopulateGrid();
    }

    public void MovePage( bool left, int toIndex )
    {
        currentbinder.data.Move( left ? currentPage - 1 : currentPage, toIndex );
        EventSystem.Instance.TriggerEvent( new BinderDataUpdateEvent() { binder = currentbinder.data } );
        PopulateGrid();
    }
}