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
    [SerializeField] GameObject cardList = null;
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] TMPro.TMP_InputField searchInput = null;
    [SerializeField] Button selectCardButton = null;
    [SerializeField] Color selectedEntryColour = new Color();

    private List<CardData> cardData = new List<CardData>();
    private int? currentCardSelectedIdx;

    public void SearchCards()
    {
        // Remove current card entries (skip/leave header)
        for( int i = 1; i < cardList.transform.childCount; ++i )
            cardList.transform.GetChild( i ).gameObject.Destroy();
        selectCardButton.interactable = false;

        // https://db.ygoprodeck.com/api-guide/
        var url = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?fname={0}", searchInput.text );
        StartCoroutine( SendGetRequest( url, HandleSearchResult ) );


        //byte[] results = request.downloadHandler.data;
        //string filename = gameObject.name + ".dat";
        //SaveImage( "Images/" + filename, results );
    }

    private void HandleSearchResult( string result )
    {
        Root data = JsonConvert.DeserializeObject<Root>( result );

        if( data.data.IsEmpty() )
        {
            AddCard( new CardData()
            {
                name = "No results found"
            } );
        }
        else
        {
            foreach( var card in data.data )
            {
                AddCard( new CardData()
                {
                    name = card.name,
                    cardId = card.id,
                    imagePath = "test",
                } );
            }
        }
    }

    IEnumerator SendGetRequest( string url, Action<string> callback = null)
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
                    Debug.LogError( url + ": HTTP Error: " + webRequest.error );
                    break;
                case UnityWebRequest.Result.Success:
                    callback?.Invoke( webRequest.downloadHandler.text );
                    break;
            }
        }
    }

    IEnumerator DownloadImage( string url, Action<Texture> callback)
    {
        Debug.Log( "Start Downloading Images" );

        using( UnityWebRequest request = UnityWebRequestTexture.GetTexture( url ) )
        {
            // uwr2.downloadHandler = new DownloadHandlerBuffer();
            yield return request.SendWebRequest();

            if( request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError )
            {
                Debug.Log( request.error );
            }
            else
            {
                Debug.Log( "Success" );
                callback?.Invoke( DownloadHandlerTexture.GetContent( request ) );
            }
        }
    }

    void SaveImage( string path, byte[] imageBytes )
    {
        //Create Directory if it does not exist
        if( !Directory.Exists( Path.GetDirectoryName( path ) ) )
        {
            Directory.CreateDirectory( Path.GetDirectoryName( path ) );
            Debug.Log( "Creating now" );
        }
        else
        {
            Debug.Log( path + " does exist" );
        }

        try
        {
            File.WriteAllBytes( path, imageBytes );
            Debug.Log( "Saved Data to: " + path.Replace( "/", "\\" ) );
        }
        catch( Exception e )
        {
            Debug.LogWarning( "Failed To Save Data to: " + path.Replace( "/", "\\" ) );
            Debug.LogWarning( "Error: " + e.Message );
        }
    }

    private GameObject GetSelectedCard()
    {
        return currentCardSelectedIdx.HasValue ? cardList.transform.GetChild( currentCardSelectedIdx.Value + 1 ).gameObject : null;
    }

    public void AddCard( CardData card )
    {
        cardData.Add( card );
        var newCardUIEntry = AddCardUI( card );

        // On click
        var eventDispatcher = newCardUIEntry.GetComponent<EventDispatcher>();
        int thisIdx = cardData.Count - 1;

        eventDispatcher.OnPointerUpEvent += ( PointerEventData e ) =>
        {
            bool unselect = currentCardSelectedIdx == thisIdx;
            if( currentCardSelectedIdx != null || unselect )
                GetSelectedCard().GetComponent<Image>().color = new Color( 0.0f, 0.0f, 0.0f, 0.0f );
            if( !unselect )
                newCardUIEntry.GetComponent<Image>().color = selectedEntryColour;
            currentCardSelectedIdx = unselect ? null : thisIdx;
            selectCardButton.interactable = !unselect;
        };

        // TODO: Double click to choose
        eventDispatcher.OnDoubleClickEvent += ( PointerEventData e ) =>
        {

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
        texts[0].text = cardData.Back().name;

        return newCardUIEntry;
    }

    public void ChooseCard()
    {
        Debug.Assert( currentCardSelectedIdx != null );
        EventSystem.Instance.TriggerEvent( new CardSelectedEvent()
        {
            card = cardData[currentCardSelectedIdx.Value]
        } );
    }

    public override void OnEventReceived( IBaseEvent e )
    {
    }
}