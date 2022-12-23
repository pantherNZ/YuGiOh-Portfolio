using System;
using UnityEngine;
using UnityEngine.EventSystems;

public static class AppUtility
{
    public static void LeftMouseFilter( bool instant, PointerEventData e, Action func, string inputPriorityKey = "MouseUp" )
    {
        if( instant && e.button == PointerEventData.InputButton.Left )
            func();
        else
            InputPriority.Instance.Request( () => e.button == PointerEventData.InputButton.Left, inputPriorityKey, 1, func );
    }

    public static void RightMouseFilter( bool instant, PointerEventData e, Action func, string inputPriorityKey = "MouseUp" )
    {
        if( instant && e.button == PointerEventData.InputButton.Right )
            func();
        else
            InputPriority.Instance.Request( () => e.button == PointerEventData.InputButton.Right, inputPriorityKey, 1, func );
    }

    public static Material GetCardMaterialFromRarity( CardDataRuntime card )
    {
        if( card == null || card.cardAPIData == null || card.cardAPIData.card_sets == null )
            return Constants.Instance.BaseCardMaterial;

        switch( card.GetRarityName() )
        {
            case "Super Rare":              return Constants.Instance.SecretRareMaterial; // TODO
            case "Rare":                    return Constants.Instance.BaseCardMaterial; // TODO
            case "Secret Rare":             return Constants.Instance.SecretRareMaterial;
            case "Prismatic Secret Rare":   return Constants.Instance.SecretRareMaterial; // TODO
            case "Ultra Rare":              return Constants.Instance.UltraRareMaterial;
            case "Ultimate Rare":           return Constants.Instance.UltraRareMaterial; // TODO
            case "Ghost Rare":              return Constants.Instance.GreyscaleMaterial; // TODO
            case "Starlight Rare":          return Constants.Instance.SecretRareMaterial; // TODO
            case "Common":
            default:                        return Constants.Instance.BaseCardMaterial;
        }
    }
}