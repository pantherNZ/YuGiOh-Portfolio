public class BinderLoadedEvent : IBaseEvent { }
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
    public bool fromInventory = false;
}

public class CardRemovedEvent : IBaseEvent 
{ 
    public CardDataRuntime card;
    public bool fromInventory = false;
}

public class CardImageLoadedEvent : IBaseEvent { public CardDataRuntime card; }

public class OpenSearchPageEvent : PageChangeRequestEvent
{
    public SearchPageOrigin behaviour;
    public SearchPageFlags flags;
    public string replacingCard;
    public string searchText;
    public int? currentBinderIdx;
}

public class CloseSearchPageEvent : PageChangeRequestEvent
{
    public CloseSearchPageEvent()
    {
        page = PageType.CardPage;
    }
    public bool? fromFullscreen;
}

public class OpenInventoryPageEvent : OpenSearchPageEvent
{
    public OpenInventoryPageEvent() 
    { 
        page = PageType.SearchPageFull;
    }
}

public class PageFullEvent : IBaseEvent { }

public class SearchEntryDragDropComplete : IBaseEvent { }

public class SaveGameEvent : IBaseEvent { }