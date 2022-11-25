using UnityEngine;
using UnityEditor;
using echo17.EndlessBook;
using echo17.EndlessBook.Demo02;
using System;
using System.Linq;
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

    [SerializeField] GameObject binderScene = null;
    [SerializeField] Material unloadedPageMaterial = null;
    [SerializeField] Material renderTextureBaseMaterial = null;
    [SerializeField] RenderTexture renderTextureBase = null;
    [SerializeField] Camera leftGridCamera = null;
    [SerializeField] Camera rightGridCamera = null;
    [SerializeField] LayerMask bookRaycastLayerMask;
    [SerializeField] LayerMask cardRaycastLayerMask;

    private EndlessBook book;
    private BinderDataRuntime currentBinder;
    private bool audioOn = false;
    private bool flipping = false;
    private RenderTexture[] savedRTs;
    private int currentPage;

    private Camera mainCamera;
    private bool cardClickedOn;
    private bool touchDown;
    private bool dragging;
    Vector2 lastDragPosition;

    protected override void Start()
    {
        base.Start();

        book = GetComponent<EndlessBook>();
        binderScene.SetActive( false );

        // turn on the audio now that the book state is set the first time,
        // otherwise we'd hear a noise and no change would occur
        audioOn = true;

        mainCamera = Camera.main;
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
            Show( openCardRequest.binder );
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

            currentPage = cardChange.newPage;
        }
        else if( e is BinderPopulateGrid populateGrid )
        {
            //currentPage = populateGrid.currentPage;
        }

        if( e is PageChangeRequestEvent pageChangeRequest && pageChangeRequest.page == PageType.BinderPage )
        {
            Hide();
        }
    }

    private void Show( BinderDataRuntime newBinder )
    {
        binderScene.SetActive( true );
        book.Reset();

        if( newBinder != currentBinder )
        {
            currentBinder = newBinder;
            savedRTs = new RenderTexture[currentBinder.data.pageCount];

            var cardCountMinusFrontBack = currentBinder.data.pageCount - 2;
            for( int i = 0; i < cardCountMinusFrontBack; ++i )
            {
                var newPage = book.AddPageData();
                newPage.material = unloadedPageMaterial;
            }
        }

        book.SetMaxPagesTurningCount( Mathf.Clamp( currentBinder.data.pageCount / 2, 1, 10 ) );
        book.SetMaterial( EndlessBook.MaterialEnum.BookPageFront, unloadedPageMaterial );
        book.SetMaterial( EndlessBook.MaterialEnum.BookPageBack, unloadedPageMaterial );

        // set the book closed
        OnBookStateChanged( EndlessBook.StateEnum.ClosedFront, EndlessBook.StateEnum.ClosedFront, -1 );
        book.SetPageNumber( 1 );

    }

    private void Hide()
    {
        binderScene.SetActive( false );
    }

    private void Update()
    {
        if( Utility.IsMouseDownOrTouchStart() )
        {
            DetectTouchDown();
        }
        if( Utility.IsMouseUpOrTouchEnd() )
        {
            DetectTouchUp();
        }
        else if( touchDown && Utility.IsMouseOrTouchHeld() )
        {
            DetectDrag();
        }
    }

    private void DetectTouchDown()
    {
        if( GetHitPoint( out var hitPosition, out var hitPositionNormalized, out var leftPage ) )
        {
            touchDown = true;
            dragging = false;

            lastDragPosition = hitPosition;
            TouchDownDetected( leftPage, hitPositionNormalized );
        }
    }

    protected virtual void DetectDrag()
    {
        if( GetHitPoint( out var hitPosition, out var hitPositionNormalized, out var leftPage ) )
        {
            var offset = hitPosition - lastDragPosition;

            const float dragThreshold = 0.007f;
            if( offset.magnitude >= dragThreshold )
            {
                dragging = true;
                OnDragDetected( leftPage, hitPosition, hitPositionNormalized, offset );
                lastDragPosition = hitPosition;
            }
        }
    }

    protected virtual void DetectTouchUp()
    {
        if( GetHitPoint( out var hitPosition, out var hitPositionNormalized, out var leftPage ) )
        {
            touchDown = false;
            TouchUpDetected( leftPage, hitPositionNormalized, dragging );
            dragging = false;
        }
    }

    protected virtual bool GetHitPoint( out Vector2 hitPosition, out Vector2 hitPositionNormalized, out bool leftPage )
    {
        var mousePosition = Utility.GetMouseOrTouchPos();
        hitPosition = Vector2.zero;
        hitPositionNormalized = Vector2.zero;
        leftPage = true;

        // get a ray from the screen to the page colliders
        Ray ray = mainCamera.ScreenPointToRay( mousePosition );

        // cast the ray against the collider mask
        if( Physics.Raycast( ray, out var hit, 1000, bookRaycastLayerMask ) )
        {
            // hit
            var bookModel = book.standins[( int )book.CurrentState];
            var colliders = bookModel.GetComponents<BoxCollider>();
            Debug.Assert( colliders.Contains( hit.collider ) );

            // determine which page was hit
            leftPage = book.CurrentState == EndlessBook.StateEnum.OpenBack ||
                book.CurrentState == EndlessBook.StateEnum.ClosedBack ||
                ( book.CurrentState == EndlessBook.StateEnum.OpenMiddle && hit.collider == colliders[0] );
            var pageBound = hit.collider == colliders[0] ? colliders[0] : colliders[1];

            // set the hit position using the x and z axis
            hitPosition = new Vector2( hit.point.x, hit.point.z );

            // normalize the hit position against the page rects
            hitPositionNormalized = new Vector2( 
                ( hit.point.x - pageBound.bounds.min.x ) / ( pageBound.bounds.extents.x * 2.0f ), 
                ( hit.point.z - pageBound.bounds.min.z ) / ( pageBound.bounds.extents.z * 2.0f ) );

            return true;
        }

        return false;
    }

    private Collider GetPageBounds( int idx )
    {
        var bookModel = book.standins[( int )book.CurrentState];
        var colliders = bookModel.GetComponents<BoxCollider>();
        var pageBounds = colliders[Mathf.Min( idx, colliders.Length - 1 )];
        Debug.Assert( pageBounds.gameObject.activeSelf );
        return pageBounds;
    }

    // Handle clicking on cards (and dragging)
    private void TouchDownDetected( bool leftPage, Vector2 hitPointNormalized )
    {
        if( book.IsTurningPages || 
            book.CurrentState == EndlessBook.StateEnum.ClosedFront ||
            book.CurrentState == EndlessBook.StateEnum.ClosedBack )
            return;

        var gridCamera = leftPage ? leftGridCamera : rightGridCamera;
        var screenPoint = new Vector3(
            gridCamera.pixelWidth * hitPointNormalized.x,
            gridCamera.pixelHeight * hitPointNormalized.y,
            0.0f );
        Ray ray = gridCamera.ScreenPointToRay( screenPoint );

        if( Physics.Raycast( ray, out var hit, 1000.0f, cardRaycastLayerMask ) )
        {
            var cardIdx = hit.collider.transform.GetChildIndex();
            var page = currentPage + ( leftPage ? -1 : 0 );
            Debug.Assert( cardIdx != -1 );

            if( currentBinder.data.cardList[page][cardIdx] == null )
                return;

            var colliderBoundsScreen = GetCardScreenSpaceRect( hit.collider.gameObject, leftPage );

            EventSystem.Instance.TriggerEvent( new StartDraggingEvent()
            {
                page = page,
                pos = cardIdx,
                colliderBoundsScreen = colliderBoundsScreen,
            } );

            cardClickedOn = true;
        }
    }

    public Rect GetCardScreenSpaceRect( GameObject gridCard, bool leftPage )
    {
        var collider = gridCard.GetComponent<BoxCollider>();
        Debug.Assert( collider != null );

        var gridCamera = leftPage ? leftGridCamera : rightGridCamera;

        // Get rect of the collider in gridCamera screen space
        var min = gridCamera.WorldToScreenPoint( collider.bounds.min );
        var max = gridCamera.WorldToScreenPoint( collider.bounds.max );

        // Normalise size & offset into percentages of the grid camera
        var minPercent = new Vector2( min.x / gridCamera.pixelWidth, min.y / gridCamera.pixelHeight );
        var maxPercent = new Vector2( max.x / gridCamera.pixelWidth, max.y / gridCamera.pixelHeight );

        // Bounds of the page
        var colliderIdx = !leftPage && book.CurrentState == EndlessBook.StateEnum.OpenMiddle ? 1 : 0;
        var pageBounds = GetPageBounds( colliderIdx );

        // Convert to world space using the bounds of the page and the percentages from grid camera screen space
        // This works because the page bounding volume occupies the same space that the grid camera render texture displays to on the book
        var minWorldSpace = new Vector3(
            Mathf.Lerp( pageBounds.bounds.min.x, pageBounds.bounds.max.x, minPercent.x ),
            pageBounds.bounds.center.y,
            Mathf.Lerp( pageBounds.bounds.min.z, pageBounds.bounds.max.z, minPercent.y ) );
        var maxWorldSpace = new Vector3(
            Mathf.Lerp( pageBounds.bounds.min.x, pageBounds.bounds.max.x, maxPercent.x ),
            pageBounds.bounds.center.y,
            Mathf.Lerp( pageBounds.bounds.min.z, pageBounds.bounds.max.z, maxPercent.y ) );

        var minScreenSpace = mainCamera.WorldToScreenPoint( minWorldSpace );
        var maxScreenSpace = mainCamera.WorldToScreenPoint( maxWorldSpace );
        var colliderBoundsScreen = new Rect( minScreenSpace, maxScreenSpace - minScreenSpace );

        return colliderBoundsScreen;
    }

    private void OnDragDetected( bool leftPage, Vector2 hitPosition, Vector2 hitPointNormalized, Vector2 offset )
    {

    }

    // Change page handling
    private void TouchUpDetected( bool leftPage, Vector2 hitPointNormalized, bool dragging )
    {
        if( !cardClickedOn )
            EventSystem.Instance.TriggerEvent( new BinderChangeCardPageRequest() { nextPage = !leftPage } );
        cardClickedOn = false;
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
        //touchPad.Toggle(TouchPad.PageEnum.Left, on && book.CurrentState != EndlessBook.StateEnum.ClosedFront);
        //touchPad.Toggle(TouchPad.PageEnum.Right, on && book.CurrentState != EndlessBook.StateEnum.ClosedBack);
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