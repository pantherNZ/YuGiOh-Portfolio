﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.IO;
using Newtonsoft.Json;

public abstract class SearchPageBase : EventReceiverInstance
{
    [SerializeField] protected GameObject searchListPage = null;
    [SerializeField] protected GameObject cardList = null;
    [SerializeField] protected TMPro.TMP_InputField searchInput = null;
    [SerializeField] protected Button selectCardButton = null;
    [SerializeField] protected Color selectedEntryColour = new();
    [SerializeField] protected bool autoSearch = true;
    [SerializeField] protected int maxSearchResults = 100;
    [SerializeField] protected int autoSearchDelayMS = 500;

    protected List<CardDataRuntime> cardData = new();
    protected Dictionary<CardDataRuntime, GameObject> searchUIEntries = new();
    protected int? currentCardSelectedIdx;
    protected SearchPageBehaviour behaviour;
    private Coroutine searchCountdown;

    override protected void Start()
    {
        base.Start();

        searchListPage.SetActive( false );

        searchInput.onValueChanged.AddListener( OnSearchTextchanged );
    }

    private void OnSearchTextchanged( string text )
    {
        if( searchCountdown != null )
            StopCoroutine( searchCountdown );

        searchCountdown = StartCoroutine( TextChangedTimer() );
    }

    private IEnumerator TextChangedTimer()
    {
        yield return new WaitForSeconds( autoSearchDelayMS / 1000.0f );
        SearchCards();
    }

    public void SearchCards()
    {
        // Remove current card entries (skip/leave header)
        for( int i = 0; i < cardList.transform.childCount; ++i )
            cardList.transform.GetChild( i ).gameObject.Destroy();
        selectCardButton.interactable = false;
        currentCardSelectedIdx = null;
        searchUIEntries.Clear();
        cardData.Clear();

        var search = searchInput.text.Trim();
        if( search.Length > 0 )
            StartCoroutine( APICallHandler.Instance.SendCardSearchRequestFuzzy( search, false, OnSearchResultReceived ) );
    }

    private void OnSearchResultReceived( string result )
    {
        Root data = JsonConvert.DeserializeObject<Root>( result );

        if( data.data.IsEmpty() )
        {
            AddCard( new CardDataRuntime() { name = "No results found" } );
        }
        else
        {
            foreach( var( idx, card ) in Utility.Enumerate( data.data ) )
            {
                // Limit to 100 results for now
                if( idx >= maxSearchResults )
                    break;

                var newCard = new CardDataRuntime()
                {
                    name = card.name,
                    cardId = card.id,
                    imageId = card.card_images[0].id,
                    cardAPIData = card.DeepCopy(),
                };
                AddCard( newCard );

                if( Constants.Instance.DownloadImages )
                {
                    var smallImageUrl = card.card_images[0].image_url_small;
                    StartCoroutine( APICallHandler.Instance.DownloadImage( smallImageUrl, true, ( texture ) => OnImageDownloaded( texture, newCard ) ) );
                }
            }
        }
    }

    private void OnImageDownloaded( Texture2D texture, CardDataRuntime cardData )
    {
        if( !searchUIEntries.ContainsKey( cardData ) )
            return;

        // Idx 1 because 0 is the UI entry background (1 is the preview card image)
        var cardPreview = searchUIEntries[cardData].GetComponentsInChildren<Image>()[1];
        cardPreview.sprite = Utility.CreateSprite( texture );
        cardPreview.color = Color.white;
        cardData.smallImage = texture;
    }

    protected GameObject GetSelectedCard()
    {
        return currentCardSelectedIdx.HasValue ? cardList.transform.GetChild( currentCardSelectedIdx.Value ).gameObject : null;
    }

    public void AddCard( CardDataRuntime card )
    {
        var newCardUIEntry = AddCardUI( card );
        searchUIEntries.Add( card, newCardUIEntry );
        cardData.Add( card );

        // On click
        var eventDispatcher = newCardUIEntry.GetComponent<EventDispatcher>();
        int thisIdx = cardData.Count - 1;

        newCardUIEntry.GetComponentsInChildren<Image>()[1].color = Color.clear;

        eventDispatcher.OnPointerUpEvent = ( PointerEventData e ) =>
        {
            bool unselect = currentCardSelectedIdx == thisIdx;
            if( currentCardSelectedIdx != null || unselect )
                GetSelectedCard().GetComponent<Image>().color = Color.clear;
            if( !unselect )
                newCardUIEntry.GetComponent<Image>().color = selectedEntryColour;
            currentCardSelectedIdx = unselect ? null : thisIdx;
            selectCardButton.interactable = !unselect && behaviour != SearchPageBehaviour.AddingCardsPageFull;
        };

        // TODO: Double click to choose
        eventDispatcher.OnDoubleClickEvent += ( PointerEventData e ) =>
        {
            currentCardSelectedIdx = thisIdx;
            ChooseCard();
        };

        // TODO: Hover to show card image?
        eventDispatcher.OnPointerEnterEvent = ( PointerEventData e ) =>
        {

        };

        eventDispatcher.OnPointerExitEvent = ( PointerEventData e ) =>
        {

        };
    }

    protected abstract GameObject AddCardUI( CardData card );

    // Called when you either double click a search result, or click to highlight and then click 'Select Card' button
    public void ChooseCard()
    {
        if( behaviour == SearchPageBehaviour.AddingCardsPageFull )
            return;

        Debug.Assert( currentCardSelectedIdx != null );

        var data = cardData[currentCardSelectedIdx.Value];

        if( data.smallImage == null )
        {
            Debug.LogWarning( "Failed to choose card as the preview image hasn't finished downloading yet" );
            return;
        }

        EventSystem.Instance.TriggerEvent( new CardSelectedEvent()
        {
            card = data,
        } );

        // Only hide this page if we are selecting for a specific card
        // This intentionally will switch back to the card list if the page is now full
        // (new behaviour will be set to AddingCardsPageFull via the PageFullEvent)
        if( behaviour != SearchPageBehaviour.AddingCards )
            searchListPage.SetActive( false );

        if( Constants.Instance.DownloadImages && Constants.Instance.DownloadLargeImages )
        {
            data.largeImageRequsted = true;

            StartCoroutine( APICallHandler.Instance.DownloadImage( data.cardAPIData.card_images[0].image_url, true, ( texture ) =>
            {
                // TODO: Save/cache image
                data.largeImage = texture;
                data.largeImageRequsted = false;

                EventSystem.Instance.TriggerEvent( new CardImageLoadedEvent()
                {
                    card = data,
                } );
            } ) );
        }
    }

    public void ClearCard()
    {
        searchListPage.SetActive( false );
        EventSystem.Instance.TriggerEvent( new CardSelectedEvent() { card = null } );
    }

    public void Cancel()
    {
        searchListPage.SetActive( false );
        EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent() { page = PageType.CardPage } );
    }

    public void ShowFullscreenSearch( bool fullscreen )
    {
        EventSystem.Instance.TriggerEvent( new OpenSearchPageEvent()
        {
            openFullPage = fullscreen,
            behaviour = behaviour
        } );
    }
}