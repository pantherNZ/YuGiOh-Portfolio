using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SearchListEntry : EventReceiverInstance
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
    [SerializeField] Button settingsButton;
    [SerializeField] Image settingsButtonIcon;
    [SerializeField] Button increaseCountButton;
    [SerializeField] Button decreaseCountButton;
    [SerializeField] Texture2D addTexture;
    [SerializeField] Texture2D removeTexture;

    public Button SettingsButton { get => settingsButton; private set { } }
    public Image SettingsButtonIcon { get => settingsButtonIcon; private set { } }
    public Texture2D AddTexture { get => addTexture; private set { } }
    public Texture2D RemoveTexture { get => removeTexture; private set { } }
    public TMPro.TextMeshProUGUI CountText { get => countText; private set { } }

    private CardDataRuntime cardData;
    private SearchPageOrigin pageBehaviour;
    private SearchPageFlags pageFlags;
    private InventoryData.Options pageMode;

    public void Initialise( CardDataRuntime data, SearchPageOrigin behaviour, SearchPageFlags flags, InventoryData.Options mode )
    {
        cardData = data;
        pageBehaviour = behaviour;
        pageFlags = flags;
        pageMode = mode;
        SetBackgroundColour( Color.clear );

        bool dataValid = data != null && data.cardAPIData != null;
        titleText.text = data.name;

        leftCardImageButton?.gameObject.SetActive( dataValid && data.cardAPIData.card_images.Count > 1 );
        rightCardImageButton?.gameObject.SetActive( dataValid && data.cardAPIData.card_images.Count > 1 );
        cardImage?.gameObject.SetActive( dataValid );
        setDropdown?.gameObject.SetActive( dataValid );
        conditionDropdown?.gameObject.SetActive( dataValid );
        typeText?.gameObject.SetActive( dataValid );
        rarityText?.gameObject.SetActive( dataValid );
        settingsButton?.gameObject.SetActive( dataValid );
        countText?.gameObject.SetActive( dataValid );
        increaseCountButton?.gameObject.SetActive( dataValid );
        decreaseCountButton?.gameObject.SetActive( dataValid && data.count > 1 );

        if( !dataValid )
            return;

        leftCardImageButton?.onClick.AddListener( () => LoadArtVariation( cardData.imageIndex - 1 ) );
        rightCardImageButton?.onClick.AddListener( () => LoadArtVariation( cardData.imageIndex + 1 ) );
        typeText?.SetText( data.cardAPIData.type );
        countText?.SetText( data.count.ToString() );

        if( conditionDropdown != null )
        {
            conditionDropdown.AddOptions( CardConditions.GetValues().ToList() );
            conditionDropdown.SetValueWithoutNotify( ( int )data.condition );
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
                : data.cardAPIData.card_sets.Select( x => string.Format( "{0} ({1}) - {2}", x.set_name, x.set_code, x.set_rarity ) ).ToList();

            setDropdown.AddOptions( options );
            setDropdown.SetValueWithoutNotify( data.cardIndex );
            setDropdown.onValueChanged.AddListener( idx =>
            {
                data.cardIndex = idx;
                UpdateUI();
            } );
        }

        decreaseCountButton?.onClick.AddListener( () =>
        {
            data.count--;
            UpdateUI();
        } );

        increaseCountButton?.onClick.AddListener( () =>
        {
            data.count++;
            UpdateUI();
        } );

        UpdateUI();
        UpdateCardText();
        GetCardPreviewImage();
    }

    private void UpdateUI()
    {
        rarityText?.SetText( cardData.GetRarityName() );
        cardData.count = Mathf.Clamp( cardData.count, 0, 32 );
        decreaseCountButton?.gameObject.SetActive( cardData.count > 1 );
        countText?.SetText( cardData.count.ToString() );
    }

    void UpdateCardText()
    {
        bool showCardInUse = cardData.insideBinderIdx != null && pageBehaviour != SearchPageOrigin.MainPage;
        int showCardOwnedCount = ( pageMode == InventoryData.Options.SearchOnline || pageMode == InventoryData.Options.TempInventory )
            ? BinderPage.Instance.Inventory.Count( x => x.cardId == cardData.cardId )
            : 0;

        titleText.text = cardData.name +
            ( showCardInUse
            ? $"\nIn Use: {BinderPage.Instance.BinderData[cardData.insideBinderIdx.Value].data.name}".ColourRGB( 0xA10000 )
            : showCardOwnedCount > 0
            ? $"\nOwned Copies: {showCardOwnedCount}".Navy()
            : String.Empty );

        if( showCardInUse && cardImage != null && cardImage.isActiveAndEnabled )
            cardImage.material = Constants.Instance.GreyscaleMaterial;
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

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is CardSelectedEvent cardSelected && cardSelected != null && cardSelected.card.cardId == cardData.cardId )
        {
            UpdateCardText();
        }
        else if( e is CardAddedToInventoryEvent cardAdded && cardAdded.card.cardId == cardData.cardId )
        {
            UpdateCardText();
        }
        else if( e is CardRemovedFromInventoryEvent cardRemoved && cardRemoved.card.cardId == cardData.cardId )
        {
            UpdateCardText();
        }
    }
}
