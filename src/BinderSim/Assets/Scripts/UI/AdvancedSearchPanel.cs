using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class Archetype
{
    public string archetype_name;
}

public class SetInfo
{
    public string set_name;
    public string set_code;
    public int num_of_cards;
    public string tcg_date;
}

public enum SearchParam
{
    Format,
    Type,
    Attribute,
    Race,
    Archetype,
    Rarity,
    Cardset,
    Sort,
}

public class AdvancedSearchPanel : EventReceiverInstance
{
    [SerializeField] Button applyFiltersButton;
    [SerializeField] Button clearFiltersButton;
    [SerializeField] Button cancelButton;
    [SerializeField] TMPro.TMP_Dropdown formatDropdown;
    [SerializeField] TMPro.TMP_Dropdown typeDropdown;
    [SerializeField] TMPro.TMP_Dropdown attributeDropdown;
    [SerializeField] TMPro.TMP_Dropdown raceDropdown;
    [SerializeField] TMPro.TMP_Dropdown archetypeDropdown;
    [SerializeField] TMPro.TMP_MultiDropdown rarityDropdown;
    [SerializeField] TMPro.TMP_Dropdown tcgSetDropdown;
    [SerializeField] TMPro.TMP_Dropdown sortByDropdown;
    [SerializeField] Toggle sortDescendingToggle;

    private List<List<int>> rawSavedValues = new List<List<int>>();
    private CanvasGroup canvas;
    private bool dropdownDataRequested;

    static public List<string> sortDropDownOptions = new List<string>()
    {
        "Name",
        "Level",
        "Release Date"
    };

    protected override void Start()
    {
        base.Start();

        canvas = GetComponent<CanvasGroup>();

        clearFiltersButton.onClick.AddListener( ClearFilters );
        applyFiltersButton.onClick.AddListener( ApplyFilters );
        cancelButton.onClick.AddListener( Cancel );

        formatDropdown.onValueChanged.AddListener( SearchFiltersChanged );
        typeDropdown.onValueChanged.AddListener( SearchFiltersChanged );
        attributeDropdown.onValueChanged.AddListener( SearchFiltersChanged );
        raceDropdown.onValueChanged.AddListener( SearchFiltersChanged );
        archetypeDropdown.onValueChanged.AddListener( SearchFiltersChanged );
        rarityDropdown.onValuesChanged.AddListener( SearchFiltersChanged );
        tcgSetDropdown.onValueChanged.AddListener( SearchFiltersChanged );
        sortByDropdown.onValueChanged.AddListener( SearchFiltersChanged );
        sortDescendingToggle.onValueChanged.AddListener( x => SearchFiltersChanged( 0 ) );

        formatDropdown.AddOptions( new List<string>()
        {
            "Common Charity",
            "Duel Links",
            "Edison",
            "GOAT",
            "OCG GOAT",
            "Rush Duel",
            "Speed Duel",
            "TCG",
        } );

        typeDropdown.AddOptions( new List<string>()
        {
            "Effect Monster",
            "Flip Effect Monster",
            "Flip Tuner Effect Monster",
            "Fusion Monster",
            "Gemini Monster",
            "Link Monster",
            "Normal Monster",
            "Normal Tuner Monster",
            "Pendulum Effect Fusion Monster",
            "Pendulum Effect Monster",
            "Pendulum Effect Ritual Monster",
            "Pendulum Flip Effect Monster",
            "Pendulum Normal Monster",
            "Pendulum Tuner Effect Monster",
            "Ritual Effect Monster",
            "Ritual Monster",
            "Skill Card",
            "Spell Card",
            "Spirit Monster",
            "Synchro Monster",
            "Synchro Pendulum Effect Monster",
            "Synchro Tuner Monster",
            "Token",
            "Toon Monster",
            "Trap Card",
            "Tuner Monster",
            "Union Effect Monster",
            "XYZ Monster",
            "XYZ Pendulum Effect Monster",
        } );

        attributeDropdown.AddOptions( new List<string>()
        {
            "Dark",
            "Divine",
            "Earth",
            "Fire",
            "Light",
            "Water",
            "Wind"
        } );

        raceDropdown.AddOptions( new List<string>()
        {
            "Aqua",
            "Beast",
            "Beast-Warrior",
            "Continuous Spell",
            "Continuous Trap",
            "Counter Trap",
            "Creator-God",
            "Cyberse",
            "Dinosaur",
            "Divine-Beast",
            "Dragon",
            "Equip Spell",
            "Fairy",
            "Field Spell",
            "Fiend",
            "Fish",
            "Insect",
            "Machine",
            "Normal Spell",
            "Normal trap",
            "Plant",
            "Psychic",
            "Pyro",
            "Quick-Play Spell",
            "Reptile",
            "Ritual Spell",
            "Rock",
            "Sea Serpent",
            "Spellcaster",
            "Thunder",
            "Warrior",
            "Winged Beast",
            "Wyrm",
            "Zombie",
        } );

        rarityDropdown.AddOptions( new List<string>()
        {
            "10000 Secret Rare",
            "Collector's Rare",
            "Common",
            "Duel Terminal Normal Parallel Rare",
            "Duel Terminal Normal Rare Parallel Rare",
            "Duel Terminal Rare Parallel Rare",
            "Duel Terminal Secret Parallel Rare",
            "Duel Terminal Super Parallel Rare",
            "Duel Terminal Ultra Parallel Rare",
            "Extra Secret Parallel Rare",
            "Extra Secret Rare",
            "Ghost Rare",
            "Ghost/Gold Rare",
            "Gold Rare",
            "Gold Secret Rare",
            "Holographic Parallel Rare",
            "Holographic Rare",
            "Mosaic Rare",
            "Normal Parallel Rare",
            "Platinum Rare",
            "Platinum Secret Rare",
            "Premium Gold Rare",
            "Prismatic Secret Rare",
            "Rare",
            "Secret Parallel Rare",
            "Secret Rare",
            "Shatterfoil Rare",
            "Short Print",
            "Starfoil",
            "Starfoil Rare",
            "Starlight Rare",
            "Super Parallel Rare",
            "Super Rare",
            "Super Short Print",
            "Ultimate Rare",
            "Ultra Parallel Rare",
            "Ultra Rare",
            "Ultra Rare (Pharaoh's Rare)",
            "Ultra Secret Rare"
        } );

        sortByDropdown.AddOptions( sortDropDownOptions );

        ClearFilters();
        rawSavedValues = GenerateRawSearchParams();

        canvas.SetVisibility( false );
    }

    void ClearFilters()
    {
        formatDropdown.SetValueWithoutNotify( 0 );
        typeDropdown.SetValueWithoutNotify( 0 );
        attributeDropdown.SetValueWithoutNotify( 0 );
        raceDropdown.SetValueWithoutNotify( 0 );
        archetypeDropdown.SetValueWithoutNotify( 0 );
        rarityDropdown.SetValuesWithoutNotify( null );
        tcgSetDropdown.SetValueWithoutNotify( 0 );
        sortByDropdown.SetValueWithoutNotify( 0 );
        sortDescendingToggle.SetIsOnWithoutNotify( false );

        SearchFiltersChanged( 0 );
    }

    List<List<int>> GenerateRawSearchParams()
    {
        return new List<List<int>>()
        {
            //formatDropdown.value,
            //typeDropdown.value,
            //attributeDropdown.value,
            //raceDropdown.value,
            //archetypeDropdown.value,
            rarityDropdown.values.ToList(),
            //tcgSetDropdown.value,
            //sortByDropdown.value,
            //Convert.ToInt32( sortDescendingToggle.isOn )
        };
    }

    void ApplyFilters()
    {
        rawSavedValues = GenerateRawSearchParams();

        var searchParams = new Dictionary<SearchParam, List<string>>();

        foreach( var search in Utility.GetEnumValues<SearchParam>() )
            searchParams[search] = new List<string>();

        //if( formatDropdown.value != 0 ) searchParams += "&format=" + formatDropdown.options[formatDropdown.value].text.ToLower();
        //if( typeDropdown.value != 0 ) searchPBarams += "&type=" + typeDropdown.options[typeDropdown.value].text;
        //if( attributeDropdown.value != 0 ) searchParams += "&attribute=" + attributeDropdown.options[attributeDropdown.value].text;
        //if( raceDropdown.value != 0 ) searchParams += "&race=" + raceDropdown.options[raceDropdown.value].text;
        //if( archetypeDropdown.value != 0 ) searchParams += "&archetype=" + archetypeDropdown.options[archetypeDropdown.value].text;
        //
        foreach( var value in rarityDropdown.values )
            searchParams[SearchParam.Rarity]. Add( rarityDropdown.options[value].text );
       //if( rarityDropdown.values.Count > 0 ) 
       //if( tcgSetDropdown.value != 0 ) searchParams += "&cardset=" + tcgSetDropdown.options[tcgSetDropdown.value].text;
       //if( sortByDropdown.value != 0 ) searchParams += "&sort=" + sortByDropdown.options[sortByDropdown.value].text;
       //
        EventSystem.Instance.TriggerEvent( new UpdateAdvancedSearch()
        {
            searchParams = searchParams,
            searchDescending = sortDescendingToggle.isOn,
        } );

        SearchFiltersChanged( 0 );
        canvas.ToggleVisibility();
    }

    void Cancel()
    {
        if( rawSavedValues != null )
        {
            var values = rawSavedValues.GetEnumerator();
            //formatDropdown.value = values.Current;
            //typeDropdown.value = values.MoveNextGet();
            //attributeDropdown.value = values.MoveNextGet();
            //raceDropdown.value = values.MoveNextGet();
            //archetypeDropdown.value = values.MoveNextGet();
            rarityDropdown.values = values.MoveNextGet().AsReadOnly();
            //tcgSetDropdown.value = values.MoveNextGet();
            //sortByDropdown.value = values.MoveNextGet();
            //sortDescendingToggle.isOn = Convert.ToBoolean( values.MoveNextGet() );
        }
        else
        {
            ClearFilters();
        }

        canvas.ToggleVisibility();
    }

    void SearchFiltersChanged( List<int> _ )
    {

    }

    void SearchFiltersChanged( int _ )
    {
        var newParams = GenerateRawSearchParams();
        clearFiltersButton.interactable = newParams.Any( x => !x.IsEmpty() );
        applyFiltersButton.interactable = !newParams.SequenceEqual( rawSavedValues );
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is PageChangeRequestEvent pageChange && pageChange.page != PageType.SearchPageFull )
        {
            canvas.SetVisibility( false );
        }
        else if( e is AdvancedFiltersToggled )
        {
            canvas.ToggleVisibility();

            if( canvas.IsVisible() && !dropdownDataRequested )
            {
                dropdownDataRequested = true;

                StartCoroutine( APICallHandler.Instance.SendGetRequest( "https://db.ygoprodeck.com/api/v7/archetypes.php", true, archetypesJson =>
                {
                    List<Archetype> archetypes = JsonConvert.DeserializeObject<List<Archetype>>( archetypesJson );
                    var options = archetypes.ConvertAll( x => x.archetype_name.Replace( "\n", string.Empty ) );
                    options.Sort();
                    archetypeDropdown.AddOptions( options );
                } ) );

                StartCoroutine( APICallHandler.Instance.SendGetRequest( "https://db.ygoprodeck.com/api/v7/cardsets.php", true, setTypesJson =>
                {
                    List<SetInfo> sets = JsonConvert.DeserializeObject<List<SetInfo>>( setTypesJson );
                    var options = sets.ConvertAll( x => x.set_name.Replace( "\n", string.Empty ) );
                    options.Sort();
                    tcgSetDropdown.AddOptions( options );
                } ) );
            }
        }
    }
}
