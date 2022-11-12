using UnityEngine;
using echo17.EndlessBook;
using echo17.EndlessBook.Demo02;
using System;
using System.Collections.Generic;

public enum BookActionTypeEnum
{
    ChangeState,
    TurnPage
}

public delegate void BookActionDelegate(BookActionTypeEnum actionType, int actionValue);

public class BinderModelHandler : EventReceiverInstance
{
    [SerializeField] float openCloseTime = 0.3f;
    [SerializeField] EndlessBook.PageTurnTimeTypeEnum groupPageTurnType;
    [SerializeField] float singlePageTurnTime;
    [SerializeField] float groupPageTurnTime;

    [SerializeField] AudioSource bookOpenSound;
    [SerializeField] AudioSource bookCloseSound;
    [SerializeField] AudioSource pageTurnSound;
    [SerializeField] AudioSource pagesFlippingSound;
    [SerializeField] float pagesFlippingSoundDelay;

    [SerializeField] TouchPad touchPad;
    [SerializeField] GameObject binderScene = null;
    [SerializeField] Material unloadedPageMaterial = null;
    [SerializeField] Material renderTextureBaseMaterial = null;
    [SerializeField] RenderTexture renderTextureBase = null;
    [SerializeField] Camera leftGridCamera = null;
    [SerializeField] Camera rightGridCamera = null;

    private EndlessBook book;
    private BinderDataRuntime currentBinder;
    private bool audioOn = false;
    private bool flipping = false;
    private RenderTexture[] savedRTs;

    protected override void Start()
    {
        base.Start();

        book = GetComponent<EndlessBook>();
        binderScene.SetActive( false );

        // set up touch pad handlers
        touchPad.touchDownDetected = TouchPadTouchDownDetected;
        touchPad.touchUpDetected = TouchPadTouchUpDetected;
        touchPad.dragDetected = TouchPadDragDetected;

        // turn on the audio now that the book state is set the first time,
        // otherwise we'd hear a noise and no change would occur
        audioOn = true;
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is OpenCardPageEvent openCardRequest )
        {
            currentBinder = openCardRequest.binder;
        }
        else if( e is BinderChangeCardPage cardChange )
        {
            int page = ToEndlessBookPageNumber( cardChange.newState, cardChange.newPage );

            if( cardChange.newState == EndlessBook.StateEnum.OpenFront )
            {
                if( book.GetMaterial( EndlessBook.MaterialEnum.BookPageFront ) == unloadedPageMaterial )
                {
                    var newMaterial = Instantiate( renderTextureBaseMaterial );
                    savedRTs[0] = Instantiate( renderTextureBase );
                    newMaterial.SetTexture( "_MainTex", savedRTs[0] );
                    book.SetMaterial( EndlessBook.MaterialEnum.BookPageFront, newMaterial );
                }

                rightGridCamera.targetTexture = savedRTs[0];
            }
            else if( cardChange.newState == EndlessBook.StateEnum.OpenBack )
            {
                if( book.GetMaterial( EndlessBook.MaterialEnum.BookPageBack ) == unloadedPageMaterial )
                {
                    var newMaterial = Instantiate( renderTextureBaseMaterial );
                    savedRTs[savedRTs.Length - 1] = Instantiate( renderTextureBase );
                    newMaterial.SetTexture( "_MainTex", savedRTs[savedRTs.Length - 1] );
                    book.SetMaterial( EndlessBook.MaterialEnum.BookPageBack, newMaterial );
                }

                leftGridCamera.targetTexture = savedRTs[savedRTs.Length - 1];
            }
            else if( page >= 0 && page < currentBinder.data.pageCount )
            {
                var pageData = page + 1;

                if( book.GetPageData( pageData ).material == unloadedPageMaterial )
                {
                    var newMaterial = Instantiate( renderTextureBaseMaterial );
                    savedRTs[cardChange.newPage - 1] = Instantiate( renderTextureBase );
                    newMaterial.SetTexture( "_MainTex", savedRTs[cardChange.newPage - 1] );
                    book.GetPageData( pageData ).material = newMaterial;
                }

                leftGridCamera.targetTexture = savedRTs[cardChange.newPage - 1];

                if( book.GetPageData( pageData + 1 ).material == unloadedPageMaterial )
                {
                    var newMaterial = Instantiate( renderTextureBaseMaterial );
                    savedRTs[cardChange.newPage] = Instantiate( renderTextureBase );
                    newMaterial.SetTexture( "_MainTex", savedRTs[cardChange.newPage] );
                    book.GetPageData( pageData + 1 ).material = newMaterial;
                }

                rightGridCamera.targetTexture = savedRTs[cardChange.newPage];
            }
        }
        else if( e is BinderPopulateGrid populateGrid )
        {
            //currentPage = populateGrid.currentPage;
        }

        if( e is PageChangeRequestEvent pageChangeRequest )
        {
            switch( pageChangeRequest.page )
            {
                case PageType.BinderPage:
                    Hide();
                    break;
                case PageType.CardPage:
                    Show();
                    break;
            }
        }
    }

    private void Show()
    {
        Debug.Assert( currentBinder != null );
        binderScene.SetActive( true );
        book.SetMaxPagesTurningCount( Mathf.Clamp( currentBinder.data.pageCount / 2, 1, 10 ) );
        savedRTs = new RenderTexture[currentBinder.data.pageCount];

        var cardCountMinusFrontBack = currentBinder.data.pageCount - 2;
        for( int i = 0; i < cardCountMinusFrontBack; ++i )
        {
            var newPage = book.AddPageData();
            newPage.material = unloadedPageMaterial;
        }

        book.SetMaterial( EndlessBook.MaterialEnum.BookPageFront, unloadedPageMaterial );
        book.SetMaterial( EndlessBook.MaterialEnum.BookPageBack, unloadedPageMaterial );

        // set the book closed
        OnBookStateChanged( EndlessBook.StateEnum.ClosedFront, EndlessBook.StateEnum.ClosedFront, -1 );
    }

    private void Hide()
    {
        binderScene.SetActive( false );
    }

    private void TouchPadTouchDownDetected( TouchPad.PageEnum page, Vector2 hitPointNormalized )
    {

    }

    private void TouchPadTouchUpDetected( TouchPad.PageEnum page, Vector2 hitPointNormalized, bool dragging )
    {
        switch( book.CurrentState )
        {
            case EndlessBook.StateEnum.ClosedFront:

                switch( page )
                {
                    case TouchPad.PageEnum.Right:

                        // transition from the ClosedFront to the OpenFront states
                        OpenFront();

                        break;
                }

                break;

            case EndlessBook.StateEnum.OpenFront:

                switch( page )
                {
                    case TouchPad.PageEnum.Left:

                        // transition from the OpenFront to the ClosedFront states
                        ClosedFront();

                        break;

                    case TouchPad.PageEnum.Right:

                        // transition from the OpenFront to the OpenMiddle states
                        OpenMiddle();

                        break;
                }

                break;

            case EndlessBook.StateEnum.OpenMiddle:

                /*
                PageView pageView;

                if( dragging )
                {
                    // get the left page view if available.
                    // in this demo we only have one group of pages that handle the drag: the map.
                    // instead of having logic for dragging on both pages, we'll just handle it on the left
                    pageView = GetPageView( book.CurrentLeftPageNumber );

                    if( pageView != null )
                    {
                        // call the drag method on the page view
                        pageView.Drag( Vector2.zero, true );
                    }

                    return;
                }

                switch( page )
                {
                    case TouchPad.PageEnum.Left:

                        // get the left page view if available
                        pageView = GetPageView( book.CurrentLeftPageNumber );

                        if( pageView != null )
                        {
                            // cast a ray into the page and exit if we hit something (don't turn the page)
                            if( pageView.RayCast( hitPointNormalized, BookAction ) )
                            {
                                return;
                            }
                        }

                        break;

                    case TouchPad.PageEnum.Right:

                        // get the right page view if available
                        pageView = GetPageView( book.CurrentRightPageNumber );

                        if( pageView != null )
                        {
                            // cast a ray into the page and exit if we hit something (don't turn the page)
                            if( pageView.RayCast( hitPointNormalized, BookAction ) )
                            {
                                return;
                            }
                        }

                        break;
                }*/

                break;

            case EndlessBook.StateEnum.OpenBack:

                switch( page )
                {
                    case TouchPad.PageEnum.Left:

                        // transition from the OpenBack to the OpenMiddle states
                        OpenMiddle();

                        break;

                    case TouchPad.PageEnum.Right:

                        // transition from the OpenBack to the ClosedBack states
                        ClosedBack();

                        break;
                }

                break;

            case EndlessBook.StateEnum.ClosedBack:

                switch( page )
                {
                    case TouchPad.PageEnum.Left:

                        // transition from the ClosedBack to the OpenBack states
                        OpenBack();

                        break;
                }

                break;

        }

        switch( page )
        {
            case TouchPad.PageEnum.Left:

                if( book.CurrentLeftPageNumber == 1 )
                {
                    // if on the first page, transition from the OpenMiddle to the OpenFront states
                    OpenFront();
                }
                else
                {
                    // not on the first page, so just turn back one page
                    book.TurnBackward( singlePageTurnTime, onCompleted: OnBookStateChanged, onPageTurnStart: OnPageTurnStart, onPageTurnEnd: OnPageTurnEnd );
                }

                break;

            case TouchPad.PageEnum.Right:

                if( book.CurrentRightPageNumber == book.LastPageNumber )
                {
                    // if on the last page, transition from the OpenMiddle to the OpenBack states
                    OpenBack();
                }
                else
                {
                    // not on the last page, so just turn forward a page
                    book.TurnForward( singlePageTurnTime, onCompleted: OnBookStateChanged, onPageTurnStart: OnPageTurnStart, onPageTurnEnd: OnPageTurnEnd );
                }

                break;
        }
    }

    private void OnBookStateChanged(EndlessBook.StateEnum fromState, EndlessBook.StateEnum toState, int pageNumber)
    {
        Debug.LogFormat( "OnBookStateChanged, pageNumber = {0}", pageNumber );

        switch( toState)
        {
            case EndlessBook.StateEnum.ClosedFront:
            case EndlessBook.StateEnum.ClosedBack:
                if (audioOn)
                    bookCloseSound.Play();
                break;

            case EndlessBook.StateEnum.OpenMiddle:
                if (fromState != EndlessBook.StateEnum.OpenMiddle)
                {
                    bookOpenSound.Play();
                }
                else
                {
                    flipping = false;
                    pagesFlippingSound.Stop();
                }
                break;

            case EndlessBook.StateEnum.OpenFront:
            case EndlessBook.StateEnum.OpenBack:
                bookOpenSound.Play();
                break;
        }

        ToggleTouchPad(true);

        // TODO (Transform pageNumber into the page system the card page uses (halved))
        EventSystem.Instance.TriggerEvent( new BinderChangeCardPage()
        {
            newState = toState,
            newPage = ToAppPageNumber( toState, pageNumber )
        } );
    }

    private int ToEndlessBookPageNumber( EndlessBook.StateEnum toState, int page )
    {
        if( page == -1 )
            return -1;

        if( toState == EndlessBook.StateEnum.OpenMiddle )
            return page - 2;

        return page;
    }

    private int ToAppPageNumber( EndlessBook.StateEnum toState, int page )
    {
        if( page == -1 )
            return -1;

        if( toState == EndlessBook.StateEnum.OpenMiddle )
            return page + 2;

        return page;
    }

    private void ToggleTouchPad(bool on)
    {
        touchPad.Toggle(TouchPad.PageEnum.Left, on && book.CurrentState != EndlessBook.StateEnum.ClosedFront);
        touchPad.Toggle(TouchPad.PageEnum.Right, on && book.CurrentState != EndlessBook.StateEnum.ClosedBack);
    }

    private void OnPageTurnStart(Page page, int pageNumberFront, int pageNumberBack, int pageNumberFirstVisible, int pageNumberLastVisible, Page.TurnDirectionEnum turnDirection)
    {
        // play page turn sound if not flipping through multiple pages
        if (!flipping)
        {
            pageTurnSound.Play();
        }

        // turn off the touch pad
        ToggleTouchPad(false);

        if( !flipping )
            Debug.LogFormat( "OnPageTurnStart, pageNumberFront = {0}, pageNumberBack = {1}", pageNumberFront, pageNumberBack );
    }

    private void OnPageTurnEnd( Page page, int pageNumberFront, int pageNumberBack, int pageNumberFirstVisible, int pageNumberLastVisible, Page.TurnDirectionEnum turnDirection )
    {
        if( !flipping )
            Debug.LogFormat( "OnPageTurnEnd, pageNumberFront = {0}, pageNumberBack = {1}", pageNumberFront, pageNumberBack );
    }

    private void TouchPadDragDetected(TouchPad.PageEnum page, Vector2 touchDownPosition, Vector2 currentPosition, Vector2 incrementalChange)
    {
        // only handle drag in the OpenMiddle state
        /*if (book.CurrentState == EndlessBook.StateEnum.OpenMiddle)
        {
            // get the page view if available
            var pageView = GetPageView(book.CurrentLeftPageNumber);
    
            if (pageView != null)
            {
                // drag
                pageView.Drag(incrementalChange, false);
            }
        }*/
    }

    private void ClosedFront()
    {
        SetState(EndlessBook.StateEnum.ClosedFront);
    }

    private void OpenFront()
    {
        SetState(EndlessBook.StateEnum.OpenFront);
    }

    private void OpenMiddle()
    {
        SetState(EndlessBook.StateEnum.OpenMiddle);
    }

    private void OpenBack()
    {
        SetState(EndlessBook.StateEnum.OpenBack);
    }

    private void ClosedBack()
    {
        SetState(EndlessBook.StateEnum.ClosedBack);
    }

    private void SetState(EndlessBook.StateEnum state)
    {
        // turn of the touch pad
        ToggleTouchPad(false);

        // set the state
        book.SetState(state, openCloseTime, OnBookStateChanged);
    }

    private void TurnToPage(int pageNumber)
    {
        var newLeftPageNumber = pageNumber % 2 == 0 ? pageNumber - 1 : pageNumber;

        // play the flipping sound if more than a single page is turning
        if (Mathf.Abs(newLeftPageNumber - book.CurrentLeftPageNumber) > 2)
        {
            flipping = true;
            pagesFlippingSound.PlayDelayed(pagesFlippingSoundDelay);
        }

        // turn to page
        book.TurnToPage(pageNumber, groupPageTurnType, groupPageTurnTime,
                        openTime: openCloseTime,
                        onCompleted: OnBookStateChanged,
                        onPageTurnStart: OnPageTurnStart,
                        onPageTurnEnd: OnPageTurnEnd);
    }
}