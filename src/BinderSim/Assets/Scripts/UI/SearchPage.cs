using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SearchPage : EventReceiverInstance
{
    [SerializeField] GameObject cardList = null;
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] TMPro.TMP_InputField searchInput = null;
    [SerializeField] Button selectCardButton = null;
    [SerializeField] Color selectedEntryColour = new Color();

    private List<CardData> cardData = new List<CardData>();
    private int? currentCardSelectedIdx;

    public void SearchCards()
    {
        // Remove current card entries (skip/leave header)
        for( int i = 1; i < cardList.transform.childCount; ++i )
            cardList.transform.GetChild( i ).gameObject.Destroy();
        selectCardButton.interactable = false;

        // TODO: Get data, load cards
        LoadCard( new CardData()
        {
            name = "test",
            cardId = 5,
            imagePath = "test",
        } );
    }
    private GameObject GetSelectedCard()
    {
        return currentCardSelectedIdx.HasValue ? cardList.transform.GetChild( currentCardSelectedIdx.Value + 1 ).gameObject : null;
    }

    public void LoadCard( CardData card )
    {
        cardData.Add( card );

        // Add UI elements
        var newCardUIEntry = Instantiate( cardEntryPrefab );
        newCardUIEntry.transform.SetParent( cardList.transform );

        var texts = newCardUIEntry.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = cardData.Back().name;

        // On click
        var eventDispatcher = newCardUIEntry.GetComponent<EventDispatcher>();
        int thisIdx = cardData.Count - 1;

        eventDispatcher.OnPointerUpEvent += ( PointerEventData e ) =>
        {
            bool unselect = currentCardSelectedIdx == thisIdx;
            if( currentCardSelectedIdx != null || unselect )
                GetSelectedCard().GetComponent<Image>().color = new Color( 0.0f, 0.0f, 0.0f, 0.0f );
            if( !unselect )
                newCardUIEntry.GetComponent<Image>().color = selectedEntryColour;
            currentCardSelectedIdx = unselect ? null : thisIdx;
            selectCardButton.interactable = !unselect;
        };

        // TODO: Double click to choose
        eventDispatcher.OnDoubleClickEvent += ( PointerEventData e ) =>
        {

        };

        // TODO: Hover to show card image?
        eventDispatcher.OnPointerEnterEvent += ( PointerEventData e ) =>
        {

        };

        eventDispatcher.OnPointerExitEvent += ( PointerEventData e ) =>
        {

        };
    }

    public void ChooseCard()
    {
        Debug.Assert( currentCardSelectedIdx != null );
        EventSystem.Instance.TriggerEvent( new CardSelectedEvent()
        {
            card = cardData[currentCardSelectedIdx.Value]
        } );
    }

    public override void OnEventReceived( IBaseEvent e )
    {
    }
}