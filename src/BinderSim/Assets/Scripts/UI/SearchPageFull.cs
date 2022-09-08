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

        var buttons = newCardUIEntry.GetComponentsInChildren<Button>();
        var removeCardButton = buttons[0];
        var addCardButton = buttons[1];
        addCardButton.gameObject.SetActive( behaviour != SearchPageBehaviour.Inventory );
        removeCardButton.gameObject.SetActive( behaviour == SearchPageBehaviour.Inventory );

        if( behaviour != SearchPageBehaviour.Inventory )
        {
            addCardButton.onClick.AddListener( () =>
            {
                currentCardSelectedIdx = entryIdx;
                ChooseCard();
            } );
        }
        else
        {
            removeCardButton.onClick.AddListener( () =>
            {
                RemoveCard( card );
            } );
        }


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
            ShowPage( openPageRequest, openInventory?.currentBinderIdx );
        }
    }

    protected override void ShowPage( OpenSearchPageEvent request, int? binderIndex )
    {
        base.ShowPage( request, binderIndex );

        bool inventoryMode = behaviour == SearchPageBehaviour.Inventory
            || behaviour == SearchPageBehaviour.InventoryFromCardPage;

        advancedSearchButton.gameObject.SetActive( !inventoryMode );
        importFromFileButton.gameObject.SetActive( inventoryMode );

        if( currentCardSelectedIdx != null )
            GetSelectedCard().GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( null );
    }
}