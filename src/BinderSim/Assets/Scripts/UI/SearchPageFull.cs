using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SearchPageFull : SearchPageBase
{
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] Button advancedSearchButton = null;
    [SerializeField] Button importFromFileButton = null;
    [SerializeField] TMPro.TMP_Dropdown optionsDropdown = null;

    protected override void Start()
    {
        base.Start();

        optionsDropdown.onValueChanged.AddListener( ( x ) =>
        {
            SearchCards();
        } );
    }

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
        else if( e is PageFullEvent )
        {
            Debug.Assert( behaviour == SearchPageBehaviour.AddingCards );
            behaviour = SearchPageBehaviour.AddingCardsPageFull;
        }
    }

    private void ShowPage( SearchPageBehaviour newBehaviour, int? binderIndex )
    {
        behaviour = newBehaviour;

        searchListPage.SetActive( true );

        bool inventoryMode = behaviour == SearchPageBehaviour.Inventory || behaviour == SearchPageBehaviour.InventoryFromCardPage;
        advancedSearchButton.gameObject.SetActive( !inventoryMode );
        importFromFileButton.gameObject.SetActive( inventoryMode );

        if( currentCardSelectedIdx != null )
            GetSelectedCard().GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( null );

        List<string> options = new();

        foreach( var (val, str) in Utility.GetEnumValues<InventoryData.Options>().Zip( InventoryData.optionStrings ) )
        {
            if( val == InventoryData.Options.CardsInBinderX )
                options.AddRange( BinderPage.Instance.BinderData.Select( ( x ) => string.Format( str, x.data.name ) ) );
            else
                options.Add( str );
        }

        optionsDropdown.ClearOptions();
        optionsDropdown.AddOptions( options );

        // Set default
        optionsDropdown.SetValueWithoutNotify(
            binderIndex != null
                ? ( int )InventoryData.Options.CardsInBinderX + binderIndex.Value
                : inventoryMode 
                ? ( int )InventoryData.Options.AllCards 
                : ( int )InventoryData.Options.SearchOnline );
    }
}