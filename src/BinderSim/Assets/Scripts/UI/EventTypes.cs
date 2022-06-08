using System;
using System.Collections.Generic;

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
}

public class BinderLoadedEvent : IBaseEvent {  }
public class PageChangeRequestEvent : IBaseEvent { public PageType page; }
public class BinderDataUpdateEvent : IBaseEvent {  }