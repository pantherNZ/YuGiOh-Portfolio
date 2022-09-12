﻿using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SearchPageSmall : SearchPageBase
{
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] GameObject dragCardGhostPrefab = null;
    [SerializeField] GameObject searchListPanel = null;

    private GameObject dragging;

    protected override void Start()
    {
        base.Start();
    }

    protected override GameObject AddCardUI( CardDataRuntime card, int entryIdx )
    {
        // Add UI elements
        var newCardUIEntry = Instantiate( cardEntryPrefab );
        newCardUIEntry.transform.SetParent( cardList.transform );

        var texts = newCardUIEntry.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = card.name;

        newCardUIEntry.GetComponentInChildren<EventDispatcher>().OnBeginDragEvent += ( e ) => LeftMouseFilter( e, () => StartDragging( newCardUIEntry, entryIdx ) );

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
                searchListPage.SetActive( false );
                return;
            }

            var openInventory = e as OpenInventoryPageEvent;
            ShowPage( openPageRequest, openInventory?.currentBinderIdx );
        }
    }

    public void SwitchSides( RectTransform button )
    {
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
        else
        {
            InputPriority.Instance.Request( () =>
            {
                return Utility.IsMouseUpOrTouchEnd()
                    && !Utility.IsPointerOverGameObject( searchListPanel )
                    && searchListPage.activeInHierarchy;
    
            }, "SearchPageButton", 0, () =>
            {
                Cancel();
            } );
        }
    }
}