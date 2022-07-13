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

    private RateLimiter rateLimiter = new( 20, TimeSpan.FromSeconds( 1.0 ) );
    public RateLimiter RateLimiterInst => rateLimiter;

    private Dictionary<string, string> cachedRequests = new();

    // https://db.ygoprodeck.com/api-guide/
    public IEnumerator SendCardSearchRequest( string cardName, bool waitForRateLimit, Action<string> callback = null )
    {
        var url = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?name={0}", cardName );
        return SendGetRequest( url, waitForRateLimit, callback );
    }

    public IEnumerator SendCardSearchRequest( int cardId, bool waitForRateLimit, Action<string> callback = null )
    {
        var url = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?id={0}", cardId );
        return SendGetRequest( url, waitForRateLimit, callback );
    }

    public IEnumerator SendCardSearchRequestFuzzy( string cardName, bool waitForRateLimit, Action<string> callback = null )
    {
        var url = String.Format( "https://db.ygoprodeck.com/api/v7/cardinfo.php?fname={0}", cardName );
        return SendGetRequest( url, waitForRateLimit, callback );
    }

    public IEnumerator SendGetRequest( string url, bool waitForRateLimit, Action<string> callback = null )
    {
        if( !waitForRateLimit && !rateLimiter.AttemptCall() )
            yield return null;

        yield return rateLimiter.WaitForCall( SendGetRequestInternal( url, callback ) );
    }

    private IEnumerator SendGetRequestInternal( string url, Action<string> callback = null )
    {
        if( cachedRequests.TryGetValue( url, out string data ) )
        {
            callback?.Invoke( data );
            yield return null;
        }    

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
                    cachedRequests.Add( url, webRequest.downloadHandler.text );
                    callback?.Invoke( webRequest.downloadHandler.text );
                    break;
            }
        }
    }

    public IEnumerator DownloadImage( string url, bool waitForRateLimit, Action<Texture2D> callback )
    {
        if( !waitForRateLimit && !rateLimiter.AttemptCall() )
            yield return null;

        yield return rateLimiter.WaitForCall( DownloadImageInternal( url, callback ) );

    }

    private IEnumerator DownloadImageInternal( string url, Action<Texture2D> callback )
    {
        using( UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture( url ) )
        {
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
                    callback?.Invoke( DownloadHandlerTexture.GetContent( webRequest ) );
                    break;
            }
        }
    }


    // TODO: Save/cache image
    //byte[] results = request.downloadHandler.data;
    //string filename = gameObject.name + ".dat";
    //SaveImage( "Images/" + filename, results );

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