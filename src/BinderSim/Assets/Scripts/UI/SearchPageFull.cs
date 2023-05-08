using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SearchPageFull : SearchPageBase
{
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] Button advancedSearchButton = null;
    [SerializeField] CanvasGroup advancedSearchPanel = null;
    [SerializeField] Button importFromFileButton = null;
    [SerializeField] GameObject entryOptionsPanel = null;
    [SerializeField] GameObject countHeaderText = null;
    [SerializeField] TMPro.TextMeshProUGUI infoMessageText = null;
    [SerializeField] TextAsset cardsJsonData;

    private Vector2 buttonsSizeDelta;
    private Coroutine fadeOutCoroutine;
    private UpdateAdvancedSearch advancedSearchData;

    protected override void Start()
    {
        base.Start();

        entryOptionsPanel.SetActive( false );
        buttonsSizeDelta = ( entryOptionsPanel.transform as RectTransform ).sizeDelta;

        advancedSearchButton.onClick.AddListener( () => EventSystem.Instance.TriggerEvent( new AdvancedFiltersToggled() ) );
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

        if( card == null || card.cardAPIData == null )
            return newCardUIEntry;

        var eventDispatcher = newCardUIEntry.GetComponentInChildren<EventDispatcher>();
        var buttons = entryOptionsPanel.GetComponent<SearchListResultButtons>();

        eventDispatcher.OnPointerDownEvent += ( e ) => AppUtility.RightMouseFilter( true, e, () =>
        {
            ShowEntryOptionsPanel( card, entryIdx, Utility.GetMouseOrTouchPos(), buttons );
        } );

        var closePanelButton = entryOptionsPanel.GetComponentInChildren<Button>();
        closePanelButton.onClick.RemoveAllListeners();
        closePanelButton.onClick.AddListener( () =>
        {
            UnselectCurrentResult();
            FixScaleModifier();
        } );

        searchEntry.SettingsButton.onClick.AddListener( () =>
        {
            int buttonsCount = Convert.ToInt32( IsRemoveFromInventoryButtonActive( card ) )
                    + Convert.ToInt32( IsRemoveFromBinderButtonActive( card ) )
                    + Convert.ToInt32( IsAddToBinderButtonActive( card ) )
                    + Convert.ToInt32( IsAddToInventoryButtonActive( card ) );

            // More than 1 button active, use the settings button
            if( buttonsCount >= 2 )
            {
                var btnRect = ( searchEntry.SettingsButtonIcon.transform as RectTransform );
                var panelRect = ( entryOptionsPanel.transform as RectTransform );
                var pos = btnRect.GetWorldRect().center - new Vector2( panelRect.rect.width, btnRect.rect.height );
                ShowEntryOptionsPanel( card, entryIdx, pos, buttons );
            }
            else if( IsRemoveFromInventoryButtonActive( card ) )
            {
                RemoveFromInventoryButtonPressed( card, entryIdx );
            }
            else if( IsRemoveFromBinderButtonActive( card ) )
            {
                RemoveFromBinderButtonPressed( card, entryIdx );
            }
            else if( IsAddToBinderButtonActive( card ) )
            {
                AddBinderButtonPressed( card, entryIdx );
            }
            else if( IsAddToInventoryButtonActive( card ) )
            {
                AddInventoryButtonPressed( card, entryIdx );
            }
        } );

        int buttonsCount = Convert.ToInt32( IsRemoveFromInventoryButtonActive( card ) )
                    + Convert.ToInt32( IsRemoveFromBinderButtonActive( card ) )
                    + Convert.ToInt32( IsAddToBinderButtonActive( card ) )
                    + Convert.ToInt32( IsAddToInventoryButtonActive( card ) );
        if( buttonsCount == 1 )
        {
            searchEntry.SettingsButtonIcon.sprite = Utility.CreateSprite(
                IsRemoveFromInventoryButtonActive( card ) || IsRemoveFromBinderButtonActive( card )
                ? searchEntry.RemoveTexture
                : searchEntry.AddTexture );
        }

        return newCardUIEntry;
    }

    private void ShowEntryOptionsPanel( CardDataRuntime card, int entryIdx, Vector2 pos, SearchListResultButtons buttons )
    {
        if( currentCardSelectedIdx != entryIdx )
            ToggleResultSelected( entryIdx );

        entryOptionsPanel.SetActive( true );
        ( entryOptionsPanel.transform as RectTransform ).anchoredPosition = pos;

        buttons.AddToBinderButton.gameObject.SetActive( card.cardAPIData != null && IsAddToBinderButtonActive( card ) );
        buttons.AddToInventoryButton.gameObject.SetActive( card.cardAPIData != null && IsAddToInventoryButtonActive( card ) );
        buttons.RemoveFromInventoryButton.gameObject.SetActive( card.cardAPIData != null && IsRemoveFromInventoryButtonActive( card ) );
        buttons.RemoveFromBinderButton.gameObject.SetActive( card.cardAPIData != null && IsRemoveFromBinderButtonActive( card ) );

        buttons.AddToBinderButton.onClick.RemoveAllListeners();
        buttons.AddToInventoryButton.onClick.RemoveAllListeners();
        buttons.RemoveFromInventoryButton.onClick.RemoveAllListeners();
        buttons.RemoveFromBinderButton.onClick.RemoveAllListeners();

        int buttonCount = 0;
        if( IsAddToInventoryButtonActive( card ) )
        {
            ++buttonCount;
            buttons.AddToInventoryButton.onClick.AddListener( () =>
            {
                AddInventoryButtonPressed( card, entryIdx );
                FixScaleModifier();
            } );
        }

        if( IsAddToBinderButtonActive( card ) )
        {
            ++buttonCount;
            buttons.AddToBinderButton.onClick.AddListener( () =>
            {
                AddBinderButtonPressed( card, entryIdx );
                FixScaleModifier();
            } );
        }

        if( IsRemoveFromInventoryButtonActive( card ) )
        {
            ++buttonCount;
            buttons.RemoveFromInventoryButton.onClick.AddListener( () =>
            {
                RemoveFromInventoryButtonPressed( card, entryIdx );
                FixScaleModifier();
            } );
        }

        if( IsRemoveFromBinderButtonActive( card ) )
        {
            ++buttonCount;
            buttons.RemoveFromBinderButton.onClick.AddListener( () =>
            {
                RemoveFromBinderButtonPressed( card, entryIdx );
                FixScaleModifier();
            } );
        }

        FixScaleModifier();
        ( entryOptionsPanel.transform as RectTransform ).sizeDelta = buttonsSizeDelta * new Vector2( 1.0f, buttonCount / 4.0f );
    }

    private void AddBinderButtonPressed( CardDataRuntime card, int entryIdx )
    {
        currentCardSelectedIdx = entryIdx;
        ChooseCard();
    }

    private void AddInventoryButtonPressed( CardDataRuntime card, int entryIdx )
    {
        ShowInfoMessage( "Added to Inventory: ".Blue() + card.name.Black() );

        BinderPage.Instance.Inventory.Add( cardData[entryIdx].DeepCopy() );
        BinderPage.Instance.SortInventory();

        if( GetDropDownOption() == InventoryData.Options.AllCards || GetDropDownOption() == InventoryData.Options.UnusedCards )
            SearchCards();
    }

    private void RemoveFromInventoryButtonPressed( CardDataRuntime card, int entryIdx )
    {
        ShowInfoMessage( "Removed from Inventory: ".Blue() + card.name.Black() );

        BinderPage.Instance.Inventory.Remove( card );
        SearchCards();
    }

    private void RemoveFromBinderButtonPressed( CardDataRuntime card, int entryIdx )
    {
        ShowInfoMessage( "Removed from Binder: ".Blue() + card.name.Black() );

        EventSystem.Instance.TriggerEvent( new CardRemovedEvent()
        {
            card = card,
            fromInventory = FromInventory()
        } );
        SearchCards();
    }

    private void FixScaleModifier()
    {
        ( entryOptionsPanel.transform as RectTransform ).sizeDelta = buttonsSizeDelta;
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

            ShowPage( openPageRequest, openPageRequest.currentBinderIdx );
        }
        else if( e is UpdateAdvancedSearch updateAdvancedSearch )
        {
            advancedSearchData = updateAdvancedSearch;
            SearchCards();
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

        if( !inventoryNonSearchMode )
            advancedSearchPanel.SetVisibility( false );
    }

    bool IsModifyingCurrentBinder( CardDataRuntime card )
    {
        return currentBinderIdx != null && card.insideBinderIdx != null && card.insideBinderIdx == currentBinderIdx.Value;
    }

    bool IsAddToBinderButtonActive( CardDataRuntime card )
    {
        return behaviour != SearchPageOrigin.MainPage && !IsModifyingCurrentBinder( card );
    }

    bool IsAddToInventoryButtonActive( CardDataRuntime card )
    {
        return GetDropDownOption() == InventoryData.Options.SearchOnline
            || GetDropDownOption() == InventoryData.Options.TempInventory;
    }

    bool IsRemoveFromBinderButtonActive( CardDataRuntime card )
    {
        // We are looking at the cards in our current binder
        return behaviour != SearchPageOrigin.MainPage && IsModifyingCurrentBinder( card );
    }

    bool IsRemoveFromInventoryButtonActive( CardDataRuntime card )
    {
        if( IsAddToInventoryButtonActive( card ) )
            return false;
        if( behaviour != SearchPageOrigin.MainPage )
            return false;
        return !IsModifyingCurrentBinder( card ) || GetDropDownOption() != InventoryData.Options.UnusedCards;
    }

    protected override void ShowInfoMessage( string msg ) 
    {
        infoMessageText.gameObject.SetActive( true );
        infoMessageText.text = msg;
        infoMessageText.color = infoMessageText.color.SetA( 1.0f );
        if( fadeOutCoroutine != null )
            StopCoroutine( fadeOutCoroutine );
        fadeOutCoroutine = StartCoroutine( FadeOutInfoMessage() );
    }

    private IEnumerator FadeOutInfoMessage()
    {
        yield return new WaitForSeconds( 2.0f );
        yield return Utility.FadeToColour( infoMessageText, infoMessageText.color.SetA( 0.0f ), 2.0f );
        infoMessageText.gameObject.SetActive( false );
    }

    protected override bool FilterCard( string search, Datum card )
    {
        if( !base.FilterCard( search, card ) )
            return false;

        if( advancedSearchData == null )
            return true;

       if( advancedSearchData.searchParams.TryGetValue( SearchParam.Format, out var found ) )
            if( !found.IsEmpty() && !found.Any( format => card.misc_info.Any( info => info.formats.Contains( format ) ) ) )
                return false;

        if( advancedSearchData.searchParams.TryGetValue( SearchParam.Type, out found ) )
            if( !found.IsEmpty() && !found.Any( type => card.type == type ) )
                return false;

        if( advancedSearchData.searchParams.TryGetValue( SearchParam.Attribute, out found ) )
            if( !found.IsEmpty() && !found.Any( attribute => card.attribute == attribute ) )
                return false;

        if( advancedSearchData.searchParams.TryGetValue( SearchParam.Race, out found ) )
            if( !found.IsEmpty() && !found.Any( race => card.race == race ) )
                return false;

        if( advancedSearchData.searchParams.TryGetValue( SearchParam.Archetype, out found ) )
            if( !found.IsEmpty() && !found.Any( archetype => card.archetype == archetype ) )
                return false;

        if( advancedSearchData.searchParams.TryGetValue( SearchParam.Rarity, out found ) )
            if( !found.IsEmpty() && !found.Any( rarity => card.card_sets.Any( set => set.set_rarity == rarity ) ) )
                return false;

        if( advancedSearchData.searchParams.TryGetValue( SearchParam.Cardset, out found ) )
            if( !found.IsEmpty() && !found.Any( cardset => card.card_sets.Any( set => set.set_name == cardset ) ) )
                return false;

        return true;
    }

    protected override void SortAndAddResults( List<CardDataRuntime> results )
    {
        if( advancedSearchData == null )
        {
            base.SortAndAddResults( results );
            return;
        }

        if( advancedSearchData.searchParams.TryGetValue( SearchParam.Sort, out var found ) )
        {
            if( found.Count != 1 )
            {
                base.SortAndAddResults( results );
                return;
            }

            if( AdvancedSearchPanel.sortDropDownOptions[0] == found[0] )
            {
                Debug.Assert( found[0] == "Name" );
                // Base search will search by name
                base.SortAndAddResults( results );
                return;
            }
            else if( AdvancedSearchPanel.sortDropDownOptions[1] == found[0] )
            {
                Debug.Assert( found[0] == "Level" );
                results.Sort( ( x, y ) =>
                {
                    if( x.cardAPIData.level == y.cardAPIData.level )
                        return 0;
                    if( !y.cardAPIData.level.HasValue )
                        return 1;
                    if( !x.cardAPIData.level.HasValue )
                        return -1;
                    return x.cardAPIData.level.Value.CompareTo( y.cardAPIData.level.Value );
                } );
            }
            else if( AdvancedSearchPanel.sortDropDownOptions[2] == found[0] )
            {
                Debug.Assert( found[0] == "Release Date" );
                results.Sort( ( x, y ) =>
                {
                    if( x.cardAPIData.misc_info == null && y.cardAPIData.misc_info == null )
                        return 0;
                    if( y.cardAPIData.misc_info == null )
                        return 1;
                    if( x.cardAPIData.misc_info == null )
                        return -1;
                    return DateTime.Parse( x.cardAPIData.misc_info[0].tcg_date ).
                        CompareTo( DateTime.Parse( y.cardAPIData.misc_info[0].tcg_date ) );
                } );
            }

            if( advancedSearchData.searchDescending )
                results.Reverse();
        }

        foreach( var card in results )
            AddCard( card );
    }
}