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

    private void InitialiseBookMaterial( Material mat, int renderTextureIdx, Action<Material> setMatFunc )
    {
        if( mat == unloadedPageMaterial )
        {
            var newMaterial = Instantiate( renderTextureBaseMaterial );
            savedRTs[renderTextureIdx] = Instantiate( renderTextureBase );
            newMaterial.SetTexture( "_MainTex", savedRTs[renderTextureIdx] );
            setMatFunc( newMaterial );
        }
    }

    public override void OnEventReceived( IBaseEvent e )
    {
        if( e is OpenCardPageEvent openCardRequest )
        {
            currentBinder = openCardRequest.binder;
        }
        else if( e is BinderChangeCardPage cardChange )
        {
            var( newState, newPage ) = ToEndlessBookPageData( cardChange.newPage );

            if( newState == EndlessBook.StateEnum.OpenFront )
            {
                InitialiseBookMaterial( book.GetMaterial( EndlessBook.MaterialEnum.BookPageFront ), 0, ( newMaterial ) =>
                {
                    book.SetMaterial( EndlessBook.MaterialEnum.BookPageFront, newMaterial );
                } );
                rightGridCamera.targetTexture = savedRTs[0];
                leftGridCamera.gameObject.SetActive( false );
                rightGridCamera.gameObject.SetActive( true );
            }
            else if( newState == EndlessBook.StateEnum.OpenBack )
            {
                InitialiseBookMaterial( book.GetMaterial( EndlessBook.MaterialEnum.BookPageBack ), savedRTs.Length - 1, ( newMaterial ) =>
                {
                    book.SetMaterial( EndlessBook.MaterialEnum.BookPageBack, newMaterial );
                } );
                leftGridCamera.targetTexture = savedRTs[savedRTs.Length - 1];
                leftGridCamera.gameObject.SetActive( true );
                rightGridCamera.gameObject.SetActive( false );
            }
            else if( newPage >= 1 && newPage < currentBinder.data.pageCount )
            {
                var leftIdx = newPage;
                InitialiseBookMaterial( book.GetPageData( leftIdx ).material, leftIdx, ( newMaterial ) =>
                {
                    book.SetPageData( leftIdx, new PageData() { material = newMaterial } );
                } );
                leftGridCamera.targetTexture = savedRTs[leftIdx];

                var rightIdx = newPage + 1;
                InitialiseBookMaterial( book.GetPageData( rightIdx ).material, rightIdx, ( newMaterial ) =>
                {
                    book.SetPageData( rightIdx, new PageData() { material = newMaterial } );
                } );
                rightGridCamera.targetTexture = savedRTs[rightIdx];
                leftGridCamera.gameObject.SetActive( true );
                rightGridCamera.gameObject.SetActive( true );
            }

            if( newState == EndlessBook.StateEnum.OpenMiddle && book.CurrentState == EndlessBook.StateEnum.OpenMiddle )
                TurnToPage( newPage );
            else
                SetState( newState );
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

        book.SetMaterial( EndlessBook.MaterialEnum.BookPageFront, unloadedPageMaterial );
        book.SetMaterial( EndlessBook.MaterialEnum.BookPageBack, unloadedPageMaterial );

        var cardCountMinusFrontBack = currentBinder.data.pageCount - 2;
        for( int i = 0; i < cardCountMinusFrontBack; ++i )
        {
            var newPage = book.AddPageData();
            newPage.material = unloadedPageMaterial;
        
            //InitialiseBookMaterial( book.GetPageData( i + 1 ).material, i + 1, ( newMaterial ) =>
            //{
            //    book.GetPageData( leftIdx ).material = newMaterial;
            //} );
        }
        
        // Fuck you
        //InitialiseBookMaterial( book.GetMaterial( EndlessBook.MaterialEnum.BookPageFront ), 0, ( newMaterial ) =>
        //{
        //    book.SetMaterial( EndlessBook.MaterialEnum.BookPageFront, newMaterial );
        //} );
        //InitialiseBookMaterial( book.GetMaterial( EndlessBook.MaterialEnum.BookPageBack ), savedRTs.Length - 1, ( newMaterial ) =>
        //{
        //    book.SetMaterial( EndlessBook.MaterialEnum.BookPageBack, newMaterial );
        //} );

        // set the book closed
        OnBookStateChanged( EndlessBook.StateEnum.ClosedFront, EndlessBook.StateEnum.ClosedFront, -1 );
        book.SetPageNumber( 1 );
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
        EventSystem.Instance.TriggerEvent( new BinderChangeCardPageRequest() { nextPage = page == TouchPad.PageEnum.Right } );
    }

    private void OnBookStateChanged(EndlessBook.StateEnum fromState, EndlessBook.StateEnum toState, int pageNumber)
    {
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
    }

    private Pair<EndlessBook.StateEnum, int> ToEndlessBookPageData( int page )
    {
        if( page < 0 )
            return new Pair<EndlessBook.StateEnum, int>( EndlessBook.StateEnum.ClosedFront, -1 );

        if( page > currentBinder.data.pageCount )
            return new Pair<EndlessBook.StateEnum, int>( EndlessBook.StateEnum.ClosedBack, 999 );

        if( page == 0 )
            return new Pair<EndlessBook.StateEnum, int>( EndlessBook.StateEnum.OpenFront, 0 );

        if( page == currentBinder.data.pageCount )
            return new Pair<EndlessBook.StateEnum, int>( EndlessBook.StateEnum.OpenBack, 0 );

        return new Pair<EndlessBook.StateEnum, int>( EndlessBook.StateEnum.OpenMiddle, page - 1 );
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
    }

    private void OnPageTurnEnd( Page page, int pageNumberFront, int pageNumberBack, int pageNumberFirstVisible, int pageNumberLastVisible, Page.TurnDirectionEnum turnDirection )
    {
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