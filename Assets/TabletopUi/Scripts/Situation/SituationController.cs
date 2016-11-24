using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Core;
using Assets.Core.Commands;
using Assets.Core.Entities;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.CS.TabletopUI.Interfaces;
using Assets.TabletopUi.Scripts.Interfaces;
using UnityEngine;

namespace Assets.TabletopUi
{
   public  class SituationController:ISituationSubscriber
   {
       public SituationToken situationToken;
       private SituationWindow situationWindow;
       public Situation situation;



       public void InitialiseToken(SituationToken t,IVerb v)
       {
            situationToken = t;
            t.Initialise(v, this);
        }

       public void InitialiseWindow(SituationWindow w)
       {
            situationWindow = w;
           w.Initialise(this);
       }
    

       public void Open()
       {
            situationWindow.transform.position = situationToken.transform.position;
            situationWindow.Show();

            situationToken.situationStorage.gameObject.SetActive(true);
            if (situation!=null)
                situationWindow.DisplayOngoing();
            else
                situationWindow.DisplayStarting();


            situationToken.IsOpen = true;
        }


        public void Close()
        {
            situationToken.situationStorage.gameObject.SetActive(false);
            situationWindow.Hide();
            situationToken.IsOpen=false;
        }


       public void UpdateAspectsDisplay()
       {
            Debug.Log("fix this");
       }

       public void UpdateSituationDisplay()
        {
            var allAspects = GetAspectsAvailableToSituation();

            RecipeConductor rc = new RecipeConductor(Registry.Compendium, allAspects,
            new Dice());
            string prediction= situation.GetPrediction(rc);

            situationWindow.DisplaySituation(situation.GetTitle(),situation.GetDescription(),prediction);
        }

       private AspectsDictionary GetAspectsAvailableToSituation()
       {
           AspectsDictionary existingAspects = situationToken.GetAspectsFromStoredElements();

           AspectsDictionary additionalAspects = situationToken.GetAspectsFromSlottedElements();

           AspectsDictionary allAspects = new AspectsDictionary();
           allAspects.CombineAspects(existingAspects);
           allAspects.CombineAspects(additionalAspects);
           return allAspects;
       }


       public void DisplayStartingRecipe()
        {
            AspectsDictionary startingAspects = situationWindow.GetAspectsFromSlottedElements();

            var r = Registry.Compendium.GetFirstRecipeForAspectsWithVerb(startingAspects, situationToken.VerbId);

            situationWindow.DisplayRecipe(r);
        }


        public HeartbeatResponse ExecuteHeartbeat(float interval)
        {
            HeartbeatResponse response=new HeartbeatResponse();
            if (situation != null)
            {
                RecipeConductor rc = new RecipeConductor(Registry.Compendium,
                GetAspectsAvailableToSituation(), new Dice());
                situation.Continue(rc, interval);
                response.SlotsToFill = situationToken.GetUnfilledGreedySlots();
            }

            return response;
        }

       public void SituationBeginning(Situation s)
       {

           situationToken.BuildSlots(s.GetSlots());
            situationToken.SetTimerVisibility(true);
            SituationOngoing(s);

            RecipeConductor rc = new RecipeConductor(Registry.Compendium, situationToken.GetSituationStorageStacksManager().GetTotalAspects(),
            new Dice());

           string nextRecipePrediction = situation.GetPrediction(rc);


            situationWindow.DisplaySituation(s.GetTitle(),s.GetDescription(), nextRecipePrediction);
       }

        public void SituationOngoing(Situation s)
        {
            situationToken.DisplayTimeRemaining(s.Warmup, s.TimeRemaining);
        }



        public void SituationExecutingRecipe(IEffectCommand command)
       {
            //move any elements currently in OngoingSlots to situation storage
            //NB we're doing this *before* we execute the command - the command may affect these elements too
           var inputStacks = situationToken.GetStacksInOngoingSlots();
           var storageGateway = situationToken.GetSituationStorageStacksManager();
            storageGateway.AcceptStacks(inputStacks);

            //execute each recipe in command
            foreach (var kvp in command.GetElementChanges())
            {
               situationToken.GetSituationStorageStacksManager().ModifyElementQuantity(kvp.Key, kvp.Value);
            }

        }

       public void SituationExtinct()
       {
            IElementStacksManager storedStacksManager = situationToken.GetSituationStorageStacksManager();

            //currently just retrieving everything
            var stacksToRetrieve = storedStacksManager.GetStacks();

            INotification notification=new Notification(situation.GetTitle(),situation.GetDescription());
            situationWindow.AddOutput(stacksToRetrieve,notification);

            situationToken.SetTimerVisibility(false);
            situation = null;
        }

        public void BeginSituation(Recipe r)
        {
            situation = new Situation(r);
            situation.Subscribe(this);

        }


        public void AttemptActivateRecipe()
       {
            var aspects =situationWindow.GetAspectsFromSlottedElements();
            var recipe = Registry.Compendium.GetFirstRecipeForAspectsWithVerb(aspects, situationToken.VerbId);
            if (recipe != null)
            {
                var containerGateway = situationToken.GetSituationStorageStacksManager();
                var stacksToStore = situationWindow.GetStacksInStartingSlots();
                containerGateway.AcceptStacks(stacksToStore);

                BeginSituation(recipe);
                situationWindow.DisplayOngoing();
            }
        }
   }
}
