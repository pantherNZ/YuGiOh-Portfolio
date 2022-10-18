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

        var searchEntry = newCardUIEntry.GetComponent<SearchListEntry>();
        searchEntry.AddButton?.gameObject.SetActive( card.cardAPIData != null && IsAddButtonActive() );
        searchEntry.RemoveButton?.gameObject.SetActive( card.cardAPIData != null && IsRemoveButtonActive() );

        if( IsAddButtonActive() )
        {
            searchEntry.AddButton?.onClick.AddListener( () =>
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
            searchEntry.RemoveButton?.onClick.AddListener( () =>
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

        GetSelectedCard()?.GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( null );
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
        if( behaviour == SearchPageOrigin.MainPage
            && GetDropDownOption() != InventoryData.Options.SearchOnline
            && GetDropDownOption() != InventoryData.Options.TempInventory )
            return true;

        if( currentBinderIdx != null && 
            GetDropDownOptionIdx() - ( int )InventoryData.Options.CardsInBinderX == currentBinderIdx.Value )
            return true;

        return false;
    }

    bool IsAddButtonActive()
    {
        if( behaviour != SearchPageOrigin.MainPage
            || GetDropDownOption() == InventoryData.Options.SearchOnline
            || GetDropDownOption() == InventoryData.Options.TempInventory )
        {
            return currentBinderIdx == null ||
                GetDropDownOptionIdx() - ( int )InventoryData.Options.CardsInBinderX != currentBinderIdx.Value;
        }

        return false;
    }

}