using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.IO;
using Newtonsoft.Json;

public class SearchPage : EventReceiverInstance
{
    [SerializeField] GameObject searchListPage = null;
    [SerializeField] GameObject cardList = null;
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] TMPro.TMP_InputField searchInput = null;
    [SerializeField] Button selectCardButton = null;
    [SerializeField] Button clearCardButton = null;
    [SerializeField] Color selectedEntryColour = new Color();
    [SerializeField] bool downloadImages = true;
    [SerializeField] bool downloadLargeImages = true;

    private List<CardDataRuntime> cardData = new();
    private Dictionary<CardDataRuntime, GameObject> searchUIEntries = new();
    private int? currentCardSelectedIdx;
    private RateLimiter rateLimiter = new( 20, TimeSpan.FromSeconds( 1.0 ) );

    override protected void Start()
    {
        base.Start();

        searchListPage.SetActive( false );
    }

    public void SearchCards()
    {
        // Remove current card entries (skip/leave header)
        for( int i = 0; i < cardList.transform.childCount; ++i )
            cardList.transform.GetChild( i ).gameObject.Destroy();
        selectCardButton.interactable = false;
        currentCardSelectedIdx = null;
        searchUIEntries.Clear();

        // https://db.ygoprodeck.com/api-guide/
        var url = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?fname={0}", searchInput.text );
        StartCoroutine( SendGetRequest( url, OnSearchResultReceived ) );
    }

    private void OnSearchResultReceived( string result )
    {
        Root data = JsonConvert.DeserializeObject<Root>( result );

        if( data.data.IsEmpty() )
        {
            AddCard( new CardDataRuntime(){ name = "No results found" } );
        }
        else
        {
            foreach( var card in data.data )
            {
                var newCard = new CardDataRuntime()
                {
                    name = card.name,
                    cardId = card.id,
                    imageId = card.card_images[0].id,
                    cardAPIData = card.DeepCopy(),
                };
                AddCard( newCard );

                if( downloadImages )
                {
                    var smallImageUrl = card.card_images[0].image_url_small;
                    StartCoroutine( DownloadImage( smallImageUrl, ( texture ) => OnImageDownloaded( texture, newCard ) ) );
                }
            }
        }
    }

    private void OnImageDownloaded( Texture2D texture, CardDataRuntime cardData )
    {
        // Idx 1 because 0 is the UI entry background (1 is the preview card image)
        var cardPreview = searchUIEntries[cardData].GetComponentsInChildren<Image>()[1];
        cardPreview.sprite = Utility.CreateSprite( texture );
        cardPreview.color = Color.white;
        cardData.smallImage = texture;


        // TODO: Save/cache image
        //byte[] results = request.downloadHandler.data;
        //string filename = gameObject.name + ".dat";
        //SaveImage( "Images/" + filename, results );
    }

    IEnumerator SendGetRequest( string url, Action<string> callback = null)
    {
        if( rateLimiter.AttemptCall() )
        {
            using( UnityWebRequest webRequest = UnityWebRequest.Get( url ) )
            {
                // Request and wait for the desired page.
                yield return webRequest.SendWebRequest();

                switch( webRequest.result )
                {
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                        Debug.LogError( url + ": Error: " + webRequest.error );
                        break;
                    case UnityWebRequest.Result.ProtocolError:
                        //Debug.LogError( url + ": HTTP Error: " + webRequest.error );
                        break;
                    case UnityWebRequest.Result.Success:
                        callback?.Invoke( webRequest.downloadHandler.text );
                        break;
                }
            }
        }
    }

    IEnumerator DownloadImage( string url, Action<Texture2D> callback)
    {
        if( rateLimiter.AttemptCall() )
        {
            using( UnityWebRequest request = UnityWebRequestTexture.GetTexture( url ) )
            {
                yield return request.SendWebRequest();

                if( request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError )
                {
                    Debug.Log( request.error );
                }
                else
                {
                    callback?.Invoke( DownloadHandlerTexture.GetContent( request ) );
                }
            }
        }
    }

    void SaveImage( string path, byte[] imageBytes )
    {
        //Create Directory if it does not exist
        if( !Directory.Exists( Path.GetDirectoryName( path ) ) )
            Directory.CreateDirectory( Path.GetDirectoryName( path ) );

        try
        {
            File.WriteAllBytes( path, imageBytes );
        }
        catch( Exception e )
        {
            Debug.LogWarning( "Failed To Save Data to: " + path.Replace( "/", "\\" ) );
            Debug.LogWarning( "Error: " + e.Message );
        }
    }

    private GameObject GetSelectedCard()
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

        newCardUIEntry.GetComponentsInChildren<Image>()[1].color =  Color.clear;

        eventDispatcher.OnPointerUpEvent += ( PointerEventData e ) =>
        {
            bool unselect = currentCardSelectedIdx == thisIdx;
            if( currentCardSelectedIdx != null || unselect )
                GetSelectedCard().GetComponent<Image>().color = Color.clear;
            if( !unselect )
                newCardUIEntry.GetComponent<Image>().color = selectedEntryColour;
            currentCardSelectedIdx = unselect ? null : thisIdx;
            selectCardButton.interactable = !unselect;
        };

        // TODO: Double click to choose
        eventDispatcher.OnDoubleClickEvent += ( PointerEventData e ) =>
        {
            currentCardSelectedIdx = thisIdx;
            ChooseCard();
        };

        // TODO: Hover to show card image?
        eventDispatcher.OnPointerEnterEvent += ( PointerEventData e ) =>
        {

        };

        eventDispatcher.OnPointerExitEvent += ( PointerEventData e ) =>
        {

        };
    }

    private GameObject AddCardUI( CardData card )
    {
        // Add UI elements
        var newCardUIEntry = Instantiate( cardEntryPrefab );
        newCardUIEntry.transform.SetParent( cardList.transform );

        var texts = newCardUIEntry.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = card.name;

        return newCardUIEntry;
    }

    public void ChooseCard()
    {
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

        searchListPage.SetActive( false );

        if( downloadImages && downloadLargeImages )
        {
            StartCoroutine( DownloadImage( data.cardAPIData.card_images[0].image_url, ( texture ) =>
            {
                // TODO: Save/cache image
                data.largeImage = texture;

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

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is OpenSearchPageEvent openPageRequest )
        {
            clearCardButton.interactable = !openPageRequest.existingCardIsEmpty;
            searchListPage.SetActive( true );
        }
    }
}