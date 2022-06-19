using System;
using System.Collections.Generic;

[Serializable]
public class CardImage
{
    public int id;
    public string image_url;
    public string image_url_small;
}

[Serializable]
public class CardPrice
{
    public string cardmarket_price;
    public string tcgplayer_price;
    public string ebay_price;
    public string amazon_price;
    public string coolstuffinc_price;
}

[Serializable]
public class CardSet
{
    public string set_name;
    public string set_code;
    public string set_rarity;
    public string set_rarity_code;
    public string set_price;
}

[Serializable]
public class Datum
{
    public int id;
    public string name;
    public string type;
    public string desc;
    public int atk;
    public int def;
    public int level;
    public string race;
    public string attribute;
    public string archetype;
    public List<CardSet> card_sets;
    public List<CardImage> card_images;
    public List<CardPrice> card_prices;
    public int? scale;
}

[Serializable]
public class Root
{
    public List<Datum> data;
}