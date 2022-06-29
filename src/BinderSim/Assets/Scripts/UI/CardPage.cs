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
    [SerializeField] TMPro.TMP_InputField pageSizeText = null;
    [SerializeField] TMPro.TextMeshProUGUI dateCreatedText = null;
    [SerializeField] Texture2D defaultCardImage = null;
    [SerializeField] Button prevPageButton = null;
    [SerializeField] Button nextPageButton = null;
    [SerializeField] TMPro.TextMeshProUGUI currentPageTextLeft = null;
    [SerializeField] TMPro.TextMeshProUGUI currentPageTextRight = null;
    [SerializeField] GameObject CardGridEntryPrefab = null;

    private BinderData currentbinder;
    private int width = Constants.DefaultStartingPageWidth;
    private int height = Constants.DefaultStartingPageHeight;
    private int currentPage;
    private int? currentModifyCardIdx;

    private GameObject dragging;

    protected override void Start()
    {
        base.Start();
        cardsPage.SetActive( false );

        prevPageButton.onClick.AddListener( PrevPage );
        nextPageButton.onClick.AddListener( NextPage );
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
        else if( e is BinderDataUpdateEvent binderUpdateEvent )
        {
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
        pageSizeText.text = string.Format( "{0}x{1}", data.pageWidth, data.pageHeight );
        dateCreatedText.text = data.dateCreated.ToShortDateString();
        ChangePage( 0 );
    }

    private void LoadCard( CardDataRuntime data )
    {
        Debug.Assert( currentModifyCardIdx != null );
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
    }

    private void SetupGrid( AdvancedGridLayout grid, int page )
    {
        if( !currentbinder.cardList.ContainsKey( page ) )
        {
            var newPage = new List<CardDataRuntime>();
            newPage.Resize( currentbinder.pageWidth * currentbinder.pageHeight );
            currentbinder.cardList.Add( page, newPage );
        }

        // TODO: Resetup grid X/Y
        if( width != currentbinder.pageWidth || height != currentbinder.pageHeight )
        {

        }
        else
        {
            // Reset/load images based on the stored data
            foreach( var (idx, card ) in Utility.Enumerate( currentbinder.cardList[page] ) )
            {
                var texture = card != null ? ( card.largeImage ?? card.smallImage ) : defaultCardImage;
                grid.transform.GetChild( idx ).GetComponent<Image>().sprite = Utility.CreateSprite( texture );
            }

            // Setup buttons
            for( int i = 0; i < grid.transform.childCount; ++i )
            {
                int idx = i;
                var dispatcher = grid.transform.GetChild( i ).GetComponent<EventDispatcher>();
                dispatcher.OnDoubleClickEvent += ( PointerEventData e ) =>
                {
                    // Add pageSize to the idx to indicate we are modifying the right page (or don't if left page)
                    currentModifyCardIdx = Utility.Mod( page + 1, 2 ) * currentbinder.pageWidth * currentbinder.pageHeight + idx;
                    OpenSearchPanel( grid, page, idx );
                };

                dispatcher.OnPointerDownEvent += ( PointerEventData e ) => StartDragging( grid, page, idx );
                dispatcher.OnPointerUpEvent += ( PointerEventData e ) => StopDragging( grid, page, idx );
            }
        }
    }

    private void StartDragging( AdvancedGridLayout grid, int page, int idx )
    {
        // Can't move empty cards
        if( currentbinder.cardList[page][idx] == null )
            return;

        dragging = Instantiate( CardGridEntryPrefab, cardsPage.transform );
        var cardToCopy = grid.transform.GetChild( idx );
        var texture = cardToCopy.GetComponent<Image>().mainTexture as Texture2D;
        dragging.GetComponent<Image>().sprite = Utility.CreateSprite( texture );
        dragging.transform.position = Input.mousePosition;
        var worldRect = ( cardToCopy.transform as RectTransform ).GetWorldRect();
        ( dragging.transform as RectTransform ).sizeDelta = new Vector2( worldRect.width, worldRect.height );
    }

    private void StopDragging( AdvancedGridLayout grid, int page, int idx )
    {
        if( dragging == null )
            return;

        var originCard = grid.transform.GetChild( idx ) as RectTransform;

        for( int i = 0; i < currentbinder.pageWidth * currentbinder.pageHeight; ++i )
        {
            if( i == idx )
                continue;

            var card = grid.transform.GetChild( i );
            var worldRect = ( card.transform as RectTransform ).GetWorldRect();

            // Collision check against other cards (centre within card bounds)
            // Swap cards (need to handle swap between page)
            if( worldRect.Contains( ( dragging.transform as RectTransform ).GetWorldRect().center ) )
            {
                var originTexture = originCard.GetComponent<Image>().mainTexture as Texture2D;
                var texture = card.GetComponent<Image>().mainTexture as Texture2D;
                originCard.GetComponent<Image>().sprite = Utility.CreateSprite( texture );
                card.GetComponent<Image>().sprite = Utility.CreateSprite( originTexture );

                currentbinder.cardList[page].Swap( i, idx );

                var originSmallImage = currentbinder.cardList[page][i]?.smallImage;
                var originLargeImage = currentbinder.cardList[page][i]?.largeImage;
                var destSmallImage = currentbinder.cardList[page][idx]?.smallImage;
                var destLargeImage = currentbinder.cardList[page][idx]?.largeImage;

                if( originSmallImage != null )
                    currentbinder.cardList[page][i].smallImage = destSmallImage;
                if( destSmallImage != null )
                    currentbinder.cardList[page][idx].smallImage = originSmallImage;
                if( originLargeImage != null )
                    currentbinder.cardList[page][i].largeImage = destLargeImage;
                if( destLargeImage != null )
                    currentbinder.cardList[page][idx].largeImage = originLargeImage;

                break;
            }
        }

        dragging.Destroy();
    }

    private void Update()
    {
        if( dragging != null )
        {
            var x = Input.mousePosition.x / Camera.main.pixelWidth * ( cardsPage.transform as RectTransform ).rect.width;
            var y = Input.mousePosition.y / Camera.main.pixelHeight * ( cardsPage.transform as RectTransform ).rect.height;
            ( dragging.transform as RectTransform ).anchoredPosition = new Vector2( x, y );
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

    public void OpenSearchPanel( AdvancedGridLayout grid, int page, int idx )
    {
        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent() 
        { 
            existingCardIsEmpty = currentbinder.cardList[page][idx] == null
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