﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Core.Commands;
using Assets.Core.Enums;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.CS.TabletopUI.Interfaces;
using Assets.Logic;
using Assets.TabletopUi;
using Assets.TabletopUi.Scripts.Interfaces;
using JetBrains.Annotations;
using UnityEngine.Assertions;

namespace Assets.Core.Entities {

	public class Situation {
		public SituationState State { get; set; }
		public Recipe currentPrimaryRecipe { get; set; }
		public float TimeRemaining { private set; get; }
		public float Warmup { get { return currentPrimaryRecipe.Warmup; } }
		public string RecipeId { get { return currentPrimaryRecipe == null ? null : currentPrimaryRecipe.Id; } }
        public readonly IVerb Verb;
        private List<ISituationSubscriber> subscribers=new List<ISituationSubscriber>();
		private HashSet<ITokenContainer>_containers=new HashSet<ITokenContainer>();
        public string OverrideTitle { get; set; }
        public int CompletionCount { get; set; }

        private SituationController _TEMP;


		public Situation(SituationCreationCommand command)
        {
            Verb = command.GetBasicOrCreatedVerb();
            TimeRemaining = command.TimeRemaining ?? 0;
            State = command.State;
            currentPrimaryRecipe = command.Recipe;
            OverrideTitle = command.OverrideTitle;
            CompletionCount = command.CompletionCount;
        }



        public Situation()
        {
            State = SituationState.Unstarted;
        }



		public bool AddSubscriber(ISituationSubscriber subscriber)
        {
            if (subscriber.GetType() == typeof(SituationController))
                _TEMP = (SituationController)subscriber;

            if (subscribers.Contains(subscriber))
                return false;

			subscribers.Add(subscriber);
            return true;
        }

        public bool RemoveSubscriber(ISituationSubscriber subscriber)
        {
            if (!subscribers.Contains(subscriber))
                return false;

            subscribers.Remove(subscriber);
            return true;
        }


        public void AddContainer(ITokenContainer container)
        {
            _containers.Add(container);
        }

		public IList<SlotSpecification> GetSlotsForCurrentRecipe() {
			if (currentPrimaryRecipe.Slots.Any())
				return currentPrimaryRecipe.Slots;
			else
				return new List<SlotSpecification>();
		}



		private void Reset() {
			currentPrimaryRecipe = null;
			TimeRemaining = 0;
			State = SituationState.Unstarted;
			foreach(var subscriber in subscribers)
			    subscriber.ResetSituation();
		}

		public void Halt()
		{
			if(State!=SituationState.Complete && State!=SituationState.Unstarted) //don't halt if the situation is not running. This is not only superfluous but dangerous: 'complete' called from an already completed verb has bad effects
			    Complete();
		}

        public void StartRecipe(Recipe recipe)
        {
            currentPrimaryRecipe = recipe;
    Start();
        }

		public void Start() {
			TimeRemaining = currentPrimaryRecipe.Warmup;
			State = SituationState.FreshlyStarted;
		}


		public void ResetIfComplete() {
			if (State == SituationState.Complete)
				Reset();
		}




		public string GetTitle() {
			return currentPrimaryRecipe == null ? "no recipe just now" :
			currentPrimaryRecipe.Label;
		}

		public string GetStartingDescription() {
			return currentPrimaryRecipe == null ? "no recipe just now" :
			currentPrimaryRecipe.StartDescription;
		}

		public string GetDescription() {
			return currentPrimaryRecipe == null ? "no recipe just now" :
			currentPrimaryRecipe.Description;
		}


        public HeartbeatResponse ExecuteHeartbeat(float interval)
        {
            HeartbeatResponse response = new HeartbeatResponse();
            var ttm = Registry.Get<TabletopManager>();
            var aspectsInContext = ttm.GetAspectsInContext(GetAspectsAvailableToSituation(true));

            RecipeConductor rc = new RecipeConductor(compendium,
                aspectsInContext, Registry.Get<IDice>(), currentCharacter);

            Situation.Continue(rc, interval, greedyAnimIsActive);

            // only pull in something if we've got a second remaining
            if (Situation.State == SituationState.Ongoing && Situation.TimeRemaining > HOUSEKEEPING_CYCLE_BEATS)
            {
                var tokenAndSlot = new TokenAndSlot()
                {
                    Token = situationAnchor as VerbAnchor,
                    RecipeSlot = situationWindowAsStorage.GetUnfilledGreedySlot() as RecipeSlot
                };

                if (tokenAndSlot.RecipeSlot != null && !tokenAndSlot.Token.Defunct && !tokenAndSlot.RecipeSlot.Defunct)
                {
                    response.SlotsToFill.Add(tokenAndSlot);
                }
            }

            return response;
        }


		public SituationState Continue(IRecipeConductor rc, float interval, bool waitForGreedyAnim = false)
		{
			if (State == SituationState.RequiringExecution)
			{
				End(rc);
			}
			else if (State == SituationState.Ongoing)
			{
				// Execute if we've got no time remaining and we're not waiting for a greedy anim
				// UNLESS timer has gone negative for 5 seconds. In that case sometime is stuck and we need to break out
				if (TimeRemaining <= 0 && (!waitForGreedyAnim || TimeRemaining < -5.0f))
				{
					RequireExecution(rc);
				}
				else
				{
					TimeRemaining = TimeRemaining - interval;
					Ongoing();
				}
			}
			else if (State == SituationState.FreshlyStarted)
			{
				Beginning(currentPrimaryRecipe);
			}
			/*
			else if (State == SituationState.Unstarted || State == SituationState.Complete) {
				//do nothing: it's either not running, or it's finished running and waiting for user action
			}
			*/

			return State;
		}

		public RecipePrediction GetPrediction(IRecipeConductor rc) {
			var rp = rc.GetRecipePrediction(currentPrimaryRecipe);

			return rp;
		}

		public void Beginning(Recipe withRecipe) {
			State = SituationState.Ongoing;

			SituationEventData d=SituationEventData.Create(this,_TEMP);

            foreach (var subscriber in subscribers)
			subscriber.SituationBeginning(d);
		}

		private void Ongoing() {

            SituationEventData d = SituationEventData.Create(this, _TEMP);

			State = SituationState.Ongoing;
            foreach (var subscriber in subscribers)
			    subscriber.SituationOngoing(d);
		}

		private void RequireExecution(IRecipeConductor rc) {
			State = SituationState.RequiringExecution;

			IList<RecipeExecutionCommand> recipeExecutionCommands = rc.GetActualRecipesToExecute(currentPrimaryRecipe);

			//actually replace the current recipe with the first on the list: any others will be additionals,
			//but we want to loop from this one.
			if (recipeExecutionCommands.First().Recipe.Id != currentPrimaryRecipe.Id)
				currentPrimaryRecipe = recipeExecutionCommands.First().Recipe;

			foreach (var c in recipeExecutionCommands) {
				SituationEffectCommand ec = new SituationEffectCommand(c.Recipe, c.Recipe.ActionId != currentPrimaryRecipe.ActionId,c.Expulsion);
                foreach (var subscriber in subscribers)
                {
                    SituationEventData d = SituationEventData.Create(this, _TEMP);
                    d.EffectCommand = ec;
                    subscriber.SituationExecutingRecipe(d);
				}
				
			}
		}

		private void End(IRecipeConductor rc) {
			

			var linkedRecipe = rc.GetLinkedRecipe(currentPrimaryRecipe);
			
			if (linkedRecipe!=null) {
				//send the completion description before we move on
				INotification notification = new Notification(currentPrimaryRecipe.Label, currentPrimaryRecipe.Description);
                foreach (var subscriber in subscribers)
                {
                    var d = SituationEventData.Create(this, _TEMP);
                    d.Notification = notification;
				    subscriber.ReceiveAndRefineTextNotification(d);
                }
				currentPrimaryRecipe = linkedRecipe;
				TimeRemaining = currentPrimaryRecipe.Warmup;
				if(TimeRemaining>0) //don't play a sound if we loop through multiple linked ones
				{
					if (currentPrimaryRecipe.SignalImportantLoop)
						SoundManager.PlaySfx("SituationLoopImportant");
					else
						SoundManager.PlaySfx("SituationLoop");

				}
				Beginning(currentPrimaryRecipe);
			}
			else { 
				Complete();
			}
		}

		private void Complete() {
			State = global::SituationState.Complete;
            foreach (var subscriber in subscribers)
            {
                var d = SituationEventData.Create(this, _TEMP);
                subscriber.SituationComplete(d);
            }

            SoundManager.PlaySfx("SituationComplete");
		}

    }

}
