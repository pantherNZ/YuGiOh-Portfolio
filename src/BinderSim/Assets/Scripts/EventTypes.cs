public class BinderLoadedEvent : IBaseEvent { }
public class PageChangeRequestEvent : IBaseEvent { public PageType page; }
public class OpenCardPageEvent : PageChangeRequestEvent 
{
    public OpenCardPageEvent() { page = PageType.CardPage; }
    public BinderData binder; 
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

public class PageFullEvent : IBaseEvent { }

public class SearchEntryDragDropComplete : IBaseEvent { }