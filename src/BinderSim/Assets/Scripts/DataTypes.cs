using System;
using System.Collections.Generic;
using UnityEngine;

public class Constants
{
    public const bool DownloadImages = true;
    public const bool DownloadLargeImages = true;
    public const int DefaultStartingNumPages = 20;
    public const int DefaultStartingPageWidth = 3;
    public const int DefaultStartingPageHeight = 3;
}

public enum PageType
{
    BinderPage,
    CardPage
}

public class BinderData
{
    public BinderData( 
        long id, 
        string name, 
        int pageCount = Constants.DefaultStartingNumPages, 
        int pageWidth = Constants.DefaultStartingPageWidth, 
        int pageHeight = Constants.DefaultStartingPageHeight, 
        string imagePath = null )
    {
        this.id = id;
        this.name = name;
        this.imagePath = imagePath;
        Resize( pageCount, pageWidth, pageHeight );
    }

    public void Resize( int pageCount, int pageWidth, int pageHeight )
    {
        this.pageCount = pageCount;
        this.pageWidth = pageWidth;
        this.pageHeight = pageHeight;

        if( cardList == null )
            cardList = new List<List<CardDataRuntime>>();
        cardList.Resize( pageCount, () => new List<CardDataRuntime>() );

        for( int i = 0; i < cardList.Count; ++i )
            cardList[i].Resize( pageWidth * pageHeight );
    }

    public void Insert( int page )
    {
        var newPage = new List<CardDataRuntime>();
        newPage.Resize( pageWidth * pageHeight );
        cardList.Insert( page, newPage );
    }

    public void Remove( int page )
    {
        cardList.RemoveAt( page );
    }

    public void Swap( int pageA, int pageB )
    {
        Debug.Assert( pageA != pageB );
        Debug.Assert( pageA >= 0 && pageA < cardList.Count );
        Debug.Assert( pageB >= 0 && pageB < cardList.Count );
        cardList.Swap( pageA, pageB );
    }

    public long id;
    public string name;
    public DateTime dateCreated;
    public int pageCount { get => cardList.Count; private set { } }
    public int pageWidth { get; private set; }
    public int pageHeight { get; private set; }
    public string imagePath;
    public List<List<CardDataRuntime>> cardList { get; private set; }
}

public class BinderDataRuntime
{
    public BinderData data;
    public GameObject binderUI;
}

public class CardData
{
    public string name;
    public int cardId;
    public int imageId;
    public float price; // TODO
    public int condition; // TODO
}

public class CardDataRuntime : CardData
{
    public Datum cardAPIData;
    public Texture2D smallImage;
    public Texture2D largeImage;
    public bool largeImageRequsted;
}