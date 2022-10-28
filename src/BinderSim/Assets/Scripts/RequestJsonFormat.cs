using System;
using System.Collections.Generic;

[Serializable]
public class BanlistInfo
{
    public string ban_ocg;
    public string ban_tcg;
}

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
    public string race;
    public List<CardSet> card_sets;
    public List<CardImage> card_images;
    public List<CardPrice> card_prices;
    public List<MiscInfo> misc_info;
    public int? atk;
    public int? def;
    public int? level;
    public string attribute;
    public string archetype;
    public BanlistInfo banlist_info;
    public int? scale;
    public int? linkval;
    public List<string> linkmarkers;
}

[Serializable]
public class MiscInfo
{
    public int views;
    public int viewsweek;
    public int upvotes;
    public int downvotes;
    public List<string> formats;
    public string tcg_date;
    public string ocg_date;
    public int konami_id;
    public int has_effect;
    public int? beta_id;
    public string beta_name;
    public int? question_atk;
}

[Serializable]
public class Root
{
    public List<Datum> data;
}