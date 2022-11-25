using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SearchPageSmall : SearchPageBase
{
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] GameObject dragCardGhostPrefab = null;
    [SerializeField] GameObject searchListPanel = null;
    [SerializeField] float slideAnimationTimeSec = 0.1f;
    [SerializeField] Button cardPageSaveAndExitButton;

    private GameObject dragging;
    private bool leftSide = true;
    private Coroutine showHideRoutine;

    protected override void Start()
    {
        base.Start();
    }

    protected override bool ContainsHeader()
    {
        return false;
    }

    protected override GameObject AddCardUI( CardDataRuntime card, int entryIdx )
    {
        // Add UI elements
        var newCardUIEntry = Instantiate( cardEntryPrefab );
        newCardUIEntry.transform.SetParent( cardList.transform );
        newCardUIEntry.GetComponentInChildren<EventDispatcher>().OnBeginDragEvent += ( e ) => LeftMouseFilter( e, () => StartDragging( newCardUIEntry, entryIdx ) );

        var searchEntry = newCardUIEntry.GetComponent<SearchListEntry>();
        searchEntry.Initialise( card, behaviour, flags, GetDropDownOption() );

        return newCardUIEntry;
    }

    void LeftMouseFilter( PointerEventData e, Action func )
    {
        InputPriority.Instance.Request( () => e.button == PointerEventData.InputButton.Left, "SearchPageButton", 1, func );
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        base.OnEventReceived( e );

        if( e is OpenSearchPageEvent openPageRequest )
        {
            if( openPageRequest.page == PageType.SearchPageFull )
            {
                HidePage();
                return;
            }

            ShowPage( openPageRequest, openPageRequest.currentBinderIdx );
        }
    }

    protected override void ShowPageInternal()
    {
        base.ShowPageInternal();

        titleText.SetText(
            flags.HasFlag( SearchPageFlags.SettingCards )
            ? "Choosing Card"
            : flags.HasFlag( SearchPageFlags.ReplacingCard )
            ? "Replacing Card" 
            : "Searching Cards" );
        titleText.gameObject.SetActive( behaviour != SearchPageOrigin.MainPage );

        if( showHideRoutine != null )
            StopCoroutine( showHideRoutine );
        showHideRoutine = StartCoroutine( InterpolatePosition( true ) );
    }

    protected override void HidePage()
    {
        if( showHideRoutine != null )
            StopCoroutine( showHideRoutine );
        showHideRoutine = StartCoroutine( HidePageInternal() );
    }

    private IEnumerator HidePageInternal()
    {
        yield return InterpolatePosition( false );
        searchListPage.SetActive( false );
    }

    private IEnumerator InterpolatePosition( bool show )
    {
        var rectTransform = searchListPanel.transform as RectTransform;
        var moveRight = leftSide == show;
        var targetX = Mathf.Abs( rectTransform.anchoredPosition.x ) * ( moveRight ? 1.0f : -1.0f );
        var interp = Mathf.Abs( targetX - rectTransform.anchoredPosition.x );

        while( ( moveRight && rectTransform.anchoredPosition.x < targetX ) ||
               ( !moveRight && rectTransform.anchoredPosition.x > targetX ) )
        {
            var diff = targetX - rectTransform.anchoredPosition.x;
            var delta = Time.deltaTime * ( 1.0f / slideAnimationTimeSec );
            rectTransform.anchoredPosition += new Vector2( Mathf.Min( Mathf.Abs( diff ), interp * delta ) * Mathf.Sign( diff ), 0.0f );
            yield return null;
        }

        rectTransform.anchoredPosition = rectTransform.anchoredPosition.SetX( targetX );
    }

    public void SwitchSides( RectTransform button )
    {
        leftSide = !leftSide;
        var rectTransform = searchListPanel.transform as RectTransform;
        var xPos = rectTransform.anchoredPosition.x;
        rectTransform.anchorMax = rectTransform.anchorMax.SetX( xPos >= 0.0f ? 1.0f : 0.0f );
        rectTransform.anchorMin = rectTransform.anchorMin.SetX( xPos >= 0.0f ? 1.0f : 0.0f );
        rectTransform.anchoredPosition = rectTransform.anchoredPosition.SetX( -xPos );
        button.localEulerAngles = new Vector3( 0.0f, 0.0f, xPos >= 0.0f ? 180.0f : 0.0f );
    }

    private void StartDragging( GameObject clickedOn, int entryIdx )
    {
        if( currentCardSelectedIdx == null )
            clickedOn.GetComponent<EventDispatcher>().OnPointerDownEvent?.Invoke( null );

        var data = cardData[entryIdx];

        // TODO: Properly handle this?
        if( data.smallImages == null )
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
        else
        {
            InputPriority.Instance.Request( () =>
            {
                return showHideRoutine == null
                    && Utility.IsMouseUpOrTouchEnd()
                    && !Utility.IsPointerOverGameObject( searchListPanel )
                    && searchListPage.activeInHierarchy;
    
            }, "SearchPageButton", 0, () =>
            {
                Cancel();

                if( Utility.IsPointerOverGameObject( cardPageSaveAndExitButton.gameObject ) )
                    cardPageSaveAndExitButton.onClick.Invoke();
            } );
        }
    }
}