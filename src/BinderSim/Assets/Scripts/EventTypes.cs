using System;
using System.Collections.Generic;

public class BinderLoadedEvent : IBaseEvent {  }
public class PageChangeRequestEvent : IBaseEvent { public PageType page; public BinderData? binder = null; }
public class BinderDataUpdateEvent : IBaseEvent {  }
public class CardSelectedEvent : IBaseEvent { public CardData card; }