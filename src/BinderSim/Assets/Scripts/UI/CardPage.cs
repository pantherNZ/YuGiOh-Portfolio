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
    [SerializeField] TMPro.TextMeshProUGUI currentPageTextLeft = null;
    [SerializeField] TMPro.TextMeshProUGUI currentPageTextRight = null;
    [SerializeField] GameObject CardGridEntryPrefab = null;
    [SerializeField] Button applyChangesButton = null;
    [SerializeField] GameObject modifyPageButtonsLeft = null;
    [SerializeField] GameObject modifyPageButtonsRight = null;

    private BinderData currentbinder;
    private int width = Constants.DefaultStartingPageWidth;
    private int height = Constants.DefaultStartingPageHeight;
    private int currentPage;
    private int? currentModifyCardIdx;

    private Camera mainCamera;
    private GameObject dragging;
    private Vector2 dragOffset;

    protected override void Start()
    {
        base.Start();
        cardsPage.SetActive( false );

        prevPageButton.onClick.AddListener( PrevPage );
        nextPageButton.onClick.AddListener( NextPage );
        applyChangesButton.onClick.AddListener( TryApplyChanges );

        mainCamera = Camera.main;

        binderNameText.onValueChanged.AddListener( _ => OnBinderHeaderChanged() );
        pageCountText.onValueChanged.AddListener( _ => OnBinderHeaderChanged() );
        pageSizeDropDown.onValueChanged.AddListener( _ => OnBinderHeaderChanged() );
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
            LoadCard( cardSelectedEvent.card );
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
        Save();
    }

    private string GetCurrentPageSizeString()
    {
        return string.Format( "{0}x{1}", currentbinder.pageWidth, currentbinder.pageHeight );
    }

    private int? FindNextEmptyCardSlot()
    {
        for( int pageIdx = currentPage - 1; pageIdx <= currentPage; ++pageIdx )
        {
            for( int cardIdx = 0; cardIdx < currentbinder.pageWidth * currentbinder.pageHeight; ++cardIdx )
            {
                if( currentbinder.cardList[pageIdx][cardIdx] == null )
                    return Utility.Mod( pageIdx + 1, 2 ) * currentbinder.pageWidth * currentbinder.pageHeight + cardIdx;
            }
        }

        return null;
    }

    private void LoadCard( CardDataRuntime data )
    {


        // Null card means we just want to add card to next valid empty card slot
        if( currentModifyCardIdx == null )
        {
            var nextEmptySlot = FindNextEmptyCardSlot();

            // TODO: Handle properly
            if( nextEmptySlot == null )
            {
                Debug.LogWarning( "No empty slot found to add card to on this page" );
                return;
            }

            currentModifyCardIdx = nextEmptySlot;
        }

        var idx = Utility.Mod( currentModifyCardIdx.Value, currentbinder.pageWidth * currentbinder.pageHeight );
        var rightSide = currentModifyCardIdx.Value >= ( currentbinder.pageWidth * currentbinder.pageHeight );
        var grid = rightSide ? cardsDisplayGridRight : cardsDisplayGridLeft;
        var image = grid.transform.GetChild( idx ).GetComponent<Image>();
        image.sprite = Utility.CreateSprite( data == null ? defaultCardImage : data.smallImage );
        currentbinder.cardList[rightSide ? currentPage : currentPage - 1][idx] = data;
        currentModifyCardIdx = null;
    }

    private void PopulateGrid()
    {
        // Setup page (and secondary/adjacent page)
        if( currentPage > 0 )
            SetupGrid( cardsDisplayGridLeft, currentPage - 1 );

        if( currentPage < currentbinder.pageCount - 1 )
            SetupGrid( cardsDisplayGridRight, currentPage );

        // Show/hide page depending on first/last page
        cardsDisplayGridLeft.gameObject.SetActive( currentPage > 0 );
        cardsDisplayGridRight.gameObject.SetActive( currentPage < currentbinder.pageCount - 1 );

        // Show/hide next/prev buttons depending on first/last page
        prevPageButton.gameObject.SetActive( currentPage > 0 );
        nextPageButton.gameObject.SetActive( currentPage < currentbinder.pageCount - 1 );

        // Show/hide modify buttons depending on first/last page
        modifyPageButtonsLeft.SetActive( currentPage > 0 );
        modifyPageButtonsRight.SetActive( currentPage < currentbinder.pageCount - 1 );
    }

    private void SetupGrid( AdvancedGridLayout grid, int page )
    {
        // TODO: Resetup grid X/Y
        if( width != currentbinder.pageWidth || height != currentbinder.pageHeight )
        {

        }
        else
        {
            // Reset/load images based on the stored data
            foreach( var (idx, card ) in Utility.Enumerate( currentbinder.cardList[page] ) )
            {
                var texture = card != null ? ( card.largeImage != null ? card.largeImage : card.smallImage ) : defaultCardImage;
                grid.transform.GetChild( idx ).GetComponent<Image>().sprite = Utility.CreateSprite( texture );
            }

            // Setup buttons
            for( int i = 0; i < grid.transform.childCount; ++i )
            {
                int idx = i;
                var dispatcher = grid.transform.GetChild( i ).GetComponent<EventDispatcher>();
                dispatcher.OnDoubleClickEvent = ( PointerEventData e ) =>
                {
                    // Add pageSize to the idx to indicate we are modifying the right page (or don't if left page)
                    currentModifyCardIdx = Utility.Mod( page + 1, 2 ) * currentbinder.pageWidth * currentbinder.pageHeight + idx;
                    OpenSearchPanel( grid, page, idx );
                };

                dispatcher.OnPointerDownEvent = ( PointerEventData e ) => StartDragging( grid, page, idx );
                dispatcher.OnPointerUpEvent = ( PointerEventData e ) => StopDragging( grid, page, idx );
            }
        }
    }

    private void StartDragging( AdvancedGridLayout grid, int page, int idx )
    {
        // Can't move empty cards
        if( currentbinder.cardList[page][idx] == null )
            return;

        var cardToCopy = grid.transform.GetChild( idx );

        var x = Input.mousePosition.x / mainCamera.pixelWidth * ( cardsPage.transform as RectTransform ).rect.width;
        var y = Input.mousePosition.y / mainCamera.pixelHeight * ( cardsPage.transform as RectTransform ).rect.height;
        dragOffset = cardToCopy.transform.position.ToVector2() - new Vector2( x, y );

        dragging = Instantiate( CardGridEntryPrefab, cardsPage.transform );
        ( dragging.transform as RectTransform ).anchoredPosition = new Vector2( x, y ) + dragOffset;
        var texture = cardToCopy.GetComponent<Image>().mainTexture as Texture2D;
        dragging.GetComponent<Image>().sprite = Utility.CreateSprite( texture );
        grid.transform.GetChild( idx ).GetComponent<Image>().sprite = Utility.CreateSprite( defaultCardImage );
        dragging.transform.position = Input.mousePosition;
        var worldRect = ( cardToCopy.transform as RectTransform ).GetWorldRect();
        ( dragging.transform as RectTransform ).sizeDelta = new Vector2( worldRect.width, worldRect.height );
    }

    private void StopDragging( AdvancedGridLayout grid, int page, int idx )
    {
        if( dragging == null )
            return;

        var otherpageIdx = GetOtherPageIndex( page );
        var otherGrid = grid == cardsDisplayGridLeft ? cardsDisplayGridRight : cardsDisplayGridLeft;

        for( int i = 0; i < currentbinder.pageWidth * currentbinder.pageHeight; ++i )
        {
            if( grid.isActiveAndEnabled && 
                StopDraggingCollisionCheck( grid.transform.GetChild( i ).gameObject, page, idx, page, i ) )
                break;

            if( otherGrid.isActiveAndEnabled &&
                StopDraggingCollisionCheck( otherGrid.transform.GetChild( i ).gameObject, page, idx, otherpageIdx, i ) )
                break;
        }

        dragging.Destroy();
    }

    private int GetOtherPageIndex( int page )
    {
        return page - 1 + Utility.Mod( page, 2 ) * 2;
    }

    private bool StopDraggingCollisionCheck( GameObject card, int pageFrom, int cardFrom, int pageTo, int cardTo )
    {
        var worldRect = ( card.transform as RectTransform ).GetWorldRect();

        // Collision check against other cards (centre within card bounds)
        // Swap cards (need to handle swap between page)
        if( worldRect.Contains( ( dragging.transform as RectTransform ).GetWorldRect().center ) )
        {
            var originTexture = dragging.GetComponent<Image>().mainTexture as Texture2D;
            var texture = card.GetComponent<Image>().mainTexture as Texture2D;
            dragging.GetComponent<Image>().sprite = Utility.CreateSprite( texture );
            card.GetComponent<Image>().sprite = Utility.CreateSprite( originTexture );

            (currentbinder.cardList[pageFrom][cardFrom], currentbinder.cardList[pageTo][cardTo]) = 
                (currentbinder.cardList[pageTo][cardTo], currentbinder.cardList[pageFrom][cardFrom]);
            return true;
        }

        return false;
    }

    private void Update()
    {
        if( dragging != null )
        {
            var x = Input.mousePosition.x / mainCamera.pixelWidth * ( cardsPage.transform as RectTransform ).rect.width;
            var y = Input.mousePosition.y / mainCamera.pixelHeight * ( cardsPage.transform as RectTransform ).rect.height;
            ( dragging.transform as RectTransform ).anchoredPosition = new Vector2( x, y ) + dragOffset;
        }
    }

    private void ChangePage(int page)
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
            behaviour = SearchPageBehaviour.AddingCards
        } );
    }

    public void OpenSearchPanel( AdvancedGridLayout grid, int page, int idx )
    {
        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent() 
        { 
            behaviour = currentbinder.cardList[page][idx] == null 
                ? SearchPageBehaviour.SettingCard
                : SearchPageBehaviour.ReplacingCard
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
        //SaveGameSystem.SaveGame( currentbinder.name );
    }
}