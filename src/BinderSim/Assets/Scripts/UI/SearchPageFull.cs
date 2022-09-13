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
        addCardButton.gameObject.SetActive( card.cardAPIData != null && IsAddButtonActive() );
        removeCardButton.gameObject.SetActive( card.cardAPIData != null && IsRemoveButtonActive() );

        if( IsAddButtonActive() )
        {
            addCardButton.onClick.AddListener( () =>
            {
                // Main page adds the card to inventory
                if( behaviour == SearchPageOrigin.MainPage )
                {
                    BinderPage.Instance.Inventory.Add( cardData[entryIdx] );

                    if( GetDropDownOption() == InventoryData.Options.AllCards || GetDropDownOption() == InventoryData.Options.UnusedCards )
                        SearchCards();
                }
                else
                {
                    // Otherwise we add the card to the binder
                    currentCardSelectedIdx = entryIdx;
                    ChooseCard();
                }
            } );
        }
        
        if( IsRemoveButtonActive() )
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

    protected override void ShowPageInternal()
    {
        base.ShowPageInternal();

        if( currentCardSelectedIdx != null )
            GetSelectedCard().GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( null );
    }

    protected override void UpdateButtons()
    {
        base.UpdateButtons();

        bool inventoryNonSearchMode = GetDropDownOption() != InventoryData.Options.SearchOnline;
        advancedSearchButton.gameObject.SetActive( !inventoryNonSearchMode );
        importFromFileButton.gameObject.SetActive( inventoryNonSearchMode );
    }

    bool IsRemoveButtonActive()
    {
        return behaviour == SearchPageOrigin.MainPage 
            && GetDropDownOption() != InventoryData.Options.SearchOnline 
            && GetDropDownOption() != InventoryData.Options.TempInventory;
    }

    bool IsAddButtonActive()
    {
        return behaviour != SearchPageOrigin.MainPage 
            || GetDropDownOption() == InventoryData.Options.SearchOnline
            || GetDropDownOption() == InventoryData.Options.TempInventory;
    }

}