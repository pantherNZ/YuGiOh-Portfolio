using System;
using System.Collections.Generic;
using UnityEngine;

public class Constants
{
    public static int DefaultStartingNumPages = 20;
    public static int DefaultStartingPageWidth = 3;
    public static int DefaultStartingPageHeight = 3;
}

public enum PageType
{
    BinderPage,
    CardPage,
}

public class BinderData
{
    public string name;
    public DateTime dateCreated;
    public int pageCount;
    public int pageWidth;
    public int pageHeight;
    public Dictionary<int, List<CardDataRuntime>> cardList;
    public string imagePath;
}

public class BinderDataRuntime : BinderData
{
    public GameObject binderUI;
}

public class CardData
{
    public string name;
    public int cardId;
    public int imageId;
}

public class CardDataRuntime : CardData
{
    public Datum cardAPIData;
    public Texture2D smallImage;
    public Texture2D largeImage;
}