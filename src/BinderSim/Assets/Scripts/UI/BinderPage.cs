using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BinderPage : EventReceiverInstance
{
    [SerializeField] GameObject binderPage = null;
    [SerializeField] AdvancedGridLayout cardsDisplayGridLeft = null;
    [SerializeField] AdvancedGridLayout cardsDisplayGridRight = null;
    [SerializeField] GameObject searchListPage = null;
    [SerializeField] TMPro.TMP_InputField binderNameText = null;
    [SerializeField] TMPro.TMP_InputField pageCountText = null;
    [SerializeField] TMPro.TMP_InputField pageSizeText = null;
    [SerializeField] TMPro.TextMeshProUGUI dateCreatedText = null;

    private BinderData currentbinder;
    private int width = Constants.DefaultStartingPageWidth;
    private int height = Constants.DefaultStartingPageHeight;
    private int currentPage;
    private int? currentModifyCardIdx;

    protected override void Start()
    {
        base.Start();
        binderPage.SetActive( false );
        searchListPage.SetActive( false );
    }

    public void SaveAndExit()
    {
        EventSystem.Instance.TriggerEvent( new PageChangeRequestEvent() { page = PageType.MainMenu } );
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is PageChangeRequestEvent pageChangeRequest )
        {
            switch( pageChangeRequest.page )
            {
                case PageType.MainMenu:
                    binderPage.SetActive( false );
                    break;
                case PageType.BinderPage:
                    binderPage.SetActive( true );
                    LoadBinder( pageChangeRequest.binder.Value );
                    break;
            }
        }
        else if( e is BinderDataUpdateEvent binderUpdateEvent )
        {
        }
        else if( e is CardSelectedEvent cardSelectedEvent )
        {
            searchListPage.SetActive( false );
            LoadCard( cardSelectedEvent.card );
        }
        else if( e is BinderLoadedEvent binderLoadedEvent )
        {
            //LoadBinder( binderLoadedEvent.data );
        }
    }

    private void LoadBinder( BinderData data )
    {
        currentbinder = data;
        currentPage = 0;
        binderNameText.text = currentbinder.name;
        pageCountText.text = data.pageCount.ToString();
        pageSizeText.text = string.Format( "{0}x{1}", data.pageWidth, data.pageHeight );
        dateCreatedText.text = data.dateCreated.ToShortDateString();
        PopulateGrid();
    }

    private void LoadCard( CardData data )
    {
        Debug.Assert( currentModifyCardIdx != null );
        // TODO: Load new image
        //cardsDisplayGrid.transform.GetChild( currentModifyCardIdx.Value ).GetComponent<Image>().sprite
        cardsDisplayGridLeft.transform.GetChild( currentModifyCardIdx.Value ).GetComponent<Image>().color = Color.red;
        currentbinder.cardList[currentModifyCardIdx.Value] = data;
    }

    private void PopulateGrid()
    {
        // TODO: Resetup grid X/Y
        if( width != currentbinder.pageWidth || height != currentbinder.pageHeight )
        {

        }
        else
        {
            // Reset images
            for( int i = 0; i < cardsDisplayGridLeft.transform.childCount; ++i )
                cardsDisplayGridLeft.transform.GetChild( i ).GetComponent<Image>().sprite = null;
        }

        // Setup buttons
        for( int i = 0; i < cardsDisplayGridLeft.transform.childCount; ++i )
        {
            int idx = i;
            cardsDisplayGridLeft.transform.GetChild( i ).GetComponent<EventDispatcher>().OnDoubleClickEvent += ( PointerEventData e ) =>
                {
                    currentModifyCardIdx = idx;
                    OpenSearchPanel();
                };
        }
    }

    public void NextPage()
    {
        currentPage = Mathf.Clamp( currentPage + 1, 0, currentbinder.pageCount - 1 );
        PopulateGrid();
    }

    public void prevPage()
    {
        currentPage = Mathf.Clamp( currentPage - 1, 0, currentbinder.pageCount - 1 );
        PopulateGrid();
    }

    public void OpenSearchPanel()
    {
        searchListPage.SetActive( true );
    }

    public void Selectcard()
    {
        Debug.Assert( currentModifyCardIdx != null );
        searchListPage.SetActive( false );
        // TODO
        currentbinder.cardList[currentModifyCardIdx.Value] = new CardData()
        {
            name = "test",
            cardId = 5,
            imagePath = "test",
        };
        currentModifyCardIdx = null;
    }
    private void OnApplicationPause( bool paused )
    {
        if( paused )
            Save();
    }

    private void OnApplicationFocus( bool hasFocus )
    {
        if( !hasFocus )
            Save();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void Save()
    {
        SaveGameSystem.SaveGame( "test" );
    }
}