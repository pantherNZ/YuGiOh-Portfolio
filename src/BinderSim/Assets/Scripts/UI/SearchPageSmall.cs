using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.IO;
using Newtonsoft.Json;

public class SearchPageSmall : SearchPageBase
{
    [SerializeField] GameObject cardEntryPrefab = null;
    [SerializeField] Button clearCardButton = null;

    protected override GameObject AddCardUI( CardData card )
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
            if( openPageRequest.openFullPage )
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
        }
        else if( e is PageFullEvent )
        {
            Debug.Assert( behaviour == SearchPageBehaviour.AddingCards );
            behaviour = SearchPageBehaviour.AddingCardsPageFull;
            selectCardButton.interactable = false;
        }
    }

    public void SwitchSides()
    {
        var rectTransform = searchListPage.transform as RectTransform;
        var xPos = rectTransform.anchoredPosition.x;
        rectTransform.anchorMax = rectTransform.anchorMax.SetX( xPos >= 0.0f ? 1.0f : 0.0f );
        rectTransform.anchorMin = rectTransform.anchorMin.SetX( xPos >= 0.0f ? 1.0f : 0.0f );
        rectTransform.anchoredPosition = rectTransform.anchoredPosition.SetX( -xPos );
    }
}