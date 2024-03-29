﻿using System.Collections.Generic;
using UnityEngine;

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
    public bool findEmptySlotInBinder = false;
}

public class CardRemovedEvent : IBaseEvent 
{ 
    public CardDataRuntime card;
    public bool fromInventory = false;
}

public class CardAddedToInventoryEvent : IBaseEvent
{
    public CardDataRuntime card;
}

public class CardRemovedFromInventoryEvent : IBaseEvent
{
    public CardDataRuntime card;
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

public class BinderFullEvent : IBaseEvent { }

public class SearchEntryDragDropComplete : IBaseEvent { }

public class SaveGameEvent : IBaseEvent { }

public class BinderChangeCardPage : IBaseEvent { public int newPage; }

public class BinderChangeCardPageRequest : IBaseEvent { public bool nextPage; }

public class BinderPopulateGrid : IBaseEvent { public int currentPage; }

public class StartDraggingEvent : IBaseEvent
{
    public int page;
    public int pos;
    public Rect colliderBoundsScreen;
    public bool doubleClick;
}

public class CardDoubleClickEvent : IBaseEvent
{
    public int page;
    public int pos;
}

public class UpdateAdvancedSearch : IBaseEvent
{
    public Dictionary<SearchParam, List<string>> searchParams;
    public bool searchDescending;
}

public class AdvancedFiltersToggled : IBaseEvent { }