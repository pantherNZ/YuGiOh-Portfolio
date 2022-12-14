using System;
using System.Collections;
using System.Collections.Generic;

public class InventoryStorage : IEnumerable<CardDataRuntime>
{
    public InventoryStorage( bool triggerEvents = true )
    {
        shouldTriggerEvents = triggerEvents;
        data = new List<CardDataRuntime>();
    }

    public InventoryStorage( IEnumerable<CardDataRuntime> collection, bool triggerEvents = true )
    {
        shouldTriggerEvents = triggerEvents;
        data = new List<CardDataRuntime>( collection );
    }

    public InventoryStorage( List<CardDataRuntime> collection, bool triggerEvents = true )
    {
        shouldTriggerEvents = triggerEvents;
        data = collection;
    }

    public int Count { get => data.Count; }

    public void Add( CardDataRuntime card )
    {
        data.Add( card );

        if( shouldTriggerEvents )
        {
            EventSystem.Instance.TriggerEvent( new CardAddedToInventoryEvent()
            {
                card = card
            } );
        }
    }

    public void Remove( CardDataRuntime card )
    {
        data.Remove( card );

        if( shouldTriggerEvents )
        {
            EventSystem.Instance.TriggerEvent( new CardRemovedFromInventoryEvent()
            {
                card = card
            } );
        }
    }

    public void AddRange( IEnumerable<CardDataRuntime> collection )
    {
        data.AddRange( collection );
    }

    public int RemoveAll( Predicate<CardDataRuntime> match )
    {
        return data.RemoveAll( match );
    }

    public IEnumerator<CardDataRuntime> GetEnumerator()
    {
        return data.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return data.GetEnumerator();
    }

    public void Sort()
    {
        data.RemoveAll( ( x ) => x.cardAPIData == null || x.name == null );

        data.Sort( ( a, b ) =>
        {
            if( a.insideBinderIdx != b.insideBinderIdx )
                return ( a.insideBinderIdx == null ? -1 : a.insideBinderIdx.Value ).CompareTo( b.insideBinderIdx == null ? -1 : b.insideBinderIdx.Value );
            return a.name.CompareTo( b.name );
        } );
    }

    private List<CardDataRuntime> data;
    private readonly bool shouldTriggerEvents;
}