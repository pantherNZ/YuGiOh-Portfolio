﻿public class BinderLoadedEvent : IBaseEvent { }
public class PageChangeRequestEvent : IBaseEvent { public PageType page; public BinderData binder; }
public class BinderDataUpdateEvent : IBaseEvent { public BinderData binder; }
public class CardSelectedEvent : IBaseEvent { public CardDataRuntime card; }
public class CardImageLoadedEvent : IBaseEvent { public CardDataRuntime card; }

public enum SearchPageBehaviour
{
    SettingCard,
    ReplacingCard,
    AddingCards,
    AddingCardsPageFull,
}

public class OpenSearchPageEvent : IBaseEvent 
{ 
    public SearchPageBehaviour behaviour; 
}

public class PageFullEvent : IBaseEvent { }


