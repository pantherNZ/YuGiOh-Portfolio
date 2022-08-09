using UnityEngine;
using UnityEngine.UI;

public class SearchPageSmall : SearchPageBase
{
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] GameObject dragCardGhostPrefab = null;
    [SerializeField] Button clearCardButton = null;

    private Camera mainCamera;
    private GameObject dragging;

    protected override void Start()
    {
        base.Start();

        mainCamera = Camera.main;
    }

    protected override GameObject AddCardUI( CardData card, int entryIdx )
    {
        // Add UI elements
        var newCardUIEntry = Instantiate( cardEntryPrefab );
        newCardUIEntry.transform.SetParent( cardList.transform );

        var texts = newCardUIEntry.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = card.name;

        newCardUIEntry.GetComponent<EventDispatcher>().OnPointerDownEvent += ( e ) => StartDragging( newCardUIEntry, entryIdx );

        return newCardUIEntry;
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is OpenSearchPageEvent openPageRequest )
        {
            if( openPageRequest.openFullPage )
            {
                searchListPage.SetActive( false );
                return;
            }

            selectCardButton.interactable = openPageRequest.behaviour != SearchPageBehaviour.AddingCardsPageFull;
            clearCardButton.interactable = openPageRequest.behaviour == SearchPageBehaviour.ReplacingCard;
            selectCardButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text =
                openPageRequest.behaviour == SearchPageBehaviour.SettingCard ||
                openPageRequest.behaviour == SearchPageBehaviour.ReplacingCard ?
                "Select Card" : "Add Card";

            behaviour = openPageRequest.behaviour;
            searchListPage.SetActive( true );
        }
        else if( e is PageFullEvent )
        {
            Debug.Assert( behaviour == SearchPageBehaviour.AddingCards );
            behaviour = SearchPageBehaviour.AddingCardsPageFull;
            selectCardButton.interactable = false;
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
        var data = cardData[entryIdx];

        // TODO: Properly handle this?
        if( data.smallImage == null )
        {
            Debug.LogWarning( "Failed to drag card as the preview image hasn't finished downloading yet" );
            return;
        }

        dragging = Instantiate( dragCardGhostPrefab, searchListPage.transform.parent );
        ( dragging.transform as RectTransform ).anchoredPosition = Utility.GetMouseOrTouchPos();
        var texture = clickedOn.GetComponentsInChildren<Image>()[1].mainTexture as Texture2D;
        dragging.GetComponent<Image>().sprite = Utility.CreateSprite( texture );
        //var worldRect = ( cardToCopy.transform as RectTransform ).GetWorldRect();
        //( dragging.transform as RectTransform ).sizeDelta = new Vector2( worldRect.width, worldRect.height );
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