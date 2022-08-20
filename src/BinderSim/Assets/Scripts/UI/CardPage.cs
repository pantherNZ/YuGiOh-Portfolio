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
    [SerializeField] TMPro.TMP_InputField binderNameText = null;
    [SerializeField] TMPro.TMP_InputField pageCountText = null;
    [SerializeField] TMPro.TMP_Dropdown pageSizeDropDown = null;
    [SerializeField] TMPro.TextMeshProUGUI dateCreatedText = null;
    [SerializeField] Texture2D defaultCardImage = null;
    [SerializeField] Button prevPageButton = null;
    [SerializeField] Button nextPageButton = null;
    [SerializeField] Button firstPageButton = null;
    [SerializeField] Button lastPageButton = null;
    [SerializeField] TMPro.TextMeshProUGUI currentPageTextLeft = null;
    [SerializeField] TMPro.TextMeshProUGUI currentPageTextRight = null;
    [SerializeField] GameObject CardGridEntryPrefab = null;
    [SerializeField] Button applyChangesButton = null;
    [SerializeField] GameObject modifyPageButtonsLeft = null;
    [SerializeField] GameObject modifyPageButtonsRight = null;
    [SerializeField] GameObject clearCardDropLocation = null;

    private BinderData currentbinder;
    private int width;
    private int height;
    private int currentPage;
    private int? currentModifyCardIdx;

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
        lastPageButton.onClick.AddListener( () => ChangePage( currentbinder.pageCount ) );

        binderNameText.onValueChanged.AddListener( _ => OnBinderHeaderChanged() );
        pageCountText.onValueChanged.AddListener( _ => OnBinderHeaderChanged() );
        pageSizeDropDown.onValueChanged.AddListener( _ => OnBinderHeaderChanged() );

        width = Constants.Instance.DefaultStartingPageWidth;
        height = Constants.Instance.DefaultStartingPageHeight;

        clearCardDropLocation.SetActive( false );
    }

    public void SaveAndExit()
    {
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
                    cardsPage.SetActive( true );
                    if( pageChangeRequest.binder != null )
                        LoadBinder( pageChangeRequest.binder );
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

            if( currentPage < currentbinder.pageCount )
            {
                var foundCard = currentbinder.cardList[currentPage].FindIndex( x => x == cardImageLoadedEvent.card );
                if( foundCard != -1 )
                {
                    var cardUIEntry = cardsDisplayGridRight.transform.GetChild( foundCard );
                    cardUIEntry.GetComponent<Image>().sprite = Utility.CreateSprite( cardImageLoadedEvent.card.largeImage );
                }

            }
            
            if( currentPage > 0 )
            {
                var foundCard = currentbinder.cardList[currentPage - 1].FindIndex( x => x == cardImageLoadedEvent.card );
                if( foundCard != -1 )
                {
                    var cardUIEntry = cardsDisplayGridLeft.transform.GetChild( foundCard );
                    cardUIEntry.GetComponent<Image>().sprite = Utility.CreateSprite( cardImageLoadedEvent.card.largeImage );
                }
            }
        }
    }

    private void LoadBinder( BinderData data )
    {
        currentbinder = data;
        binderNameText.text = currentbinder.name;
        pageCountText.text = data.pageCount.ToString();
        var pageSizeStr = GetCurrentPageSizeString();
        pageSizeDropDown.value = pageSizeDropDown.options.FindIndex( x => x.text == pageSizeStr );
        dateCreatedText.text = data.dateCreated.ToShortDateString();
        ChangePage( 0 );
    }

    private void OnBinderHeaderChanged()
    {
        applyChangesButton.interactable = binderNameText.text != currentbinder.name
            || pageCountText.text != currentbinder.pageCount.ToString()
            || pageSizeDropDown.options[pageSizeDropDown.value].text != GetCurrentPageSizeString();
    }

    private void TryApplyChanges()
    {
        // Error checks
        if( binderNameText.text.Length == 0
            || !int.TryParse( pageCountText.text, out _ ) )
            return;

        // TODO: Show confirmation box for size or num pages change
        if( binderNameText.text == currentbinder.name )
        {

            return;
        }

        ApplyChanges();
    }

    public void ApplyChanges()
    {
        currentbinder.name = binderNameText.text;
        var pageSize = pageSizeDropDown.options[pageSizeDropDown.value].text;
        currentbinder.Resize(
            int.Parse( pageCountText.text ),
            int.Parse( pageSize[0].ToString() ),
            int.Parse( pageSize[2].ToString() ) );

        EventSystem.Instance.TriggerEvent( new BinderDataUpdateEvent() { binder = currentbinder } );
        PopulateGrid();
    }

    private string GetCurrentPageSizeString()
    {
        return string.Format( "{0}x{1}", currentbinder.pageWidth, currentbinder.pageHeight );
    }

    private int? FindNextEmptyCardSlot()
    {
        for( int pageIdx = Mathf.Max( 0, currentPage - 1 ); pageIdx <= currentPage && pageIdx < currentbinder.cardList.Count; ++pageIdx )
        {
            for( int cardIdx = 0; cardIdx < currentbinder.pageWidth * currentbinder.pageHeight; ++cardIdx )
            {
                if( currentbinder.cardList[pageIdx][cardIdx] == null )
                    return Utility.Mod( pageIdx + 1, 2 ) * currentbinder.pageWidth * currentbinder.pageHeight + cardIdx;
            }
        }

        return null;
    }

    private void LoadCard( CardSelectedEvent e )
    {
        // From drag and drop - calculate mouse pos to determine which card to replace
        if( e.fromDragDrop )
        {
            currentModifyCardIdx = null;

            for( int i = 0; i < currentbinder.pageWidth * currentbinder.pageHeight; ++i )
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

        var (page, pos) = GetPageAndPosFromIndex( currentModifyCardIdx.Value );
        var grid = GetGrid( page );
        var image = grid.transform.GetChild( pos ).GetComponent<Image>();
        var data = e.card;
        image.sprite = Utility.CreateSprite( data == null ? defaultCardImage : data.smallImage );
        currentbinder.cardList[page][pos] = data;
        currentModifyCardIdx = null;

        if( data != null && !e.fromDragDrop && FindNextEmptyCardSlot() == null )
            EventSystem.Instance.TriggerEvent( new PageFullEvent() );
    }

    private void PopulateGrid()
    {
        // Setup page (and secondary/adjacent page)
        if( currentPage > 0 )
            SetupGrid( currentPage - 1 );

        if( currentPage < currentbinder.pageCount - 1 )
            SetupGrid( currentPage );

        // Show/hide page depending on first/last page
        cardsDisplayGridLeft.gameObject.SetActive( currentPage > 0 );
        cardsDisplayGridRight.gameObject.SetActive( currentPage < currentbinder.pageCount - 1 );

        // Show/hide next/prev buttons depending on first/last page
        prevPageButton.gameObject.SetActive( currentPage > 0 );
        nextPageButton.gameObject.SetActive( currentPage < currentbinder.pageCount - 1 );

        // Show/hide first/last buttons depending on first/last page
        firstPageButton.gameObject.SetActive( currentPage > 0 );
        lastPageButton.gameObject.SetActive( currentPage < currentbinder.pageCount - 1 );

        // Show/hide modify buttons depending on first/last page
        modifyPageButtonsLeft.SetActive( currentPage > 0 );
        modifyPageButtonsRight.SetActive( currentPage < currentbinder.pageCount - 1 );

        EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent() { page = PageType.CardPage } );
    }

    private AdvancedGridLayout GetGrid( int page )
    {
        return page == currentPage ? cardsDisplayGridRight : cardsDisplayGridLeft;
    }

    private void SetupGrid( int page )
    {
        var grid = GetGrid( page );

        // TODO: Resetup grid X/Y
        if( width != currentbinder.pageWidth || height != currentbinder.pageHeight )
        {

        }
        else
        {
            // Reset/load images based on the stored data
            foreach( var (pos, card ) in Utility.Enumerate( currentbinder.cardList[page] ) )
            {
                var texture = defaultCardImage;
                
                if( card != null )
                {
                    if( card.largeImage != null )
                    {
                        texture = card.largeImage;
                    }
                    else if( card.smallImage != null )
                    {
                        texture = card.smallImage;
                    }
                    else if( !card.largeImageRequsted )
                    {
                        card.largeImageRequsted = true;
                        var cardPos = pos;
                        var cardPage = currentPage;

                        // Load card images if not loaded yet (happens when we load a binder for the first time)
                        StartCoroutine( APICallHandler.Instance.DownloadImage( card.cardAPIData.card_images[0].image_url, true, ( texture ) =>
                        {
                            // TODO: Save/cache image
                            card.largeImage = texture;
                            card.largeImageRequsted = false;

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
                dispatcher.OnDoubleClickEvent = ( e ) => LeftMouseFilter( e, () => OpenSearchPanel( page, pos ) );
                dispatcher.OnBeginDragEvent = ( e ) => LeftMouseFilter( e, () => StartDragging( page, pos ) );
            }
        }
    }

    void LeftMouseFilter( PointerEventData e, Action func )
    {
        if( e.button == PointerEventData.InputButton.Left )
            func();
    }

    private void StartDragging( int page, int pos )
    {
        // Can't move empty cards
        if( currentbinder.cardList[page][pos] == null )
            return;

        var grid = GetGrid( page );
        var cardToCopy = grid.transform.GetChild( pos );
        dragCardIdx = GetIndexFromPageAndPos( page, pos );

        dragOffset = cardToCopy.transform.position.ToVector2() - Utility.GetMouseOrTouchPos();

        dragging = Instantiate( CardGridEntryPrefab, cardsPage.transform.parent );
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

        for( int i = 0; i < currentbinder.pageWidth * currentbinder.pageHeight; ++i )
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
        return Utility.Mod( page + 1, 2 ) * currentbinder.pageWidth * currentbinder.pageHeight + pos;
    }

    private Pair<int, int> GetPageAndPosFromIndex( int idx )
    {
        return new Pair<int, int>(
            idx >= ( currentbinder.pageWidth * currentbinder.pageHeight ) ? currentPage : currentPage - 1,
            Utility.Mod( idx, currentbinder.pageWidth * currentbinder.pageHeight )
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

            (currentbinder.cardList[pageFrom][cardFrom], currentbinder.cardList[pageTo][cardTo]) = 
                (currentbinder.cardList[pageTo][cardTo], currentbinder.cardList[pageFrom][cardFrom]);
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
        currentPage = Mathf.Clamp( page, 0, currentbinder.pageCount );
        currentPageTextLeft.text = page == 0 ? string.Empty : string.Format( "Page: {0}", currentPage );
        currentPageTextRight.text = page >= currentbinder.pageCount ? string.Empty : string.Format( "Page: {0}", currentPage + 1 );
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

    public void SwapPage( int from, int to )
    {
        if( from == to )
            return;

        currentbinder.cardList.Swap( from, to );
        PopulateGrid();
    }

    public void OpenSearchPanelGeneric()
    {
        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent()
        {
            openFullPage = false,
            behaviour = FindNextEmptyCardSlot() == null 
                ? SearchPageBehaviour.AddingCardsPageFull 
                : SearchPageBehaviour.AddingCards
        } );
    }

    public void OpenSearchPanel( int page, int pos )
    {
        currentModifyCardIdx = GetIndexFromPageAndPos( page, pos );

        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent() 
        { 
            openFullPage = false,
            behaviour = currentbinder.cardList[page][pos] == null 
                ? SearchPageBehaviour.SettingCard
                : SearchPageBehaviour.ReplacingCard
        } );
    }

    public void AddPage( bool left, int count = 1 )
    {
        for( int i = 0; i < count; ++i )
            currentbinder.Insert( left ? currentPage - 1 : currentPage );
        pageCountText.text = currentbinder.pageCount.ToString();
        PopulateGrid();
    }

    public void RemovePage( bool left )
    {
        currentbinder.Remove( left ? currentPage - 1 : currentPage );
        pageCountText.text = currentbinder.pageCount.ToString();
        PopulateGrid();
    }

    public void SwapPage( bool left, int withIndex )
    {
        currentbinder.Swap( left ? currentPage - 1 : currentPage, withIndex );
        PopulateGrid();
    }

    public void MovePage( bool left, int toIndex )
    {
        currentbinder.Move( left ? currentPage - 1 : currentPage, toIndex );
        PopulateGrid();
    }
}