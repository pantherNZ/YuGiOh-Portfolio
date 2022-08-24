using UnityEngine;

public class ModifyPageButtons : EventReceiverInstance
{
    [SerializeField] CardPage cardPage;
    [SerializeField] GameObject buttonsPanel;
    [SerializeField] GameObject verticalLayout;
    [SerializeField] bool leftSide;

    public void Reset()
    {
        foreach( var( idx, child ) in Utility.Enumerate( verticalLayout.transform ) )
        {
            child.gameObject.SetActive( ( idx & 1 ) == 0 );
        }
    }

    public void ToggleModifyButtons()
    {
        buttonsPanel.ToggleActive();
        Reset();
    }

    public void AddPage( TMPro.TMP_InputField input )
    {
        if( int.TryParse( input.text, out var count ) )
            cardPage.AddPage( leftSide, count );
        ToggleModifyButtons();
    }

    public void MovePage( TMPro.TMP_InputField input )
    {
        if( int.TryParse( input.text, out var toIndex ) )
            cardPage.MovePage( leftSide, toIndex - 1 ); // Subtract 1 because users will count from first page being 1
        ToggleModifyButtons();
    }

    public void SwapPage( TMPro.TMP_InputField input )
    {
        if( int.TryParse( input.text, out var withIndex ) )
            cardPage.SwapPage( leftSide, withIndex - 1 ); // Subtract 1 because users will count from first page being 1
        ToggleModifyButtons();
    }

    public void RemovePage()
    {
        cardPage.RemovePage( leftSide );
        ToggleModifyButtons();
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is PageChangeRequestEvent pageChangeRequest && pageChangeRequest.page != PageType.CardPage )
        {
            Reset();
            buttonsPanel.SetActive( false );
        }
    }
}