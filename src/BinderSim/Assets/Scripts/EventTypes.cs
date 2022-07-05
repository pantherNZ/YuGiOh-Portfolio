public class BinderLoadedEvent : IBaseEvent {  }
public class PageChangeRequestEvent : IBaseEvent { public PageType page; public BinderData binder; }
public class BinderDataUpdateEvent : IBaseEvent { public BinderData binder; }
public class CardSelectedEvent : IBaseEvent { public CardDataRuntime card; }
public class CardImageLoadedEvent : IBaseEvent { public CardDataRuntime card; }
public class OpenSearchPageEvent : IBaseEvent { public bool existingCardIsEmpty; }