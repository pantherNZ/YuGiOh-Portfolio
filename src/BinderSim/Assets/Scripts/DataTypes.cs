using System;
using System.Collections.Generic;

public class Constants
{
    public static int DefaultStartingNumPages = 20;
    public static int DefaultStartingPageWidth = 3;
    public static int DefaultStartingPageHeight = 3;
    public static int DefaultStartingNumCards = DefaultStartingNumPages * DefaultStartingPageWidth * DefaultStartingPageHeight;
}

public enum PageType
{
    MainMenu,
    BinderPage,
}

public struct BinderData
{
    public string name;
    public DateTime dateCreated;
    public int pageCount;
    public int pageWidth;
    public int pageHeight;
    public List<CardData> cardList;
    public string imagePath;
}

public struct CardData
{
    public string name;
    public int cardId;
    public string imagePath;
}