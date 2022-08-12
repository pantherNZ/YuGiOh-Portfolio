using UnityEngine;
using UnityEngine.UI;

public class SearchPageFull : SearchPageBase
{
    [SerializeField] GameObject cardEntryPrefab = null;

    protected override GameObject AddCardUI( CardData card, int entryIdx )
    {
        // Add UI elements
        var newCardUIEntry = Instantiate( cardEntryPrefab );
        newCardUIEntry.transform.SetParent( cardList.transform );

        var texts = newCardUIEntry.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = card.name;

        return newCardUIEntry;
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        base.OnEventReceived( e );

        if( e is OpenSearchPageEvent openPageRequest )
        {
            if( !openPageRequest.openFullPage )
            {
                searchListPage.SetActive( false );
                return;
            }

            behaviour = openPageRequest.behaviour;
            searchListPage.SetActive( true );

            if( currentCardSelectedIdx != null )
                GetSelectedCard().GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( null );
        }
        else if( e is PageFullEvent )
        {
            Debug.Assert( behaviour == SearchPageBehaviour.AddingCards );
            behaviour = SearchPageBehaviour.AddingCardsPageFull;
        }
    }
}