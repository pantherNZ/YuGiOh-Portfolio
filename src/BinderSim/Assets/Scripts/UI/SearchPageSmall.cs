using UnityEngine;
using UnityEngine.UI;

public class SearchPageSmall : SearchPageBase
{
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] GameObject dragCardGhostPrefab = null;

    private GameObject dragging;

    protected override void Start()
    {
        base.Start();
    }

    protected override GameObject AddCardUI( CardData card, int entryIdx )
    {
        // Add UI elements
        var newCardUIEntry = Instantiate( cardEntryPrefab );
        newCardUIEntry.transform.SetParent( cardList.transform );

        var texts = newCardUIEntry.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = card.name;

        newCardUIEntry.GetComponentInChildren<EventDispatcher>().OnBeginDragEvent += ( e ) => StartDragging( newCardUIEntry, entryIdx );

        return newCardUIEntry;
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        base.OnEventReceived( e );

        if( e is OpenSearchPageEvent openPageRequest )
        {
            if( openPageRequest.openFullPage )
            {
                searchListPage.SetActive( false );
                return;
            }

            behaviour = openPageRequest.behaviour;
            searchListPage.SetActive( true );
        }
        else if( e is PageFullEvent )
        {
            Debug.Assert( behaviour == SearchPageBehaviour.AddingCards );
            behaviour = SearchPageBehaviour.AddingCardsPageFull;
        }
    }

    public void SwitchSides()
    {
        var rectTransform = searchListPage.transform as RectTransform;
        var xPos = rectTransform.anchoredPosition.x;
        rectTransform.anchorMax = rectTransform.anchorMax.SetX( xPos >= 0.0f ? 1.0f : 0.0f );
        rectTransform.anchorMin = rectTransform.anchorMin.SetX( xPos >= 0.0f ? 1.0f : 0.0f );
        rectTransform.anchoredPosition = rectTransform.anchoredPosition.SetX( -xPos );
    }

    private void StartDragging( GameObject clickedOn, int entryIdx )
    {
        if( currentCardSelectedIdx == null )
            clickedOn.GetComponent<EventDispatcher>().OnPointerDownEvent?.Invoke( null );

        var data = cardData[entryIdx];

        // TODO: Properly handle this?
        if( data.smallImage == null )
        {
            Debug.LogWarning( "Failed to drag card as the preview image hasn't finished downloading yet" );
            return;
        }

        dragging = Instantiate( dragCardGhostPrefab, searchListPage.transform.parent );
        ( dragging.transform as RectTransform ).anchoredPosition = Utility.GetMouseOrTouchPos();
        var image = clickedOn.GetComponentsInChildren<Image>()[1];
        var texture = image.mainTexture as Texture2D;
        dragging.GetComponent<Image>().sprite = Utility.CreateSprite( texture );
        var worldRect = ( image.transform as RectTransform ).GetWorldRect();
        ( dragging.transform as RectTransform ).sizeDelta = new Vector2( worldRect.width, worldRect.height );
    }

    private void StopDragging()
    {
        Debug.Assert( currentCardSelectedIdx != null );
        dragging.Destroy();
        ChooseCardInternal( true );
    }

    private void Update()
    {
        if( dragging != null )
        {
            ( dragging.transform as RectTransform ).anchoredPosition = Utility.GetMouseOrTouchPos();

            if( Utility.IsMouseUpOrTouchEnd() )
                StopDragging();
        }
    }
}