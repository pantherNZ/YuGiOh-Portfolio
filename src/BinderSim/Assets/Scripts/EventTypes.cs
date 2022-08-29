﻿public class BinderLoadedEvent : IBaseEvent { }
public class PageChangeRequestEvent : IBaseEvent { public PageType page; }
public class OpenCardPageEvent : PageChangeRequestEvent 
{
    public OpenCardPageEvent() { page = PageType.CardPage; }
    public BinderDataRuntime binder; 
}

public class BinderDataUpdateEvent : IBaseEvent { public BinderData binder; }
public class CardSelectedEvent : IBaseEvent 
{ 
    public CardDataRuntime card;
    public bool fromDragDrop = false;
}

public class CardImageLoadedEvent : IBaseEvent { public CardDataRuntime card; }

public class OpenSearchPageEvent : PageChangeRequestEvent
{
    public SearchPageBehaviour behaviour;
    public bool pageFull;
}

public class OpenInventoryPageEvent : OpenSearchPageEvent
{
    public OpenInventoryPageEvent() 
    { 
        page = PageType.SearchPageFull;
        behaviour = SearchPageBehaviour.InventoryFromCardPage;
    }
    public int currentBinderIdx;
}

public class PageFullEvent : IBaseEvent { }

public class SearchEntryDragDropComplete : IBaseEvent { }