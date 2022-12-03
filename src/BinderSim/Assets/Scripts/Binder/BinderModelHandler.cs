using UnityEngine;
using UnityEditor;
using echo17.EndlessBook;
using echo17.EndlessBook.Demo02;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.EventSystems;

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
    [SerializeField] LayerMask pageRaycastLayerMask;
    [SerializeField] LayerMask cardRaycastLayerMask;
    [SerializeField] LayerMask bookRaycastLayerMask;
    [SerializeField] LayerMask uiRaycastLayerMask;
    [SerializeField] BoxCollider fullBookCollider;

    private float doubleClickTimer = 0.0f;
    [SerializeField] float doubleClickInterval = 0.5f;
    [SerializeField] float turnStopSpeed = 1.0f;

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
    private bool isPageDragMouseDown;
    Vector2 lastDragPosition;

    public event Action<EndlessBook.StateEnum, EndlessBook.StateEnum, int> onBookStateChanged;

    protected override void Start()
    {
        base.Start();

        book = GetComponent<EndlessBook>();
        binderScene.SetActive( false );

        // turn on the audio now that the book state is set the first time,
        // otherwise we'd hear a noise and no change would occur
        audioOn = true;

        mainCamera = Camera.main;

        DebugScreen.AddDebugEntry( () => String.Format( "Book pages (left,#,right): {0}, {1}, {2}",
            book.CurrentLeftPageNumber, book.CurrentPageNumber, book.CurrentRightPageNumber ) );
        DebugScreen.AddDebugEntry( () => String.Format( "Book CurrentState: {0}", book.CurrentState ) );
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
            //currentPage = cardChange.newPage;
            //Repopulate();           
        }
        else if( e is BinderDataUpdateEvent update )
        {
            Reinitialise();
        }
        else if( e is BinderPopulateGrid populateGrid )
        {
            currentPage = populateGrid.currentPage;
            Repopulate();
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
            Reinitialise();
        }

        book.SetMaxPagesTurningCount( Mathf.Clamp( currentBinder.data.pageCount / 2, 1, 10 ) );
        book.SetMaterial( EndlessBook.MaterialEnum.BookPageFront, unloadedPageMaterial );
        book.SetMaterial( EndlessBook.MaterialEnum.BookPageBack, unloadedPageMaterial );

        // set the book closed
        OnBookStateChanged( EndlessBook.StateEnum.ClosedFront, EndlessBook.StateEnum.ClosedFront, -1 );
        book.SetPageNumber( 1 );

    }
    private void Repopulate()
    {
        var (newState, newPage) = ToEndlessBookPageData( currentPage );

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

    private void Reinitialise()
    {
        savedRTs = new RenderTexture[currentBinder.data.pageCount];

        var cardCountMinusFrontBack = currentBinder.data.pageCount - 2;
        if( ( cardCountMinusFrontBack & 1 ) == 1 )
            cardCountMinusFrontBack++;

        for( int i = 0; i < cardCountMinusFrontBack; ++i )
        {
            var newPage = book.AddPageData();
            newPage.material = unloadedPageMaterial;
        }
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
        if( book.IsTurningPages )
            return;

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

            const float dragThreshold = 0.004f;
            if( offset.magnitude >= dragThreshold )
            {
                bool startedDragging = !dragging;
                dragging = true;
                OnDragDetected( leftPage, hitPosition, hitPositionNormalized, offset, startedDragging );
                lastDragPosition = hitPosition;
            }
        }
    }

    protected virtual void DetectTouchUp()
    {
        if( book.IsTurningPages )
            return;

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

        // Don't raycast into the book if you are clicking on a UI element
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        var pointerEventData = new PointerEventData( eventSystem );
        pointerEventData.position = mousePosition;
        var results = new List<RaycastResult>();
        eventSystem.RaycastAll( pointerEventData, results );

        if( results.Any( (x) => ( x.gameObject.layer & uiRaycastLayerMask ) == uiRaycastLayerMask ) )
            return false;

        // get a ray from the screen to the page colliders
        Ray ray = mainCamera.ScreenPointToRay( mousePosition );

        // cast the ray against the collider mask
        if( Physics.Raycast( ray, out var hit, 1000, pageRaycastLayerMask ) )
        {
            // hit
            var bookModel = book.standins[( int )book.CurrentState];
            var colliders = bookModel.GetComponents<BoxCollider>();
            Debug.Assert( colliders.Contains( hit.collider ) );

            // determine which page was hit
            leftPage = book.CurrentState == EndlessBook.StateEnum.OpenBack ||
                book.CurrentState == EndlessBook.StateEnum.ClosedBack ||
                ( colliders.Length > 1 && hit.collider == colliders[0] );
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

    private Collider GetPageBounds( bool leftPage )
    {
        var bookModel = book.standins[( int )book.CurrentState];
        var colliders = bookModel.GetComponents<BoxCollider>();
        var idx = !leftPage && colliders.Length > 1 ? 1 : 0;
        var pageBounds = colliders[Mathf.Min( idx, colliders.Length - 1 )];
        Debug.Assert( pageBounds.gameObject.activeSelf );
        return pageBounds;
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
        var pageBounds = GetPageBounds( leftPage );

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

    private void TestCardInteraction( bool leftPage, Vector2 hitPointNormalized, Action<Collider, int, int> action )
    {
        if( book.IsTurningPages ||
            book.CurrentState == EndlessBook.StateEnum.ClosedFront ||
            book.CurrentState == EndlessBook.StateEnum.ClosedBack ||
            ( book.CurrentState == EndlessBook.StateEnum.OpenFront && leftPage ) ||
            ( book.CurrentState == EndlessBook.StateEnum.OpenBack && !leftPage ) )
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
            action( hit.collider, page, cardIdx );
        }
    }

    // Handle double clicking on cards
    private void TouchDownDetected( bool leftPage, Vector2 hitPointNormalized )
    {
        TestCardInteraction( leftPage, hitPointNormalized, ( collider, page, cardIdx ) =>
        {
            cardClickedOn = true;

            if( !dragging && Time.time - doubleClickTimer <= doubleClickInterval )
            {
                EventSystem.Instance.TriggerEvent( new CardDoubleClickEvent()
                {
                    page = page,
                    pos = cardIdx,
                } );
                doubleClickTimer = 0.0f;
            }
            else
            {
                doubleClickTimer = Time.time;
            }
        } );
    }

    // Dragging cards around or dragging page manually
    private void OnDragDetected( bool leftPage, Vector2 hitPosition, Vector2 hitPointNormalized, Vector2 offset, bool startedDragging )
    {
        // First dragging
        if( startedDragging )
        {
            bool draggingCard = false;

            // Check card drag
            TestCardInteraction( leftPage, hitPointNormalized, ( collider, page, cardIdx ) =>
            {
                if( currentBinder.data.cardList[page][cardIdx] == null )
                    return;

                var colliderBoundsScreen = GetCardScreenSpaceRect( collider.gameObject, leftPage );
                draggingCard = true;

                EventSystem.Instance.TriggerEvent( new StartDraggingEvent()
                {
                    page = page,
                    pos = cardIdx,
                    colliderBoundsScreen = colliderBoundsScreen,
                } );
            } );

            // Otherwise check page drag
            if( !draggingCard )
            {
                if( book.IsTurningPages || book.IsDraggingPage || UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() )
                {
                    // exit if already turning
                    return;
                }

                var normalizedTime = GetNormalizedTime();
                var direction = leftPage ? Page.TurnDirectionEnum.TurnBackward : Page.TurnDirectionEnum.TurnForward;
                book.TurnPageDragStart( direction );
                isPageDragMouseDown = true;
            }
        }
        // Continue dragging
        else if( !book.IsTurningPages && book.IsDraggingPage && isPageDragMouseDown )
        {
            // get the normalized time based on the mouse position
            var normalizedTime = GetNormalizedTime();

            // tell the book to move the manual page drag to the normalized time
            book.TurnPageDrag( normalizedTime );
        }
    }

    private float GetNormalizedTime()
    {
        // get the ray from the camera to the screen
        var ray = mainCamera.ScreenPointToRay( Input.mousePosition );

        // cast a ray and see where it hits
        if( Physics.Raycast( ray, out var hit, bookRaycastLayerMask ) )
        {
            // return the position of the ray cast in terms of the normalized position of the collider box
            return ( hit.point.x - fullBookCollider.bounds.min.x ) / fullBookCollider.bounds.size.x;
        }

        // if we didn't hit the collider, then check to see if we are on the
        // left or right side of the screen and calculate the normalized time appropriately
        var viewportPoint = mainCamera.ScreenToViewportPoint( Input.mousePosition );
        return ( viewportPoint.x >= 0.5f ) ? 1 : 0;
    }

    // Change page handling (mouse up, if no card clicked on)
    private void TouchUpDetected( bool leftPage, Vector2 hitPointNormalized, bool dragging )
    {
        // Page drag release
        if( isPageDragMouseDown )
        {
            isPageDragMouseDown = false;

            if( book.IsTurningPages || !book.IsDraggingPage || UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() )
            {
                // if not turning then exit
                return;
            }
            
            // tell the book to stop manual turning.
            book.TurnPageDragStop( turnStopSpeed, null, book.TurnPageDragNormalizedTime < 0.5f );
        }
        // Not dragging card? Then it's a single click so we turn the page
        else
        {
            if( !cardClickedOn )
                EventSystem.Instance.TriggerEvent( new BinderChangeCardPageRequest() { nextPage = !leftPage } );
            cardClickedOn = false;
        }
    }

    private void OnBookStateChanged(EndlessBook.StateEnum fromState, EndlessBook.StateEnum toState, int pageNumber)
    {
        onBookStateChanged?.Invoke( fromState, toState, pageNumber );

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

    private void OnPageTurnStart(Page page, int pageNumberFront, int pageNumberBack, int pageNumberFirstVisible, int pageNumberLastVisible, Page.TurnDirectionEnum turnDirection)
    {
        // play page turn sound if not flipping through multiple pages
        if (!flipping)
        {
            pageTurnSound.Play();
        }
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