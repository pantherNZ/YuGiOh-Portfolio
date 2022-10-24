using UnityEngine;
using UnityEngine.UI;

public class SearchPageFull : SearchPageBase
{
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] Button advancedSearchButton = null;
    [SerializeField] Button importFromFileButton = null;
    [SerializeField] GameObject entryOptionsPanel = null;
    [SerializeField] GameObject countHeaderText = null;

    private float scaleModifier = 1.0f;

    protected override void Start()
    {
        base.Start();

        entryOptionsPanel.SetActive( false );
    }

    protected override bool ContainsHeader()
    {
        return true;
    }

    protected override GameObject AddCardUI( CardDataRuntime card, int entryIdx )
    {
        // Add UI elements
        var newCardUIEntry = Instantiate( cardEntryPrefab );
        newCardUIEntry.transform.SetParent( cardList.transform );

        var searchEntry = newCardUIEntry.GetComponent<SearchListEntry>();
        searchEntry.Initialise( card, behaviour, flags, GetDropDownOption() );

        searchEntry.SettingsButton.onClick.AddListener( () =>
        {
            if( currentCardSelectedIdx != entryIdx )
                ToggleResultSelected( entryIdx );

            entryOptionsPanel.SetActive( true );
            var pos = ( searchEntry.SettingsButton.transform as RectTransform ).GetWorldRect().center;
            ( entryOptionsPanel.transform as RectTransform ).anchoredPosition = pos;

            var buttons = entryOptionsPanel.GetComponent<SearchListResultButtons>();
            buttons.AddToBinderButton.gameObject.SetActive( card.cardAPIData != null && IsAddToBinderButtonActive() );
            buttons.AddToInventoryButton.gameObject.SetActive( card.cardAPIData != null && IsAddToInventoryButtonActive() );
            buttons.RemoveButton.gameObject.SetActive( card.cardAPIData != null && IsRemoveButtonActive() );

            buttons.AddToBinderButton.onClick.RemoveAllListeners();
            buttons.AddToInventoryButton.onClick.RemoveAllListeners();
            buttons.RemoveButton.onClick.RemoveAllListeners();

            int buttonCount = 0;
            if( IsAddToInventoryButtonActive() )
            {
                ++buttonCount;
                buttons.AddToInventoryButton.onClick.AddListener( () =>
                {
                    BinderPage.Instance.Inventory.Add( cardData[entryIdx] );
                    BinderPage.Instance.SortInventory();

                    if( GetDropDownOption() == InventoryData.Options.AllCards || GetDropDownOption() == InventoryData.Options.UnusedCards )
                        SearchCards();

                    FixScaleModifier();
                } );
            }

            if( IsAddToBinderButtonActive() )
            {
                ++buttonCount;
                buttons.AddToBinderButton.onClick.AddListener( () =>
                {
                    currentCardSelectedIdx = entryIdx;
                    ChooseCard();
                    FixScaleModifier();
                } );
            }

            if( IsRemoveButtonActive() )
            {
                ++buttonCount;
                buttons.RemoveButton.onClick.AddListener( () =>
                {
                    RemoveCard( card );
                    FixScaleModifier();
                } );
            }

            scaleModifier = buttonCount / 3.0f;
            ( entryOptionsPanel.transform as RectTransform ).sizeDelta *= new Vector2( 1.0f, scaleModifier );
        } );

        var closePanelButton = entryOptionsPanel.GetComponentInChildren<Button>();
        closePanelButton.onClick.RemoveAllListeners();
        closePanelButton.onClick.AddListener( () =>
        {
            UnselectCurrentResult();
            FixScaleModifier();
        } );

        return newCardUIEntry;
    }

    private void FixScaleModifier()
    {
        ( entryOptionsPanel.transform as RectTransform ).sizeDelta *= new Vector2( 1.0f, 1.0f / scaleModifier );
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

        titleText.SetText(
            behaviour == SearchPageOrigin.MainPage
            ? "Modifying Inventory"
            : flags.HasFlag( SearchPageFlags.SettingCards )
            ? "Choosing Card"
            : flags.HasFlag( SearchPageFlags.ReplacingCard )
            ? "Replacing: " + replacingCardName
            : "Searching Cards" );
    }

    protected override void UpdateButtons()
    {
        base.UpdateButtons();

        bool inventoryNonSearchMode = GetDropDownOption() != InventoryData.Options.SearchOnline;
        advancedSearchButton.gameObject.SetActive( !inventoryNonSearchMode );
        importFromFileButton.gameObject.SetActive( inventoryNonSearchMode );
        countHeaderText.SetActive( inventoryNonSearchMode );
    }
    bool IsModifyingCurrentBinder()
    {
        return currentBinderIdx != null &&
            GetDropDownOptionIdx() - ( int )InventoryData.Options.CardsInBinderX == currentBinderIdx.Value;
    }

    bool IsRemoveButtonActive()
    {
        // main page AND not searching online and not in temp inventory (file inventory)
        if( behaviour == SearchPageOrigin.MainPage
            && GetDropDownOption() != InventoryData.Options.SearchOnline
            && GetDropDownOption() != InventoryData.Options.TempInventory )
            return true;

        // OR we are looking at the cards in our current binder
        if( IsModifyingCurrentBinder() )
            return true;

        return false;
    }

    bool IsAddToBinderButtonActive()
    {
        return behaviour != SearchPageOrigin.MainPage && !IsModifyingCurrentBinder();
    }

    bool IsAddToInventoryButtonActive()
    {
        // - not main page
        // - OR we are searching online
        // - OR we are searching a temp inventory( file inventory )
        if( behaviour != SearchPageOrigin.MainPage
            || GetDropDownOption() == InventoryData.Options.SearchOnline
            || GetDropDownOption() == InventoryData.Options.TempInventory )
        {
            // AND we aren't looking at the cards in our current binder
            return !IsModifyingCurrentBinder();
        }

        return false;
    }

}