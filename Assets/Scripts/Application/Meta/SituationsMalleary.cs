﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Application.Commands.SituationCommands;
using SecretHistories.Commands;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Application.Meta
{
   public class SituationsMalleary: MonoBehaviour
   {
       [SerializeField] private AutoCompletingInput input;
       [SerializeField] private ThresholdSphere situationDrydockThreshold;

       public void Awake()
       {
           var sphereSphec = new SphereSpec();
           sphereSphec.SetId("situationsmalleary");
           sphereSphec.Label = "Malleary: Situations";
           sphereSphec.AllowAnyToken = true;
     var spherePath=new SpherePath(SituationPath.Root(),sphereSphec.Id);
           
           situationDrydockThreshold.Initialise(sphereSphec, spherePath);
       }


        public void CreateSituation()
       {
           string recipeId = input.text;

           var compendium = Watchman.Get<Compendium>();
           var recipe = compendium.GetEntityById<Recipe>(recipeId.Trim());

           if (!recipe.IsValid())
               return;

    
           SituationCreationCommand newSituationCommand = new SituationCreationCommand(recipe.ActionId, recipe.Id, new SituationPath(recipe.ActionId), StateEnum.Ongoing);

           //assuming we want the whole lifetime of the recipe
           newSituationCommand.TimeRemaining = recipe.Warmup;

           var newTokenLocation = new TokenLocation(0f, 0f, 0f, situationDrydockThreshold.GetPath());
           var newTokenCommand = new TokenCreationCommand(newSituationCommand, newTokenLocation);
           newTokenCommand.Execute(new Context(Context.ActionSource.Debug));

    
       }

        public void TimeForwards()
        {
            situationDrydockThreshold.RequestTokensSpendTime(Heart.BEAT_INTERVAL_SECONDS * 50);
        }

        public void TimeBackwards()
        {
            situationDrydockThreshold.RequestTokensSpendTime(Heart.BEAT_INTERVAL_SECONDS * -50);
        }

        public void AdvanceTimeToEndOfRecipe()
        {
            var situation = situationDrydockThreshold.GetTokenInSlot().Payload;
            var timeRemaining = situation.GetTimeshadow().LifetimeRemaining;
            if(timeRemaining>0)
                situationDrydockThreshold.RequestTokensSpendTime(timeRemaining);
        }
    }
}
