using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SearchListEntry : MonoBehaviour
{
    [SerializeField] Image cardImage;
    [SerializeField] Button leftCardImageButton;
    [SerializeField] Button rightCardImageButton;
    [SerializeField] Image backgroundImage;
    [SerializeField] TMPro.TextMeshProUGUI titleText;
    [SerializeField] TMPro.TextMeshProUGUI typeText;
    [SerializeField] TMPro.TextMeshProUGUI rarityText;
    [SerializeField] TMPro.TMP_Dropdown setDropdown;
    [SerializeField] TMPro.TMP_Dropdown conditionDropdown;
    [SerializeField] TMPro.TextMeshProUGUI countText;
    [SerializeField] Button addButton;
    [SerializeField] Button removeButton;

    public Button AddButton { get => addButton; private set { } }
    public Button RemoveButton { get => removeButton; private set { } }

    private CardDataRuntime cardData;

    public void Initialise( CardDataRuntime data )
    {
        cardData = data;
        SetBackgroundColour( Color.clear );

        titleText.text = data.name;

        bool dataValid = data != null && data.cardAPIData != null;
        leftCardImageButton?.gameObject.SetActive( dataValid && data.cardAPIData.card_images.Count > 1 );
        rightCardImageButton?.gameObject.SetActive( dataValid && data.cardAPIData.card_images.Count > 1 );
        cardImage?.gameObject.SetActive( dataValid );
        setDropdown?.gameObject.SetActive( dataValid );
        conditionDropdown?.gameObject.SetActive( dataValid );
        rarityText?.gameObject.SetActive( dataValid );
        countText?.gameObject.SetActive( dataValid && data.count > 1 );

        if( data == null || data.cardAPIData == null )
            return;

        leftCardImageButton?.onClick.AddListener( () => LoadArtVariation( cardData.imageIndex - 1 ) );
        rightCardImageButton?.onClick.AddListener( () => LoadArtVariation( cardData.imageIndex + 1 ) );
        typeText?.SetText( data.cardAPIData.type );
        countText?.SetText( data.count.ToString() );

        if( conditionDropdown != null )
        {
            conditionDropdown.AddOptions( CardConditions.GetValues().ToList() );
            conditionDropdown.SetValueWithoutNotify( ( int )CardConditions.Values.NearMint );
            conditionDropdown.onValueChanged.AddListener( idx =>
            {
                data.condition = ( CardConditions.Values )idx;
            } );
        }

        if( setDropdown != null )
        {
            var options =
                data.cardAPIData.card_sets == null
                ? new List<string>() { "Unreleased" }
                : data.cardAPIData.card_sets.Select( x => string.Format( "{0} ({1})", x.set_name, x.set_code ) ).ToList();

            setDropdown.AddOptions( options );
            setDropdown.SetValueWithoutNotify( 0 );
            setDropdown.onValueChanged.AddListener( idx =>
            {
                data.cardIndex = idx;
                UpdateUI();
            } );
        }

        UpdateUI();
        GetCardPreviewImage();
    }

    private void UpdateUI()
    {
        rarityText?.SetText( cardData.cardAPIData.card_sets != null 
            ? cardData.cardAPIData.card_sets[cardData.cardIndex].set_rarity
            : "Unknown" );
    }

    private void GetCardPreviewImage()
    {
        cardData.imageIndex = 0;

        if( Constants.Instance.DownloadImages && cardData.cardAPIData != null )
        {
            if( cardData.smallImages != null )
            {
                Debug.Assert( cardData.imageIndex == 0 || cardData.artVariationsRequested );
                OnImageDownloaded( 0, cardData.smallImages[0] );
            }
            else
            {
                cardData.smallImages = new Texture2D[1];
                var smallImageUrl = cardData.cardAPIData.card_images[cardData.imageIndex].image_url_small;
                StartCoroutine( APICallHandler.Instance.DownloadImage( smallImageUrl, true, ( texture ) => OnImageDownloaded( 0, texture ) ) );
            }
        }
    }

    public void SetBackgroundColour( Color colour )
    {
        backgroundImage.color = colour;
    }

    public void SetCardSprite( int index )
    {
        Debug.Assert( cardData.smallImages != null && index < cardData.smallImages.Length );
        cardImage.sprite = Utility.CreateSprite( cardData.smallImages[index] );
        cardData.imageIndex = index;
        cardData.largeImageRequested = false;
    }

    private void LoadArtVariation( int index )
    {
        index = Utility.Mod( index, cardData.cardAPIData.card_images.Count );

        if( cardData.imageIndex == index )
            return;

        cardData.imageIndex = index;
        cardData.cardId = cardData.cardAPIData.card_images[index].id;
        bool noData = cardData.smallImages == null || index >= cardData.smallImages.Length;

        if( !Constants.Instance.DownloadImages )
            return;

        if( cardData.artVariationsRequested && noData )
            return;

        if( noData )
        {
            cardData.artVariationsRequested = true;
            var prevImage = cardData.smallImages?[0];
            cardData.smallImages = new Texture2D[cardData.cardAPIData.card_images.Count];
            cardData.smallImages[0] = prevImage;

            for( int i = 0; i < cardData.smallImages.Length; ++i )
            {
                int idx = i;
                var smallImageUrl = cardData.cardAPIData.card_images[i].image_url_small;
                StartCoroutine( APICallHandler.Instance.DownloadImage( smallImageUrl, true, ( texture ) => OnImageDownloaded( idx, texture ) ) );
            }
        }
        else
        {
            SetCardSprite( index );
        }
    }

    private void OnImageDownloaded( int index, Texture2D texture )
    {
        cardData.smallImages[index] = texture;

        if( cardData.imageIndex == index )
            SetCardSprite( index );
    }
}
