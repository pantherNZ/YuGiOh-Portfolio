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
    [SerializeField] GameObject searchListPage = null;
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
        searchListPage.SetActive( false );

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
                    LoadBinder( pageChangeRequest.binder );
                    break;
            }
        }
        else if( e is BinderDataUpdateEvent binderUpdateEvent )
        {
        }
        else if( e is CardSelectedEvent cardSelectedEvent )
        {
            searchListPage.SetActive( false );
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
        image.sprite = Utility.CreateSprite( data.smallImage );
        currentbinder.cardList[rightSide ? currentPage : currentPage - 1][idx] = data;
    }

    private void PopulateGrid()
    {
        // Setup page (and secondary/adjacent page)
        if( currentPage > 0 )
            SetupGrid( currentPage - 1, cardsDisplayGridLeft );

        if( currentPage < currentbinder.pageCount - 1 )
            SetupGrid( currentPage, cardsDisplayGridRight );

        // Show/hide page depending on first/last page
        cardsDisplayGridLeft.gameObject.SetActive( currentPage > 0 );
        cardsDisplayGridRight.gameObject.SetActive( currentPage < currentbinder.pageCount - 1 );

        // Show/hide next/prev buttons depending on first/last page
        prevPageButton.gameObject.SetActive( currentPage > 0 );
        nextPageButton.gameObject.SetActive( currentPage < currentbinder.pageCount - 1 );
    }

    private void SetupGrid( int page, AdvancedGridLayout grid )
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
                    OpenSearchPanel();
                };

                dispatcher.OnPointerDownEvent += ( PointerEventData e ) =>
                {
                    dragging = Instantiate( CardGridEntryPrefab, cardsPage.transform );
                    var cardToCopy = grid.transform.GetChild( idx );
                    var texture = cardToCopy.GetComponent<Image>().mainTexture as Texture2D;
                    dragging.GetComponent<Image>().sprite = Utility.CreateSprite( texture );
                    dragging.transform.position = Input.mousePosition;
                    var worldRect = ( cardToCopy.transform as RectTransform ).GetWorldRect();
                    ( dragging.transform as RectTransform ).sizeDelta = new Vector2( worldRect.width, worldRect.height );
                };

                dispatcher.OnPointerUpEvent += ( PointerEventData e ) =>
                {
                    dragging.Destroy();
                };
            }
        }
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

    public void OpenSearchPanel()
    {
        searchListPage.SetActive( true );
    }

    public void Selectcard()
    {
        Debug.Assert( currentModifyCardIdx != null );
        searchListPage.SetActive( false );
        currentModifyCardIdx = null;
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