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
    [SerializeField] TMPro.TMP_MultiDropdown formatDropdown;
    [SerializeField] TMPro.TMP_MultiDropdown typeDropdown;
    [SerializeField] TMPro.TMP_MultiDropdown attributeDropdown;
    [SerializeField] TMPro.TMP_MultiDropdown raceDropdown;
    [SerializeField] TMPro.TMP_MultiDropdown archetypeDropdown;
    [SerializeField] TMPro.TMP_MultiDropdown rarityDropdown;
    [SerializeField] TMPro.TMP_MultiDropdown tcgSetDropdown;
    [SerializeField] TMPro.TMP_Dropdown sortByDropdown;
    [SerializeField] Toggle sortDescendingToggle;

    private List<List<int>> rawSavedValues = new List<List<int>>();
    private CanvasGroup canvas;

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

        formatDropdown.onValuesChanged.AddListener( SearchFiltersChanged );
        typeDropdown.onValuesChanged.AddListener( SearchFiltersChanged );
        attributeDropdown.onValuesChanged.AddListener( SearchFiltersChanged );
        raceDropdown.onValuesChanged.AddListener( SearchFiltersChanged );
        archetypeDropdown.onValuesChanged.AddListener( SearchFiltersChanged );
        rarityDropdown.onValuesChanged.AddListener( SearchFiltersChanged );
        tcgSetDropdown.onValuesChanged.AddListener( SearchFiltersChanged );
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
        formatDropdown.SetValuesWithoutNotify( null );
        typeDropdown.SetValuesWithoutNotify( null );
        attributeDropdown.SetValuesWithoutNotify( null );
        raceDropdown.SetValuesWithoutNotify( null );
        archetypeDropdown.SetValuesWithoutNotify( null );
        rarityDropdown.SetValuesWithoutNotify( null );
        tcgSetDropdown.SetValuesWithoutNotify( null );
        sortByDropdown.SetValueWithoutNotify( 0 );
        sortDescendingToggle.SetIsOnWithoutNotify( false );

        SearchFiltersChanged( 0 );
    }

    List<List<int>> GenerateRawSearchParams()
    {
        return new List<List<int>>()
        {
            formatDropdown.values.ToList(),
            typeDropdown.values.ToList(),
            attributeDropdown.values.ToList(),
            raceDropdown.values.ToList(),
            archetypeDropdown.values.ToList(),
            rarityDropdown.values.ToList(),
            tcgSetDropdown.values.ToList(),
            new List<int>(){ sortByDropdown.value },
            new List<int>(){ Convert.ToInt32( sortDescendingToggle.isOn ) }
        };
    }

    void ApplyFilters()
    {
        rawSavedValues = GenerateRawSearchParams();

        var searchParams = new Dictionary<SearchParam, List<string>>();

        foreach( var search in Utility.GetEnumValues<SearchParam>() )
            searchParams[search] = new List<string>();

        foreach( var value in formatDropdown.values )
            searchParams[SearchParam.Format].Add( formatDropdown.options[value].text );

        foreach( var value in typeDropdown.values )
            searchParams[SearchParam.Type].Add( typeDropdown.options[value].text );

        foreach( var value in attributeDropdown.values )
            searchParams[SearchParam.Attribute].Add( attributeDropdown.options[value].text );

        foreach( var value in raceDropdown.values )
            searchParams[SearchParam.Race].Add( raceDropdown.options[value].text );

        foreach( var value in archetypeDropdown.values )
            searchParams[SearchParam.Archetype].Add( archetypeDropdown.options[value].text );

        foreach( var value in rarityDropdown.values )
            searchParams[SearchParam.Rarity].Add( rarityDropdown.options[value].text );

        foreach( var value in tcgSetDropdown.values )
            searchParams[SearchParam.Cardset].Add( tcgSetDropdown.options[value].text );

        if( sortByDropdown.value != 0 )
            searchParams[SearchParam.Sort].Add( sortByDropdown.options[sortByDropdown.value].text );
       
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
            // https://codeblog.jonskeet.uk/2010/07/27/iterate-damn-you/
            // This is trash, why c# why
            IEnumerator<List<int>> values = rawSavedValues.GetEnumerator();
            formatDropdown.values = values.MoveNextGet().AsReadOnly();
            typeDropdown.values = values.MoveNextGet().AsReadOnly();
            attributeDropdown.values = values.MoveNextGet().AsReadOnly();
            raceDropdown.values = values.MoveNextGet().AsReadOnly();
            archetypeDropdown.values = values.MoveNextGet().AsReadOnly();
            rarityDropdown.values = values.MoveNextGet().AsReadOnly();
            tcgSetDropdown.values = values.MoveNextGet().AsReadOnly();
            sortByDropdown.value = values.MoveNextGet()[0];
            sortDescendingToggle.isOn = Convert.ToBoolean( values.MoveNextGet()[0] );
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

            if( canvas.IsVisible() && archetypeDropdown.options.Count <= 1 )
            {
                var archetypeOptions = APICallHandler.Instance.archetypes.ConvertAll( x => x.archetype_name.Replace( "\n", string.Empty ) );
                archetypeOptions.Sort();
                archetypeDropdown.AddOptions( archetypeOptions );

                var setOptions = APICallHandler.Instance.sets.ConvertAll( x => x.set_name.Replace( "\n", string.Empty ) );
                setOptions.Sort();
                tcgSetDropdown.AddOptions( setOptions );
            }
        }
    }
}
