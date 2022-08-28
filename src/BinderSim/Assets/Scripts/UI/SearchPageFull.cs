using UnityEngine;
using UnityEngine.UI;

public class SearchPageFull : SearchPageBase
{
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] Button advancedSearchButton = null;
    [SerializeField] Button importFromFileButton = null;

    protected override GameObject AddCardUI( CardDataRuntime card, int entryIdx )
    {
        // Add UI elements
        var newCardUIEntry = Instantiate( cardEntryPrefab );
        newCardUIEntry.transform.SetParent( cardList.transform );

        var texts = newCardUIEntry.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = card.name;

        newCardUIEntry.GetComponentInChildren<Button>().onClick.AddListener( () =>
        {
            currentCardSelectedIdx = entryIdx;
            ChooseCard();
        } );

        return newCardUIEntry;
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        base.OnEventReceived( e );

        if( e is OpenSearchPageEvent openPageRequest )
        {
            if( openPageRequest.page == PageType.SearchPage )
            {
                searchListPage.SetActive( false );
                return;
            }

            var openInventory = e as OpenInventoryPageEvent;
            ShowPage( openPageRequest.behaviour, openInventory?.currentBinderIdx );
        }
    }

    protected override void ShowPage( SearchPageBehaviour newBehaviour, int? binderIndex )
    {
        base.ShowPage( newBehaviour, binderIndex );

        bool inventoryMode = behaviour == SearchPageBehaviour.Inventory
            || behaviour == SearchPageBehaviour.InventoryFromCardPage;

        advancedSearchButton.gameObject.SetActive( !inventoryMode );
        importFromFileButton.gameObject.SetActive( inventoryMode );

        if( currentCardSelectedIdx != null )
            GetSelectedCard().GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( null );
    }
}