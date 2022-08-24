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
}

public class CardImageLoadedEvent : IBaseEvent { public CardDataRuntime card; }

public enum SearchPageBehaviour
{
    None,
    SettingCard,
    ReplacingCard,
    AddingCards,
    AddingCardsPageFull,
    Inventory,
    InventoryFromCardPage,
}

public class OpenSearchPageEvent : PageChangeRequestEvent
{
    public SearchPageBehaviour behaviour;
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