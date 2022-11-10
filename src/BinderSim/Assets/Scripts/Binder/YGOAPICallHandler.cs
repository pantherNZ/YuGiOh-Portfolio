using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

class APICallHandler
{
    static APICallHandler instance;
    public static APICallHandler Instance
    {
        get
        {
            if( instance == null )
                instance = new APICallHandler();
            return instance;
        }
    }

    private RateLimiter rateLimiter = new RateLimiter( 20, TimeSpan.FromSeconds( 1.0 ) );
    public RateLimiter RateLimiterInst => rateLimiter;

    private Dictionary<string, string> cachedRequests = new Dictionary<string, string>();
    private Dictionary<string, Texture2D> cachedImages = new Dictionary<string, Texture2D>();

    // https://db.ygoprodeck.com/api-guide/
    public IEnumerator SendCardSearchRequest( string cardName, bool waitForRateLimit, Action<string> callback = null )
    {
        var uri = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?name={0}&misc=yes", Uri.EscapeDataString( cardName ) );
        return SendGetRequest( uri, waitForRateLimit, callback );
    }

    public IEnumerator SendCardSearchRequest( int cardId, bool waitForRateLimit, Action<string> callback = null )
    {
        var uri = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?id={0}&misc=yes", cardId );
        return SendGetRequest( uri, waitForRateLimit, callback );
    }

    public IEnumerator SendCardSearchRequestFuzzy( string cardName, bool waitForRateLimit, Action<string> callback = null )
    {
        var uri = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?fname={0}&misc=yes", Uri.EscapeDataString( cardName ) );
        return SendGetRequest( uri, waitForRateLimit, callback );
    }

    public IEnumerator SendGetRequest( string uri, bool waitForRateLimit, Action<string> callback = null )
    {
        if( !waitForRateLimit )
        {
            if( rateLimiter.AttemptCall() )
                yield return SendGetRequestInternal( uri, callback );
        }
        else
        {
            yield return rateLimiter.WaitForCall( SendGetRequestInternal( uri, callback ) );
        }
    }

    private IEnumerator SendGetRequestInternal( string uri, Action<string> successCallback = null, Action<string> failedCallback = null )
    {
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

    public IEnumerator SendURLShortenerRequest( string url, Action<string> successCallback = null, Action<string> failedCallback = null )
    {
        var address = "http://oddreflex.pythonanywhere.com/";

        if( cachedRequests.TryGetValue( url, out string data ) )
        {
            successCallback?.Invoke( data );
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddField( "url", url );

        using( UnityWebRequest webRequest = UnityWebRequest.Post( address, form ) )
        {
            cachedRequests[url] = string.Empty;

            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch( webRequest.result )
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError( address + ": Error: " + webRequest.error );
                    break;
                case UnityWebRequest.Result.ProtocolError:
                case UnityWebRequest.Result.Success:
                    var result = JsonConvert.DeserializeObject<URLShortenerResult>( webRequest.downloadHandler.text );
                    cachedRequests[url] = result.url;
                    successCallback?.Invoke( result.url );
                    break;
            }
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