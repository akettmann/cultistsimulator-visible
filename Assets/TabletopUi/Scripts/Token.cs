#pragma warning disable 0649
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Core.Commands;
using Assets.Core.Entities;
using Assets.Core.Enums;
using Assets.Core.Fucine;
using Assets.Core.Interfaces;
using Assets.Core.NullObjects;
using Assets.Core.Services;
using Assets.CS.TabletopUI.Interfaces;
using Assets.TabletopUi;
using Assets.TabletopUi.Scripts.Elements;
using Assets.TabletopUi.Scripts.Elements.Manifestations;
using Assets.TabletopUi.Scripts.Infrastructure;
using Assets.TabletopUi.Scripts.Infrastructure.Events;
using Assets.TabletopUi.Scripts.Interfaces;
using Assets.TabletopUi.Scripts.Services;
using Assets.TabletopUi.Scripts.TokenContainers;
using Assets.TabletopUi.Scripts.UI;
using Noon;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Assets.CS.TabletopUI {

    public enum TokenXNess
    {
        NoValidDestination,
        ValidDestination,
        DivertedByGreedySlot,
        DoesntMatchSlotRequirements,
        DroppedOnTableContainer,
        ReturningSplitStack,
        ReturnedToStartingSlot,
        PlacedInSlot,
        ElementDroppedOnTokenButCannotInteractWithIt,
        DroppedOnTokenWhichMovedAside,
        DroppedOnTokenWhichWontMoveAside,
        MergedIntoStack
    }


    [RequireComponent(typeof(RectTransform))]
    public class Token : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler, IPointerEnterHandler,
        IPointerExitHandler, ISituationSubscriber
    {
        protected bool singleClickPending = false;
        public UnityEvent OnStart;
        public UnityEvent OnCollect;
        public UnityEvent OnWindowClosed;
        public OnContainerAddedEvent OnContainerAdded;
        public OnContainerRemovedEvent OnContainerRemoved;

        protected bool shrouded = false;
        protected Situation _attachedToSituation;

        public IVerb Verb
        {
            get { return _attachedToSituation.Verb; }
        }

        private ElementStack _attachedToElementStack;

        public ElementStack ElementStack
        {
            get => _attachedToElementStack;
        }

        public int ElementQuantity => ElementStack.Quantity;

        public Element Element => ElementStack.Element;

        public Token OriginToken { get; private set; }

        public TokenTravelItinerary Itinerary { get; set;}


     [SerializeField] public RectTransform TokenRectTransform;

     public RectTransform ManifestationRectTransform => _manifestation.RectTransform;
     public TokenLocation Location => new TokenLocation(TokenRectTransform.anchoredPosition3D, Sphere.GetPath());

        [SerializeField] protected bool rotateOnDrag = true;

        [HideInInspector] public Vector2? LastTablePos = null; // if it was pulled from the table, save that position

        protected IManifestation _manifestation;

        protected Transform startParent;
        protected Vector3 startPosition;
        protected int startSiblingIndex;
        protected Vector3 dragOffset;
        protected CanvasGroup canvasGroup;
        private Token originToken = null; // if it was pulled from a stack, save that stack!


        //set true when the Chronicler notices it's been placed on the desktop. This ensures we don't keep spamming achievements / Lever requests. It isn't persisted in saves! which is probably fine.
        public bool PlacementAlreadyChronicled = false;

        protected float perlinRotationPoint = 0f;

        protected float
            dragHeight = -8f; // Draggables all drag on a specifc height and have a specific "default height"

        public Sphere Sphere;
        protected Sphere OldSphere; // Used to tell OldContainsTokens that this thing was dropped successfully


        public void Awake()
        {
            TokenRectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            Sphere = Registry.Get<NullSphere>();
            Itinerary = TokenTravelItinerary.StayExactlyWhereYouAre(this);
            _manifestation = Registry.Get<NullManifestation>();


        }

        public void StartArtAnimation()
        {
            if (!CanAnimateArt())
                return;
            _manifestation.BeginIconAnimation();

        }

        public bool CanAnimateArt()
        {
            if (gameObject == null)
                return false;

            if (gameObject.activeInHierarchy == false)
                return false; // can not animate if deactivated


            return _manifestation.CanAnimateIcon();
        }

        public bool IsInMotion { get; set; }
        public bool Defunct { get; protected set; }
        public bool NoPush => _manifestation.NoPush;
        public bool _currentlyBeingDragged { get; protected set; }
        
        private TokenXNess TokenXNess { get; set; }



        protected AnchorDurability _durability;



        public AnchorDurability Durability
        {
            get { return _durability; }
        }

        protected void OnDisable()
        {
            // this resets any animation frames so we don't get stuck when deactivating mid-anim
            _manifestation.ResetIconAnimation();

        }

        public void Populate(Situation situation)
        {
            _attachedToElementStack = new GameObject(nameof(NullElementStack)).AddComponent<NullElementStack>();

            _attachedToSituation = situation;
            _manifestation = Registry.Get<PrefabFactory>()
                .CreateManifestationPrefab(situation.Verb.AnchorManifestationType, this.transform);


            if (Verb.Transient)
                _durability = AnchorDurability.Transient;
            else
                _durability = AnchorDurability.Enduring;

            name =  Verb.Id + "_verbtoken";

            SituationStateChanged(situation);
            TimerValuesChanged(situation);

            
        }

        
        public void Populate(ElementStack elementStack)
        {
            _attachedToSituation = new NullSituation();

            _attachedToElementStack = elementStack;
            name = elementStack.Element.Id + "_stacktoken";
        }

        private void SwapOutManifestation(IManifestation oldManifestation, IManifestation newManifestation,
            RetirementVFX vfxForOldManifestation)
        {
            var manifestationToRetire = oldManifestation;
            _manifestation = newManifestation;

            manifestationToRetire.Retire(vfxForOldManifestation, OnSwappedOutManifestationRetired);

        }

        private void OnSwappedOutManifestationRetired()
        {
            //
        }

        public virtual void Manifest(Type manifestationType)
        {

            if(ElementStack.IsValidElementStack())
            {

                    var newManifestation = Registry.Get<PrefabFactory>()
                        .CreateManifestationPrefab(manifestationType, this.transform);
                    SwapOutManifestation(_manifestation, newManifestation, RetirementVFX.None);
                _manifestation.InitialiseVisuals(ElementStack.Element);
                _manifestation.UpdateVisuals(ElementStack.Element, ElementStack.Quantity);
            }
            else
            {
                    var newManifestation = Registry.Get<PrefabFactory>()
                        .CreateManifestationPrefab(manifestationType, this.transform);
                    SwapOutManifestation(_manifestation, newManifestation, RetirementVFX.None);
                    _manifestation.InitialiseVisuals(Verb);
            }

            TokenRectTransform.sizeDelta=new Vector2(ManifestationRectTransform.sizeDelta.x,ManifestationRectTransform.sizeDelta.y);

        }

        public virtual void Manifest()
        {

            if (ElementStack.IsValidElementStack())
            {

                if (_manifestation.GetType() != Sphere.ElementManifestationType)
                    Manifest(Sphere.ElementManifestationType);
            }
            else
                Manifest(Sphere.SituationManifestationType);
            

        }

        /// <summary>
        /// replaces one manifestation with an identical manifestation - so for example we can do a vfx retiring the old one
        /// </summary>
        /// <param name="vfx"></param>
        public virtual void Remanifest(RetirementVFX vfx)
        {
            var reManifestation = Registry.Get<PrefabFactory>()
                .CreateManifestationPrefab(_manifestation.GetType(), this.transform);

            reManifestation.Transform.position=_manifestation.Transform.position;

            // Put it behind the old card that we're about to destroy showily
            reManifestation.Transform.SetSiblingIndex(_manifestation.Transform.GetSiblingIndex() - 1);

            SwapOutManifestation(_manifestation,reManifestation,vfx);

           Manifest();
        }


        public void TryReturnToOriginalPosition()
        {
            if (LastTablePos != null)
                transform.localPosition = new Vector3(LastTablePos.Value.x, LastTablePos.Value.y);
        }



        public void Start()
        {
            if (Sphere.GetType() != typeof(CardsPile))
                Registry.Get<LocalNexus>().TokenInteractionEvent.AddListener(ReactToDraggedToken);

            SetXNess(TokenXNess.NoValidDestination);

        }

        public void SetXNess(TokenXNess xness)
        {
            TokenXNess = xness;
        }


        public bool ShouldReturnToStart()
        {
            return TokenXNess == TokenXNess.NoValidDestination ||
                   TokenXNess == TokenXNess.DivertedByGreedySlot ||
                   TokenXNess == TokenXNess.ReturningSplitStack ||
                   TokenXNess == TokenXNess.ReturnedToStartingSlot ||
                   TokenXNess == TokenXNess.DroppedOnTokenWhichWontMoveAside;
        }

        protected  bool AllowsDrag()
        {
            return !IsInMotion && !shrouded && !_manifestation.RequestingNoDrag;

        }



        public void SnapToGrid()
        {
            transform.localPosition = Registry.Get<Choreographer>().SnapToGrid(transform.localPosition);
        }

        public void SetSphere(Sphere newSphere, Context context)
        {
            OldSphere = Sphere;
            Sphere = newSphere;

            if (OldSphere != null && OldSphere != newSphere)
            {
                OldSphere.RemoveToken(this);
                if (OldSphere.ContentsHidden && !newSphere.ContentsHidden)
                    _manifestation.UpdateVisuals(Element,ElementQuantity);
            }

            Sphere = newSphere;
        }

        public bool IsInContainer(Sphere compareContainer, Context context)
        {
            return compareContainer == Sphere;
        }


        public void OnBeginDrag(PointerEventData eventData)
        {
            NoonUtility.Log("Beginning drag for " + this.name,0,VerbosityLevel.SystemChatter);

            if (CanDrag(eventData))
            {
                _currentlyBeingDragged = true;
                Registry.Get<LocalNexus>().SignalTokenBeginDrag(this, eventData);
                StartDrag(eventData);
            }

        }

        bool CanDrag(PointerEventData eventData)
        {

            if (!Sphere.AllowDrag || !AllowsDrag())
                return false;


            return true;
        }

        protected void StartDrag(PointerEventData eventData)
        {
            

            if (!Keyboard.current.shiftKey.wasPressedThisFrame)
            {
                if (ElementStack.IsValidElementStack() && ElementQuantity > 1)
                    CalveTokenAtSamePosition(ElementQuantity - 1, new Context(Context.ActionSource.PlayerDrag));
                
            }

            _currentlyBeingDragged = true;

            var enrouteContainer = Registry.Get<SphereCatalogue>().GetContainerByPath(
                new SpherePath(Registry.Get<Compendium>().GetSingleEntity<Dictum>().DefaultEnRouteSpherePath));

            enrouteContainer.AcceptToken(this, new Context(Context.ActionSource.PlayerDrag));
            
            TokenRectTransform.SetAsLastSibling();
            _manifestation.OnBeginDragVisuals();

            TokenXNess = TokenXNess.NoValidDestination;
            canvasGroup.blocksRaycasts = false;

            startPosition = TokenRectTransform.anchoredPosition3D;

            startParent = TokenRectTransform.parent;
            startSiblingIndex = TokenRectTransform.GetSiblingIndex();

            if (TokenRectTransform.anchoredPosition.sqrMagnitude > 0.0f
            ) // Never store 0,0 as that's a slot position and we never auto-return to slots - CP
            {
                LastTablePos = TokenRectTransform.anchoredPosition3D;
            }


            //commented out because I *might* not need it; but if I do, we can probably calculate it on the fly.
            //if (this.EntityId=="dropzone")
            //{
            //    Vector3 pressPos;
            //    RectTransformUtility.ScreenPointToWorldPointInRectangle(Registry.Get<IDraggableHolder>().RectTransform, eventData.pressPosition, eventData.pressEventCamera, out pressPos);
            //    dragOffset = (startPosition + startParent.position) - pressPos;
            //}
            //else
            //{
            dragOffset = Vector3.zero;
            //  }

            SoundManager.PlaySfx("CardPickup");
            TabletopManager.RequestNonSaveableState(TabletopManager.NonSaveableType.Drag, true);

        }



        public void OnDrag(PointerEventData eventData)
        {
            if (!_currentlyBeingDragged)
                return;

            MoveObject(eventData);


            Sphere.OnTokenDragged(new TokenEventArgs {PointerEventData = eventData,Token=this,Sphere= Sphere});

        }



        public void MoveObject(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(Sphere.GetRectTransform(),
                eventData.position, eventData.pressEventCamera, out var draggedToPosition);

            
            // Potentially change this so it is using UI coords and the RectTransform?
            //  rectTransform.position = new Vector3(dragPos.x + dragOffset.x, dragPos.y + dragOffset.y, dragPos.z + dragHeight);

         TokenRectTransform.position = draggedToPosition; ///aaaahh it's *position* not anchoredposition3D because we're getting the world point from the click

            
            _manifestation.DoMove(ManifestationRectTransform);

            // rotate object slightly based on pointer Delta
            if (rotateOnDrag && eventData.delta.sqrMagnitude > 10f)
            {
                // This needs some tweaking so that it feels more responsive, physica. Card rotates into the direction you swing it?
                perlinRotationPoint += eventData.delta.sqrMagnitude * 0.001f;
                transform.localRotation =
                    Quaternion.Euler(new Vector3(0, 0, -10 + Mathf.PerlinNoise(perlinRotationPoint, 0) * 20));
            }



        }


        public  void OnEndDrag(PointerEventData eventData)
        {
            NoonUtility.Log("Ending drag for " + this.name, 0, VerbosityLevel.SystemChatter);

            Registry.Get<LocalNexus>().SignalTokenEndDrag(this, eventData);
            FinishDrag();
        }


        public  void FinishDrag()
        {
            if (_currentlyBeingDragged)
            {
                _currentlyBeingDragged = false;
                canvasGroup.blocksRaycasts = true;

                if (ShouldReturnToStart())
                    ReturnToStartPosition();

                TabletopManager.RequestNonSaveableState(TabletopManager.NonSaveableType.Drag,
                    false); // There is also a failsafe to catch unexpected aborts of Drag state - CP

            }
        }

        public void ReturnToStartPosition()
        {
            if (startParent == null)
            {
                //newly created token! If we try to set it to startposition, it'll disappear into strange places
                ReturnToTabletop(new Context(Context.ActionSource.PlayerDrag));
                return; // no sound on new token
            }

            SoundManager.PlaySfx("CardDragFail");
            var tabletopContainer = startParent.GetComponent<TabletopSphere>();

            // Token was from tabletop - return it there. This auto-merges it back in case of ElementStacks
            // The map is not the tabletop but inherits from it, so we do the IsTabletop check
            if (tabletopContainer != null && tabletopContainer.IsTabletop)
            {
                ReturnToTabletop(new Context(Context.ActionSource.PlayerDrag));
            }
            else
            {
                TokenRectTransform.anchoredPosition3D = startPosition;

                TokenRectTransform.localRotation = Quaternion.identity;
                TokenRectTransform.SetParent(startParent);
                TokenRectTransform.SetSiblingIndex(startSiblingIndex);
                
            }
        }

        public  void OnDrop(PointerEventData eventData)
        {

            var incomingToken = eventData.pointerDrag.GetComponent<Token>();
            if (incomingToken == null)
                return;

            if (CanInteractWithIncomingToken(incomingToken))
                InteractWithIncomingToken(incomingToken, eventData);
            else
            {
                this.Sphere.TryMoveAsideFor(this, incomingToken, out bool moveAsideFor);

                if (moveAsideFor)
                    SetXNess(TokenXNess.DroppedOnTokenWhichMovedAside);
                else
                    SetXNess(TokenXNess.DroppedOnTokenWhichWontMoveAside);
            }
        }

        public bool CanInteractWithIncomingToken(Token incomingToken)
        {
            //can we merge tokens?
            if (incomingToken.ElementStack.CanMergeWith(incomingToken.ElementStack))
                return true;

            //can we put a stack in a threshold associated with this token?
            if (_attachedToSituation.GetFirstAvailableThresholdForStackPush(incomingToken.ElementStack).SphereCategory ==
                SphereCategory.Threshold)
                return true;

            return false;
        }

        private void InteractWithIncomingToken(Token incomingToken, PointerEventData eventData)
        {
            Sphere.OnTokenReceivedADrop(new TokenEventArgs
            {
                Token = this,
                Sphere = Sphere,
                PointerEventData = eventData
            });
            if (ElementStack.IsValidElementStack() && incomingToken.ElementStack.IsValidElementStack())
            {
                if (incomingToken.ElementStack.CanMergeWith(incomingToken.ElementStack))
                    ElementStack.AcceptIncomingStackForMerge(incomingToken.ElementStack);
                else
                {
                    ElementStack.ShowNoMergeMessage(incomingToken.ElementStack);

                    incomingToken.Sphere.TryMoveAsideFor(this, incomingToken, out bool moveAsideFor);

                    if (moveAsideFor)
                        SetXNess(TokenXNess.DroppedOnTokenWhichMovedAside);
                }
            }

            else if (incomingToken.ElementStack.IsValidElementStack())
            {

                _attachedToSituation.PushDraggedStackIntoThreshold(incomingToken);

                // Then we open the situation (cause this closes other situations and this may return the stack we try to move
                // back onto the tabletop - if it was in its starting slots. - Martin
                if (!_attachedToSituation.IsOpen)
                    _attachedToSituation.OpenAtCurrentLocation();
                return;
            }
            else
            {
                // We can't interact? Then dump us on the tabletop
                incomingToken.ReturnToTabletop(new Context(Context.ActionSource.PlayerDrag));
            }

        }

        public Token CalveTokenAtSamePosition(int quantityToLeaveBehind, Context context)
        {

            if (quantityToLeaveBehind <= 0) //for some reason we're trying to leave an empty stack behind..
                return Sphere.ProvisionElementStackToken(NullElement.NULL_ELEMENT_ID, 0);

            if (ElementQuantity <= quantityToLeaveBehind
            ) //we're trying to leave everything behind. Abort the drag and return the original token, ie this token
            {
                FinishDrag();
                return this;
            }


            var calvedToken =
                Sphere.ProvisionElementStackToken(Element.Id, ElementQuantity - 1, Source.Existing(),
                    new Context(Context.ActionSource.PlayerDrag), ElementStack.GetCurrentMutations());


            this.ElementStack.SetQuantity(ElementQuantity - quantityToLeaveBehind, context);

            // Accepting stack will trigger overlap checks, so make sure we're not in the default pos but where we want to be.
            calvedToken.transform.position = transform.position;

            // Accepting stack may put it to pos Vector3.zero, so this is last
            calvedToken.transform.position = transform.position;
            originToken = calvedToken;
            return calvedToken;

        }

        public  void OnPointerClick(PointerEventData eventData)
        {



            if (!_manifestation.HandleClick(eventData, this))
            {
                //Manifestation didn't handle click
                Registry.Get<DebugTools>().SetInput(_attachedToSituation.RecipeId);

                if (!_attachedToSituation.IsOpen)
                    _attachedToSituation.OpenAtCurrentLocation();
                else
                    _attachedToSituation.Close();
            }



            if (eventData.clickCount > 1)
            {
                // Double-click, so abort any pending single-clicks
                singleClickPending = false;
                Sphere.OnTokenDoubleClicked(new TokenEventArgs
                {
                    Element = ElementStack.Element,
                    Token = this,
                    Sphere = Sphere,
                    PointerEventData = eventData
                });

            }
            else
            {
                // Single-click BUT might be first half of a double-click
                // Most of these functions are OK to fire instantly - just the ShowCardDetails we want to wait and confirm it's not a double
                singleClickPending = true;


                if (shrouded)
                {
                    Unshroud(false);

                }
                else
                {
                    Sphere.OnTokenClicked(new TokenEventArgs
                    {
                        Element = ElementStack.Element,
                        Token = this,
                        Sphere = Sphere,
                        PointerEventData = eventData
                    });
                }

                // this moves the clicked sibling on top of any other nearby cards.
                if (Sphere.GetType() != typeof(RecipeSlot) && Sphere.GetType() != typeof(ExhibitCards))
                    transform.SetAsLastSibling();
            }

        }



        public  void ReturnToTabletop(Context context)
        {
            Registry.Get<Choreographer>().PlaceTokenOnTableAtFreePosition(this, context);


            //if we have an origin stack and the origin stack is on the tabletop, merge it with that.
            //We might have changed the element that a stack is associated with... so check we can still merge it
            if (originToken != null && originToken.Sphere.SphereCategory == SphereCategory.World &&
                ElementStack.CanMergeWith(originToken.ElementStack))
            {
                originToken.ElementStack.AcceptIncomingStackForMerge(this.ElementStack);
                return;
            }
            else
            {
                var tabletop = Registry.Get<TabletopManager>()._tabletop;
                var existingElementTokens = tabletop.GetElementTokens();

                //check if there's an existing stack of that type to merge with
                foreach (var elementToken in existingElementTokens)
                {
                    if (this.ElementStack.CanMergeWith(elementToken.ElementStack))
                    {
                        elementToken.ElementStack.AcceptIncomingStackForMerge(this.ElementStack);

                        _manifestation.Highlight(HighlightType.AttentionPls);
                        return;
                    }
                }
            }

            if(LastTablePos!=null)
                Registry.Get<Choreographer>()
                .PlaceTokenAsCloseAsPossibleToSpecifiedPosition(this, context, LastTablePos.Value);

        }


        public void DisplayOverrideIcon(string icon)
        {
            _manifestation.OverrideIcon(icon);
        }

        protected void NotifyChroniclerPlacedOnTabletop()
        {
            Registry.Get<Chronicler>()?.TokenPlacedOnTabletop(this);
        }

        public  bool Retire()
        {
            return Retire(RetirementVFX.None);
        }

        public  bool Retire(RetirementVFX vfx)
        {
            if (Defunct)
                return false;
            Defunct = true;
            FinishDrag(); // Make sure we have the drag aborted in case we're retiring mid-drag (merging stack frex)

            _manifestation.Retire(vfx, OnManifestationRetired);
            ElementStack.Retire(vfx);
            Sphere.NotifyTokensChangedForSphere(new TokenEventArgs { Element = Element, Token = this, Sphere = Sphere });  // Notify tabletop that aspects will need recompiling

            SetSphere(Registry.Get<NullSphere>(), new Context(Context.ActionSource.Retire));

            return true;
        }

        private void OnManifestationRetired()
        {

            Destroy(this.gameObject);
        }



        public virtual void ReactToDraggedToken(TokenInteractionEventArgs args)
        {
            if (Defunct)
                return;

            if (args.TokenInteractionType == TokenInteractionType.BeginDrag)
            {
                _manifestation.Highlight(HighlightType.CanInteractWithOtherToken);
            }

            if (args.TokenInteractionType == TokenInteractionType.EndDrag)
                _manifestation.Unhighlight(HighlightType.CanInteractWithOtherToken);

        }

        public  void HighlightPotentialInteractionWithToken(bool show)
        {
            if (show)
                _manifestation.Highlight(HighlightType.CanInteractWithOtherToken);
            else
                _manifestation.Unhighlight(HighlightType.CanInteractWithOtherToken);


        }


        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!eventData.dragging)
                _manifestation.Highlight(HighlightType.Hover);

            var tabletopManager = Registry.Get<TabletopManager>();
            if (tabletopManager != null
            ) //eg we might have a face down card on the credits page - in the longer term, of course, this should get interfaced
            {
                if (!shrouded && ElementStack.IsValidElementStack())
                    tabletopManager.SetHighlightedElement(Element.Id, ElementQuantity);
                else
                    tabletopManager.SetHighlightedElement(null);
            }


        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!eventData.dragging)
                _manifestation.Unhighlight(HighlightType.Hover);

            Sphere.OnTokenPointerExited(new TokenEventArgs
            {
                Element = Element,
                Token = this,
                Sphere = Sphere,
                PointerEventData = eventData
            });


            var ttm = Registry.Get<TabletopManager>();
            if (ttm != null)
            {
                Registry.Get<TabletopManager>().SetHighlightedElement(null);
            }

        }

        public void BurnImageUnderToken(string burnImage)
        {
            Registry.Get<INotifier>()
                .ShowImageBurn(burnImage, this, 20f, 2f,
                    TabletopImageBurner.ImageLayoutConfig.CenterOnToken);
        }

        public void TravelTo(TokenTravelItinerary itinerary)
        {
            Itinerary = itinerary;
            _manifestation.TravelTo(this, itinerary.Duration, itinerary.startPos, itinerary.endPos, TravelComplete, itinerary.startScale, itinerary.endScale);
        }

        private void TravelComplete()
        {
            Itinerary.DestinationSphere.AcceptToken(this,new Context(Context.ActionSource.AnimEnd));
            //this will (at time of writing) call Sphere.DisplayHere->Token.Manifest, which also resets itinerary, so this *should* be the only line we need at the end of the animation
        }


        public virtual void SituationStateChanged(Situation situation)
        {
         
                    if (situation.CurrentBeginningEffectCommand != null)
                    {
                        //if (situation.CurrentBeginningEffectCommand.OngoingSlots.Any())
                        //    _manifestation.DisplaySpheres(situation.CurrentBeginningEffectCommand.OngoingSlots[0].Greedy);
                        if (!string.IsNullOrEmpty(situation.CurrentBeginningEffectCommand.BurnImage))
                            BurnImageUnderToken(situation.CurrentBeginningEffectCommand.BurnImage);
                    }


                    _manifestation.DisplaySpheres(situation.GetSpheresActiveForCurrentState());
        }

        public void TimerValuesChanged(Situation situation)
        {
            _manifestation.UpdateTimerVisuals(situation.Warmup, situation.TimeRemaining,
                situation.IntervalForLastHeartbeat, false, situation.CurrentPrimaryRecipe.SignalEndingFlavour);

        }

        public virtual void ElementStackStateUpdated(ElementStack stack)
        {

            _manifestation.UpdateVisuals(stack.Element,stack.Quantity);
            _manifestation.UpdateTimerVisuals(stack.Element.Lifetime,stack.LifetimeRemaining,stack.IntervalForLastHeartbeat,Element.Resaturate,EndingFlavour.None);
            PlacementAlreadyChronicled = false; //should really only do this if the element has changed

            Sphere.NotifyTokensChangedForSphere(new TokenEventArgs { Token = this, Element = Element, Sphere = Sphere });
        }

    public void SphereContentsUpdated(Situation situation)
        {
            var thresholdSpheresWithStacks = situation.GetSpheresByCategory(SphereCategory.Threshold)
                .Where(sphere => sphere.GetElementTokens().Count() == 1);

            _manifestation.DisplaySpheres(thresholdSpheresWithStacks);


            int completionCount = situation.GetStacks(SphereCategory.Output).Select(s => s.Quantity).Sum();
            _manifestation.SendNotification(new Notification(string.Empty, completionCount.ToString()));
        }

        public void ReceiveNotification(INotification n)
        {
           NoonUtility.Log("ReceiveNotification on Token: use it or lose it");
        }

        public void Understate()
        {
            _manifestation.Understate();
        }

        public void Emphasise()
        {
            _manifestation.Emphasise();
        }


        public void Unshroud(bool instant = false)
        {
            shrouded = false;
            _manifestation.Reveal(instant);

            //if a card has just been turned face up in a situation, it's now an existing, established card
            if (ElementStack.StackSource.SourceType == SourceType.Fresh)
                ElementStack.StackSource = Source.Existing();

        }

        public void Shroud(bool instant = false)
        {
            shrouded = true;
            _manifestation.Shroud(instant);


        }

        public bool Shrouded()
        {
            return shrouded;
        }


    }
}