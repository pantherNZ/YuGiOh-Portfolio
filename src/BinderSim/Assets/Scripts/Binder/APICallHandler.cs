using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

class APICallHandler : MonoBehaviour
{
    static APICallHandler instance;
    public static APICallHandler Instance { get => instance; }

    private RateLimiter rateLimiter = new RateLimiter( 20, TimeSpan.FromSeconds( 1.0 ) );
    public RateLimiter RateLimiterInst => rateLimiter;

    private Dictionary<string, string> cachedRequests = new Dictionary<string, string>();
    private Dictionary<string, Texture2D> cachedImages = new Dictionary<string, Texture2D>();
    [HideInInspector] public Root cardsDatabase;
    [HideInInspector] public List<Archetype> archetypes;
    [HideInInspector] public List<SetInfo> sets;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // Load cards DB from json
        Task.Run( () =>
        {
            try
            {
                AddCardDatabase( Constants.Instance.storedCardsData );
            }
            catch( Exception e )
            {
                Debug.LogError( "AddCardDatabase error: " + e.Message );
            }
        } );

        // Request full up to date DB from API
        StartCoroutine( SendGetRequest( "https://db.ygoprodeck.com/api/v7/cardinfo.php?misc=yes", true, json =>
        {
            Task.Run( () =>
            {
                //AddCardDatabase( json );
            } );
        } ) );

        // Request archetypes db
        StartCoroutine( SendGetRequest( "https://db.ygoprodeck.com/api/v7/archetypes.php", true, archetypesJson =>
        {
            archetypes = JsonConvert.DeserializeObject<List<Archetype>>( archetypesJson );
        } ) );

        // Request sets db
        StartCoroutine( SendGetRequest( "https://db.ygoprodeck.com/api/v7/cardsets.php", true, setTypesJson =>
        {
            sets = JsonConvert.DeserializeObject<List<SetInfo>>( setTypesJson );
        } ) );
    }

    private void AddCardDatabase( string json )
    {
        var settings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        cardsDatabase = JsonConvert.DeserializeObject<Root>( json, settings );
        
        // Remove unreleased cards
        cardsDatabase.data.RemoveAll( x => x.card_sets == null );
    }

    // https://db.ygoprodeck.com/api-guide/
    public IEnumerator SendCardSearchRequest( string cardName, bool waitForRateLimit, Action<string> callback = null )
    {
        var uri = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?name={0}&misc=yes", Uri.EscapeUriString( cardName ) );
        return SendGetRequest( uri, waitForRateLimit, callback );
    }

    public IEnumerator SendCardSearchRequest( int cardId, bool waitForRateLimit, Action<string> callback = null )
    {
        var uri = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?id={0}&misc=yes", cardId );
        return SendGetRequest( uri, waitForRateLimit, callback );
    }

    public IEnumerator SendCardSearchRequestFuzzy( string cardName, bool waitForRateLimit, Action<string> callback = null, bool alreadyEscaped = false )
    {
        var uri = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?fname={0}&misc=yes", alreadyEscaped ? cardName : Uri.EscapeUriString( cardName ) );
        return SendGetRequest( uri, waitForRateLimit, callback );
    }

    public IEnumerator SendGetRequest( string uri, bool waitForRateLimit, Action<string> callback = null, Action<UnityWebRequest.Result, string> failedCallback = null )
    {
        if( !waitForRateLimit )
        {
            if( rateLimiter.AttemptCall() )
                yield return SendGetRequestInternal( uri, callback, failedCallback );
        }
        else
        {
            yield return rateLimiter.WaitForCall( SendGetRequestInternal( uri, callback, failedCallback ) );
        }
    }

    private IEnumerator SendGetRequestInternal( string uri, Action<string> successCallback = null, Action<UnityWebRequest.Result, string> failedCallback = null )
    {
        uri = uri.Replace( "\'", "%27" );

        if( cachedRequests.TryGetValue( uri, out string data ) )
        {
            successCallback?.Invoke( data );
            yield break;
        }    

        using( UnityWebRequest webRequest = UnityWebRequest.Get( uri ) )
        {
            cachedRequests[uri] = string.Empty;

            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch( webRequest.result )
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError( uri + ": Error: " + webRequest.error );
                    failedCallback( webRequest.result, webRequest.error );
                    break;
                case UnityWebRequest.Result.ProtocolError:
                case UnityWebRequest.Result.Success:
                    cachedRequests[uri] = webRequest.downloadHandler.text;
                    successCallback?.Invoke( webRequest.downloadHandler.text );
                    break;
            }
        }
    }

    public struct URLShortenerResult
    {
#pragma warning disable 0649
        public string url;
#pragma warning restore 0649
    }

    public static string URLShortenerAddress = "https://oddreflex.pythonanywhere.com/";

    public IEnumerator SendURLShortenerRequest( string url, bool generate = true, Action<string> successCallback = null, Action<string> failedCallback = null )
    {
        if( cachedRequests.TryGetValue( url, out string data ) )
        {
            successCallback?.Invoke( data );
            yield break;
        }

        UnityWebRequest webRequest;

        if( generate )
        {
            WWWForm form = new WWWForm();
            form.AddField( "url", url );
            url = URLShortenerAddress;
            webRequest = UnityWebRequest.Post( url, form );
        }
        else
        {
            webRequest = UnityWebRequest.Get( url );
        }

        cachedRequests[url] = string.Empty;

        // Request and wait for the desired page.
        yield return webRequest.SendWebRequest();

        switch( webRequest.result )
        {
            case UnityWebRequest.Result.ConnectionError:
            case UnityWebRequest.Result.DataProcessingError:
                Debug.LogError( url + ": Error: " + webRequest.error );
                break;
            case UnityWebRequest.Result.ProtocolError:
            case UnityWebRequest.Result.Success:
                var result = generate ?
                    JsonConvert.DeserializeObject<URLShortenerResult>( webRequest.downloadHandler.text ).url :
                    Uri.UnescapeDataString( webRequest.uri.AbsoluteUri );
                cachedRequests[url] = result;
                successCallback?.Invoke( result );
                break;
        }
    }

    public IEnumerator DownloadImage( string uri, bool waitForRateLimit, Action<Texture2D> callback )
    {
        if( !waitForRateLimit && !rateLimiter.AttemptCall() )
            yield return null;

        yield return rateLimiter.WaitForCall( DownloadImageInternal( uri, callback ) );
    }

    private IEnumerator DownloadImageInternal( string uri, Action<Texture2D> callback )
    {
        using( UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture( uri ) )
        {
            yield return webRequest.SendWebRequest();

            switch( webRequest.result )
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError( uri + ": Error: " + webRequest.error );
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    //Debug.LogError( uri + ": HTTP Error: " + webRequest.error );
                    break;
                case UnityWebRequest.Result.Success:
                    callback?.Invoke( DownloadHandlerTexture.GetContent( webRequest ) );
                    break;
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
}