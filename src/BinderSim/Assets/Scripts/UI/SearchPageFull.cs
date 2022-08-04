using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.IO;
using Newtonsoft.Json;

public class SearchPageFull : SearchPageBase
{
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] Button clearCardButton = null;

    protected override GameObject AddCardUI( CardData card, int entryIdx )
    {
        // Add UI elements
        var newCardUIEntry = Instantiate( cardEntryPrefab );
        newCardUIEntry.transform.SetParent( cardList.transform );

        var texts = newCardUIEntry.GetComponentsInChildren<TMPro.TextMeshProUGUI>();
        texts[0].text = card.name;

        return newCardUIEntry;
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is OpenSearchPageEvent openPageRequest )
        {
            if( !openPageRequest.openFullPage )
            {
                searchListPage.SetActive( false );
                return;
            }

            selectCardButton.interactable = openPageRequest.behaviour != SearchPageBehaviour.AddingCardsPageFull;
            clearCardButton.interactable = openPageRequest.behaviour == SearchPageBehaviour.ReplacingCard;
            selectCardButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text =
                openPageRequest.behaviour == SearchPageBehaviour.SettingCard ||
                openPageRequest.behaviour == SearchPageBehaviour.ReplacingCard ?
                "Select Card" : "Add Card";

            behaviour = openPageRequest.behaviour;
            searchListPage.SetActive( true );

            if( currentCardSelectedIdx != null )
                GetSelectedCard().GetComponent<EventDispatcher>().OnPointerUpEvent.Invoke( null );
        }
        else if( e is PageFullEvent )
        {
            Debug.Assert( behaviour == SearchPageBehaviour.AddingCards );
            behaviour = SearchPageBehaviour.AddingCardsPageFull;
            selectCardButton.interactable = false;
        }
    }
}