using System;
using System.Collections.Generic;

public class Constants
{
    public static int DefaultStartingNumPages = 20;
    public static int DefaultStartingPageWidth = 3;
    public static int DefaultStartingPageHeight = 3;
}

public enum PageType
{
    MainMenu,
    BinderPage,
}

public struct BinderData
{
    string name;
    DateTime dateCreated;
    int pageCount;
    int pageWidth;
    int pageHeight;
    List<UInt32> cardList;
    string imagePath;
}

public class BinderLoadedEvent : IBaseEvent {  }
public class PageChangeRequestEvent : IBaseEvent { public PageType page; }
public class BinderDataUpdateEvent : IBaseEvent {  }