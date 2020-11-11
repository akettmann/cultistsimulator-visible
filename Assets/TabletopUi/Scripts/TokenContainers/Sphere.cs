﻿using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Core;
using Assets.Core.Commands;
using Assets.Core.Entities;
using Assets.Core.Enums;
using Assets.Core.Fucine;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.CS.TabletopUI.Interfaces;
using Assets.Logic;
using Assets.TabletopUi.Scripts.Elements;
using Assets.TabletopUi.Scripts.Elements.Manifestations;
using Assets.TabletopUi.Scripts.Infrastructure.Events;
using Assets.TabletopUi.Scripts.Interfaces;
using Assets.TabletopUi.Scripts.Services;
using Noon;
using UnityEngine;
using UnityEngine.Events;

namespace Assets.TabletopUi.Scripts.Infrastructure {


    public enum BlockReason
    {
      StackEnRouteToContainer
    }

    public enum BlockDirection
    {
        None,
        Inward,
        Outward,
        All
    }

    /// <summary>
    /// blocking entry/exit
    /// </summary>
    public class ContainerBlock
    {
        public BlockDirection BlockDirection { get; }
        public BlockReason BlockReason { get; }

        public ContainerBlock(BlockDirection direction, BlockReason reason)
        {
            BlockDirection = direction;
            BlockReason = reason;
        }
    }

    public abstract class Sphere : MonoBehaviour, ISphereEventSubscriber
    {
        public virtual Type DropzoneType => typeof(DropzoneManifestation);
        public virtual Type ElementManifestationType => typeof(CardManifestation);

        public virtual bool AllowDrag { get; private set; }
        public virtual bool AllowStackMerge => true;
        public virtual bool AlwaysShowHoverGlow => true;
        public virtual bool PersistBetweenScenes => false;
        public virtual bool EnforceUniqueStacksInThisContainer => true;
        public virtual bool ContentsHidden => false;
        public virtual bool IsGreedy => false;
        public abstract ContainerCategory ContainerCategory { get; }
        public SlotSpecification GoverningSlotSpecification { get; set; }

        public SphereCatalogue Catalogue
        {
            get
            {
                if (_catalogue == null)
                {
                    _catalogue = Registry.Get<SphereCatalogue>();
                    _catalogue.RegisterSphere(this);
                }

                return _catalogue;
            }
        }


        protected List<INotifier> _notifiersForContainer = new List<INotifier>();
        public bool Defunct { get; protected set; }
        protected HashSet<ContainerBlock> _currentContainerBlocks = new HashSet<ContainerBlock>();
        private SphereCatalogue _catalogue;
        private List<ElementStackToken> _stacks = new List<ElementStackToken>();
        private readonly HashSet<ISphereEventSubscriber> _subscribers = new HashSet<ISphereEventSubscriber>();

        public void Subscribe(ISphereEventSubscriber subscriber)
        {
            _subscribers.Add(subscriber);
        }

        public void Unsubscribe(ISphereEventSubscriber subscriber)
        {
            _subscribers.Remove(subscriber);
        }


        public virtual void OnEnable()
        {
            Catalogue.RegisterSphere(
                this); //this is a double call - we already subscribe above. This should be fine because it's a hashset, and because we may want to disable then re-enable. But FYI, future AK.
        }

        public virtual void OnDisable()
        {
            Catalogue.DeregisterSphere(this);
        }


        public virtual bool Retire()
        {
            RetireAllStacks();
            Destroy(gameObject);
            Defunct = true;
            return true;
        }

        public virtual bool CurrentlyBlockedFor(BlockDirection direction)
        {
            var currentBlockDirection = CurrentBlockDirection();
            return (currentBlockDirection == BlockDirection.All ||
                    currentBlockDirection == direction);
        }

        public BlockDirection CurrentBlockDirection()
        {
            bool inwardblock = _currentContainerBlocks.Any(cb => cb.BlockDirection == BlockDirection.Inward);
            bool outwardBlock = _currentContainerBlocks.Any(cb => cb.BlockDirection == BlockDirection.Outward);
            bool allBlock = _currentContainerBlocks.Any(cb => cb.BlockDirection == BlockDirection.All);

            if (allBlock || (inwardblock && outwardBlock))
                return BlockDirection.All;

            if (inwardblock)
                return BlockDirection.Inward;
            if (outwardBlock)
                return BlockDirection.Outward;

            return BlockDirection.None;
        }

        public bool AddBlock(ContainerBlock block)
        {
            return _currentContainerBlocks.Add(block);
        }

        public int RemoveBlock(ContainerBlock blockToRemove)
        {
            if (blockToRemove.BlockDirection == BlockDirection.All)
                return _currentContainerBlocks.RemoveWhere(cb => cb.BlockReason == blockToRemove.BlockReason);
            else
                return _currentContainerBlocks.RemoveWhere(cb =>
                    cb.BlockDirection == blockToRemove.BlockDirection && cb.BlockReason == blockToRemove.BlockReason);

        }

        public ElementStackToken ProvisionStackFromCommand(StackCreationCommand stackCreationCommand,
            Source stackSource, Context context)
        {
            var stack = ProvisionElementStack(stackCreationCommand.ElementId, stackCreationCommand.ElementQuantity,
                stackSource, context);
            foreach (var m in stackCreationCommand.Mutations)
                stack.SetMutation(m.Key, m.Value, false);

            stack.IlluminateLibrarian = new IlluminateLibrarian(stackCreationCommand.Illuminations);

            if (stackCreationCommand.LifetimeRemaining > 0)
                stack.LifetimeRemaining = stackCreationCommand.LifetimeRemaining;

            if (stackCreationCommand.MarkedForConsumption)
                stack.MarkedForConsumption = true;

            stack.LastTablePos = stackCreationCommand.Location.Position;

            return stack;
        }


        public virtual ElementStackToken ProvisionElementStack(string elementId, int quantity)
        {
            return ProvisionElementStack(elementId, quantity, Source.Existing(),
                new Context(Context.ActionSource.Unknown));
        }


        public virtual ElementStackToken ProvisionElementStack(string elementId, int quantity, Source stackSource,
            Context context)
        {


            var stack = Registry.Get<PrefabFactory>().CreateLocally<ElementStackToken>(transform);

            stack.Populate(elementId, quantity, stackSource);

            AcceptStack(stack, context);

            return stack;
        }

        public virtual ElementStackToken ProvisionNullElementStack(int quantity, Source stackSource, Context context,
            string locatorid = null)
        {

            var limbo = Registry.Get<Limbo>();

            var stack = Registry.Get<PrefabFactory>().CreateLocally<NullElementStackToken>(transform);
            stack.SetSphere(limbo, context);

            if (locatorid != null)
                stack.SaveLocationInfo = locatorid;

            return stack;
        }


        public virtual void DisplayHere(ElementStackToken stack, Context context)
        {
            stack.Manifest(this);
            DisplayHere(stack as AbstractToken, context);
        }

        public virtual void DisplayHere(IToken token, Context context)
        {
            token.transform.SetParent(transform);
            token.transform.localPosition = Vector3.zero;
            token.transform.localRotation = Quaternion.identity;
            token.transform.localScale = Vector3.one;

        }

        public virtual void TryMoveAsideFor(VerbAnchor potentialUsurper, AbstractToken incumbent,
            out bool incumbentMoved)
        {
            // By default: do no move-aside
            incumbentMoved = false;
        }

        public virtual void TryMoveAsideFor(ElementStackToken potentialUsurper, AbstractToken incumbent,
            out bool incumbentMoved)
        {
            // By default: do no move-aside
            incumbentMoved = false;
        }

        public abstract SpherePath GetPath();

        public virtual void OnDestroy()
        {
            Registry.Get<SphereCatalogue>().DeregisterSphere(this);
        }

        public void ModifyElementQuantity(string elementId, int quantityChange, Source stackSource, Context context)
        {
            if (quantityChange > 0)
                IncreaseElement(elementId, quantityChange, stackSource, context);
            else
                ReduceElement(elementId, quantityChange, context);
        }

        /// <summary>
        /// Reduces matching stacks until change is satisfied
        /// </summary>
        /// <param name="elementId"></param>
        /// <param name="quantityChange">must be negative</param>
        /// <returns>returns any unsatisfied change remaining</returns>
        public int ReduceElement(string elementId, int quantityChange, Context context)
        {
            CheckQuantityChangeIsNegative(elementId, quantityChange);

            int unsatisfiedChange = quantityChange;
            while (unsatisfiedChange < 0)
            {
                ElementStackToken stackToAffect =
                    _stacks.FirstOrDefault(c => !c.Defunct && c.GetAspects().ContainsKey(elementId));

                if (stackToAffect == null
                    ) //we haven't found either a concrete matching element, or an element with that ID.
                    //so end execution here, and return the unsatisfied change amount
                    return unsatisfiedChange;

                int originalQuantity = stackToAffect.Quantity;
                stackToAffect.ModifyQuantity(unsatisfiedChange, context);
                unsatisfiedChange += originalQuantity;

            }

            return unsatisfiedChange;
        }


        public static void CheckQuantityChangeIsNegative(string elementId, int quantityChange)
        {
            if (quantityChange >= 0)
                throw new ArgumentException("Tried to call ReduceElement for " + elementId + " with a >=0 change (" +
                                            quantityChange + ")");
        }

        public int IncreaseElement(string elementId, int quantityChange, Source stackSource, Context context,
            string locatorid = null)
        {

            if (quantityChange <= 0)
                throw new ArgumentException("Tried to call IncreaseElement for " + elementId + " with a <=0 change (" +
                                            quantityChange + ")");

            var newStack = ProvisionElementStack(elementId, quantityChange, stackSource, context);
            AcceptStack(newStack, context);
            return quantityChange;
        }

        public IEnumerable<ElementStackToken> GetStacks()
        {
            return _stacks.Where(s => !s.Defunct).ToList();
        }

        public List<string> GetUniqueStackElementIds()
        {
            return _stacks.Select(s => s.EntityId).Distinct().ToList();
        }

        public List<string> GetStackElementIds()
        {
            return _stacks.Select(s => s.EntityId).ToList();
        }


        /// <summary>
        /// All the aspects in all the stacks, summing the aspects
        /// </summary>
        /// <returns></returns>
        public AspectsDictionary GetTotalAspects(bool includingSelf = true)
        {
            return AspectsDictionary.GetFromStacks(_stacks, includingSelf);
        }


        public int GetTotalStacksCount()
        {
            return GetTotalElementsCount(x => true);
        }

        public int GetTotalStacksCountWithFilter(Func<ElementStackToken, bool> filter)
        {

            return _stacks.Count(filter);
        }

        public int GetTotalElementsCount()
        {
            return GetTotalElementsCount(x => true);

        }

        public int GetTotalElementsCount(Func<ElementStackToken, bool> filter)
        {
            return _stacks.Where(filter).Sum(stack => stack.Quantity);

        }



        public int TryPurgeStacks(Element element, int maxToPurge)
        {

            if (string.IsNullOrEmpty(element.DecayTo))
            {
                //nb -p.value - purge max is specified as a positive cap, not a negative, for readability
                return ReduceElement(element.Id, -maxToPurge, new Context(Context.ActionSource.Purge));
            }
            else
            {
                int unsatisfiedChange = maxToPurge;
                while (unsatisfiedChange > 0)
                {

                    //nb: if we transform a stack of >1, it's possible maxToPurge/Transform will be less than the stack total - iwc it'll transform the whole stack. Probably fine.
                    ElementStackToken stackToAffect =
                        _stacks.FirstOrDefault(c => !c.Defunct && c.GetAspects().ContainsKey(element.Id));

                    if (stackToAffect == null
                        ) //we haven't found either a concrete matching element, or an element with that ID.
                        //so end execution here, and return the unsatisfied change amount
                        return unsatisfiedChange;

                    int originalQuantity = stackToAffect.Quantity;
                    stackToAffect.Decay(-1);
                    //stackToAffect.Populate(element.DecayTo, stackToAffect.Quantity, Source.Existing());
                    unsatisfiedChange -= originalQuantity;
                }

                return unsatisfiedChange;
            }



        }


        public virtual void AcceptAnchor(ISituationAnchor anchor, Context context)
        {

            anchor.SetSphere(this, context);
            DisplayHere(anchor,context);
        }

    public virtual void AcceptStack(ElementStackToken stack, Context context)
        {
            
            stack.SetSphere(this, context);

            if (EnforceUniqueStacksInThisContainer)
            {
                var dealer = new Dealer(Registry.Get<Character>());
                if (!String.IsNullOrEmpty(stack.UniquenessGroup))
                    dealer.RemoveFromAllDecksIfInUniquenessGroup(stack.UniquenessGroup);
                if (stack.Unique)
                    dealer.IndicateUniqueCardManifested(stack.EntityId);
            }

            // Check if we're dropping a unique stack? Then kill all other copies of it on the tabletop
            if (EnforceUniqueStacksInThisContainer)
                RemoveDuplicates(stack);

            // Check if the stack's elements are decaying, and split them if they are
            // Decaying stacks should not be allowed
            while (stack.Decays && stack.Quantity > 1)
            {
                AcceptStack(stack.SplitOffNCardsToNewStack(stack.Quantity - 1, context), context);
            }


            //sometimes, we reassign a stack to a container where it already lives. Don't add it again!
            if (!_stacks.Contains(stack))
                _stacks.Add(stack);

            DisplayHere(stack, context);
            NotifyStacksChangedForContainer(new TokenEventArgs { Container = this });

        }


        public bool TryAcceptStackAsThreshold(ElementStackToken stack)
        {

            //does the token match the slot? Check that first
            ContainerMatchForStack match = GetMatchForStack(stack);

            if (match.MatchType != SlotMatchForAspectsType.Okay)
            {
                stack.SetXNess(TokenXNess.DoesntMatchSlotRequirements);
                stack.ReturnToStartPosition();

                var notifier = Registry.Get<INotifier>();

                var compendium = Registry.Get<ICompendium>();

                if (notifier != null)
                    notifier.ShowNotificationWindow(Registry.Get<ILocStringProvider>().Get("UI_CANTPUT"), match.GetProblemDescription(compendium), false);
            }
            else if (stack.Quantity != 1)
            {
                // We're dropping more than one?
                // set main stack to be returned to start position
                stack.SetXNess(TokenXNess.ReturningSplitStack);
                // And we split a new one that's 1 (leaving the returning card to be n-1)
                var newStack = stack.SplitOffNCardsToNewStack(stack.Quantity - 1, new Context(Context.ActionSource.PlayerDrag));
                // And we put that into the slot
                AcceptStack(newStack, new Context(Context.ActionSource.PlayerDrag));
            }
            else
            {
                //it matches. Now we check if there's a token already there, and replace it if so:
                var currentOccupant = GetStacks().FirstOrDefault();

                // if we drop in the same slot where we came from, do nothing.
                if (currentOccupant == stack)
                {
                    stack.SetXNess(TokenXNess.ReturnedToStartingSlot);
                    return false;
                }

                if (currentOccupant != null)
                    NoonUtility.LogWarning("There's still a card in the slot when this reaches the slot; it wasn't intercepted by being dropped on the current occupant. Rework.");
                //currentOccupant.ReturnToTabletop();

                //now we put the token in the slot.
                stack.SetXNess(TokenXNess.PlacedInSlot);
                AcceptStack(stack, new Context(Context.ActionSource.PlayerDrag));
                SoundManager.PlaySfx("CardPutInSlot");
            }

            return true;
        }


        public void RemoveDuplicates(ElementStackToken incomingStack)
        {

            if (!incomingStack.Unique && string.IsNullOrEmpty(incomingStack.UniquenessGroup))
                return;

            foreach (var existingStack in new List<ElementStackToken>(_stacks))
            {

                if (existingStack != incomingStack && existingStack.EntityId == incomingStack.EntityId)
                {
                    NoonUtility.Log(
                        "Not the stack that got accepted, but has the same ID as the stack that got accepted? It's a copy!");
                    existingStack.Retire(RetirementVFX.CardHide);
                    return; // should only ever be one stack to retire!
                    // Otherwise this crashes because Retire changes the collection we are looking at
                }
                else if (existingStack != incomingStack && !string.IsNullOrEmpty(incomingStack.UniquenessGroup))
                {
                    if (existingStack.UniquenessGroup == incomingStack.UniquenessGroup)
                        existingStack.Retire(RetirementVFX.CardHide);

                }
            }
        }

        public void AcceptStacks(IEnumerable<ElementStackToken> stacks, Context context)
        {
            foreach (var eachStack in stacks)
            {
                AcceptStack(eachStack, context);
            }
        }

        /// <summary>
        /// removes the stack from this stack manager; doesn't retire the stack
        /// </summary>
        /// <param name="stack"></param>
        public virtual void RemoveStack(ElementStackToken stack)
        {
            _stacks.Remove(stack);
            NotifyStacksChangedForContainer(new TokenEventArgs {Container = this});
        }

        /// <summary>
        /// removes the stacks from this stack manager; doesn't retire the stack
        /// </summary>
        public void RemoveAllStacks()
        {
            var stacksListCopy = new List<ElementStackToken>(_stacks);
            foreach (ElementStackToken s in stacksListCopy)
                RemoveStack(s);
        }

        public void RetireAllStacks()
        {
            var stacksListCopy = new List<ElementStackToken>(_stacks);
            foreach (ElementStackToken s in stacksListCopy)
                s.Retire(RetirementVFX.None);
        }

        public void RetireStacksWhere(Func<ElementStackToken, bool> filter)
        {
            var stacksToRetire = new List<ElementStackToken>(_stacks).Where(filter);
            foreach (ElementStackToken s in stacksToRetire)
                s.Retire(RetirementVFX.None);
        }


        /// <summary>
        /// This was relevant for a refactoring of the greedy slot code; I decided to do something else
        /// but this code might still be useful elsewhere!
        /// </summary>
        /// <param name="requirement"></param>
        /// <returns></returns>
        public List<ElementStackToken> GetStacksWithAspect(KeyValuePair<string, int> requirement)
        {
            List<ElementStackToken> matchingStacks = new List<ElementStackToken>();
            var candidateStacks = new List<ElementStackToken>(_stacks); //room here for caching
            foreach (var stack in candidateStacks)
            {
                int aspectAtValue = stack.GetAspects(true).AspectValue(requirement.Key);
                if (aspectAtValue >= requirement.Value)
                    matchingStacks.Add(stack);
            }

            return matchingStacks;
        }

        public virtual void ActivatePreRecipeExecutionBehaviour()
        {
            //eg slot consumptions

        }

        public ContainerMatchForStack GetMatchForStack(ElementStackToken stack)
        {
            //no multiple stack is ever permitted in a slot  EDIT: removed this because we have support for splitting the stack to get a single card out - CP
            //			if (stack.Quantity > 1)
            //				return new SlotMatchForAspects(new List<string>{"Too many!"}, SlotMatchForAspectsType.ForbiddenAspectPresent);

            if (stack.EntityId == "dropzone")
                return new ContainerMatchForStack(new List<string>(), SlotMatchForAspectsType.ForbiddenAspectPresent);
            if (GoverningSlotSpecification == null)
                return ContainerMatchForStack.MatchOK();
            else
                return GoverningSlotSpecification.GetSlotMatchForAspects(stack.GetAspects());
        }

        public void NotifyStacksChangedForContainer(TokenEventArgs args)
        {
            Catalogue.NotifyStacksChangedForContainer(args);
            foreach(var s in _subscribers)
                s.NotifyStacksChangedForContainer(args);
        }

        public void OnTokenClicked(TokenEventArgs args)
        {
            Catalogue.OnTokenClicked(args);
            foreach (var s in _subscribers)
                s.OnTokenClicked(args);
        }

        public void OnTokenReceivedADrop(TokenEventArgs args)
        {
            Catalogue.OnTokenReceivedADrop(args);
            foreach (var s in _subscribers)
                s.OnTokenReceivedADrop(args);
        }

        public void OnTokenPointerEntered(TokenEventArgs args)
        {
            Catalogue.OnTokenPointerEntered(args);
            foreach (var s in _subscribers)
                s.OnTokenPointerEntered(args);
        }

        public void OnTokenPointerExited(TokenEventArgs args)
        {
            Catalogue.OnTokenPointerExited(args);
            foreach (var s in _subscribers)
                s.OnTokenPointerExited(args);
        }

        public void OnTokenDoubleClicked(TokenEventArgs args)
        {
            Catalogue.OnTokenDoubleClicked(args);
            foreach (var s in _subscribers)
                s.OnTokenDoubleClicked(args);
        }

        public void OnTokenDragged(TokenEventArgs args)
        {
            Catalogue.OnTokenDragged(args);
            foreach(var s in _subscribers)
                s.OnTokenDragged(args);
        }
    }

}

