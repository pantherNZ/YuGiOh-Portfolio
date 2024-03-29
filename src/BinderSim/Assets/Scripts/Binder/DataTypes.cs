﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

public enum PageType
{
    BinderPage,
    CardPage,
    SearchPage,
    SearchPageFull,
}

[Serializable]
public class BinderData
{
    public BinderData( 
        long id, 
        string name, 
        int? pageCount = null, 
        int? pageWidth = null, 
        int? pageHeight = null, 
        string imagePath = null,
        DateTime? dateCreated = null )
    {
        this.id = id;
        this.name = name;
        this.imagePath = imagePath;
        this.dateCreated = dateCreated ?? DateTime.Now;
        Resize( pageCount != null ? pageCount.Value : Constants.Instance.DefaultStartingNumPages
            , pageWidth != null ? pageWidth.Value : Constants.Instance.DefaultStartingPageWidth
            , pageHeight != null ? pageHeight.Value : Constants.Instance.DefaultStartingPageHeight );
    }

    public void Resize( int pageCount, int pageWidth, int pageHeight )
    {
        this.pageCount = pageCount;
        this.pageWidth = pageWidth;
        this.pageHeight = pageHeight;

        if( cardList == null )
            cardList = new List<List<CardDataRuntime>>();
        cardList.Resize( pageCount, () => new List<CardDataRuntime>() );

        for( int i = 0; i < cardList.Count; ++i )
            cardList[i].Resize( pageWidth * pageHeight );
    }

    public void Insert( int page )
    {
        var newPage = new List<CardDataRuntime>();
        newPage.Resize( pageWidth * pageHeight );

        if( page >= cardList.Count )
            Resize( page, pageWidth, pageHeight );

        cardList.Insert( page, newPage );
    }

    public void Move( int pageA, int pageB )
    {
        if( pageA != pageB &&
           pageA >= 0 && pageA < cardList.Count &&
           pageB >= 0 && pageB < cardList.Count )
        {
            cardList.Insert( pageB, cardList[pageA] );
            cardList.RemoveAt( pageB > pageA ? pageA : pageA - 1 );
        }
    }

    public void Remove( int page )
    {
        if( page >= 0 && page < cardList.Count )
            cardList.RemoveAt( page );
    }

    public void Swap( int pageA, int pageB )
    {
        if( pageA != pageB &&
            pageA >= 0 && pageA < cardList.Count &&
            pageB >= 0 && pageB < cardList.Count )
        {
            cardList.Swap( pageA, pageB );
        }
    }

    public long id;
    public string name;
    public DateTime dateCreated;
    public int pageCount { get => cardList.Count; private set { } }
    public int pageWidth { get; private set; }
    public int pageHeight { get; private set; }
    public string imagePath;
    public List<List<CardDataRuntime>> cardList { get; private set; }

    public int MaxCards { get { return pageCount * pageWidth * pageHeight; } private set { } }
    public int NumCards
    {
        get
        {
            int count = 0;
            foreach( var x in cardList )
                foreach( var y in x )
                    if( y != null )
                        count++;
            return count;
        }
        private set { }
    }
    public bool HasCards
    {
        get
        {
            foreach( var x in cardList )
                foreach( var y in x )
                    if( y != null )
                        return true;
            return false;
        }
        private set { }
    }
}

[Serializable]
public class BinderDataRuntime
{
    public void AddCards( List<CardDataRuntime> cards )
    {
        foreach( var (idx, card) in cards.Enumerate() )
        {
            var cardIndex = Utility.Mod( idx, data.pageWidth * data.pageHeight );
            var pageIndex = idx / ( data.pageWidth * data.pageHeight );

            if( pageIndex >= data.cardList.Count )
                data.Insert( pageIndex );

            card.insideBinderIdx = index;
            data.cardList[pageIndex][cardIndex] = card;
        }
    }

    public BinderData data;
    public GameObject binderUI;
    public int index;
}

[Serializable]
public class CardData
{
    public string name;
    public int cardId;    // JSON id
    public int cardIndex; // Index of the card within the variations (think 1 index for each different set)
    public int imageIndex; // Index of the card art within the variations (unrelated to set, no mapping unfortunately)
    public CardConditions.Values condition;
    public int count;
}

public class CardDataRuntime : CardData
{
    public Datum cardAPIData;
    public Texture2D[] smallImages;
    public Texture2D largeImage;
    public bool largeImageRequested;
    public bool artVariationsRequested;
    public int? insideBinderIdx;        // Index of the binder this card is a part of currently

    public CardDataRuntime DeepCopy()
    {
        return new CardDataRuntime()
        {
            // Base
            name = name,
            cardId = cardId,
            cardIndex = cardIndex,
            imageIndex = imageIndex,
            condition = condition,
            count = count,

            // Runtime
            cardAPIData = cardAPIData,
            smallImages = smallImages,
            largeImage = largeImage,
            largeImageRequested = largeImageRequested,
            artVariationsRequested = artVariationsRequested,
            insideBinderIdx = insideBinderIdx,
        };
    }

    public string GetRarityName( string unknownStr = "Unknown" )
    {
        return cardAPIData.card_sets != null
            ? cardAPIData.card_sets[cardIndex].set_rarity
            : unknownStr;
    }
}

public enum SearchPageOrigin
{
    None,
    MainPage,
    CardPageInventory,
    CardPageSearch,
}

[Flags]
public enum SearchPageFlags
{
    SettingCards = 1 << 1,
    ReplacingCard = 1 << 2,
    PageFull = 1 << 3,
    BinderFull = 1 << 4,
    FillAllPages = 1 << 5,
    RequestedFillAllPages = 1 << 6,
}

public class ImportData
{
    public string name;
    public int count;
    public float totalValue;
    public List<CardDataRuntime> cards = new List<CardDataRuntime>();

    public enum Options
    {
        CreatePopulatedBinder,
        AddToInventory,
        ReplaceInventory,
        AddToExistingBinderX,
        OptionsCount,
    }

    public static readonly ReadOnlyCollection<string> optionStrings = new ReadOnlyCollection<string>(
        new string[( int )Options.OptionsCount] 
    {
        "Create Populated Binder",
        "Add Cards To Inventory",
        "Replace Inventory",
        "Add Cards To {0}",
    } );
}

public static class InventoryData
{
    public enum Options
    {
        TempInventory,
        SearchOnline,
        AllCards,
        AllCardsInBinders,
        UnusedCards,
        CardsInBinderX,
        OptionsCount,
    }

    public static readonly ReadOnlyCollection<string> optionStrings = new ReadOnlyCollection<string>(
        new string[( int )Options.OptionsCount]
    {
        "Imported Cards",
        "Search Online",
        "All Owned Cards",
        "All Cards In Binders",
        "Unused Cards",
        "Cards In {0}",
    } );
}

public static class CardConditions
{
    public enum Values
    {
        NearMint,
        LightlyPlayed,
        ModeratelyPlayed,
        HeavilyPlayed,
        Damaged,
        MaxValues,
    }

    public static readonly ReadOnlyCollection<string> valueStrings = new ReadOnlyCollection<string>(
        new string[( int )Values.MaxValues]
    {
        "Near Mint",
        "Lightly Played",
        "Moderately Played",
        "Heavily Played",
        "Damaged",
    } );

    public static readonly ReadOnlyCollection<string> dragonShieldValues = new ReadOnlyCollection<string>(
        new string[]
    {
        "Mint",
        "NearMint",
        "Excellent",
        "Good",
        "LightPlayed",
        "Played",
        "Poor",
    } );

    public static readonly ReadOnlyCollection<Values> dragonShieldValueMappings = new ReadOnlyCollection<Values>(
        new Values[]
    {
        Values.NearMint,
        Values.NearMint,
        Values.NearMint,
        Values.LightlyPlayed,
        Values.LightlyPlayed,
        Values.ModeratelyPlayed,
        Values.HeavilyPlayed,
    } );

    public static Values ParseDragonShieldConditionString( string conditionStr )
    {
        var index = dragonShieldValues.IndexOf( conditionStr );
        return index != -1 ? dragonShieldValueMappings[index] : Values.NearMint;
    }

    public static Values ParseConditionString( string conditionStr )
    {
        conditionStr = conditionStr.Replace( " ", string.Empty );
        int found = valueStrings.IndexOf( conditionStr );

        if( found != -1 )
            return ( Values )found; 
        return Utility.ParseEnum<Values>( conditionStr, true );
    }

    public static string ParseConditionEnum( Values condition )
    {
        return valueStrings[( int )condition];
    }

    public static string[] GetValues()
    {
        return valueStrings.ToArray();
    }
}