﻿using System;
using Assets.Core.Commands;
using Assets.Core.Entities;
using Assets.Core.Enums;
using Assets.TabletopUi.Scripts.Infrastructure;

namespace Assets.Core.States
{
    public class CompleteState : SituationState
    {
        public override bool Extinct => false;
        public override void Enter(Situation situation)
        {
         
            var outputTokens = situation.GetTokens(SphereCategory.SituationStorage);
            situation.AcceptTokens(SphereCategory.Output, outputTokens, new Context(Context.ActionSource.SituationResults));

            situation.AttemptAspectInductions(situation.CurrentPrimaryRecipe, outputTokens);
            SoundManager.PlaySfx("SituationComplete"); //this could run through that Echo obj
        }

        public override void Exit(Situation situation)
        {
       
        }

        public override bool IsActiveInThisState(Sphere s)
        {
            if (s.SphereCategory == SphereCategory.Output)
                return true;

            return false;
        }

        public override bool IsValidPredictionForState(Recipe recipeToCheck, Situation s)
        {
            return false;
        }

        public override void Continue(Situation situation)
        {
            situation.CommandQueue.ExecuteCommandsFor(CommandCategory.Output, situation);
        }
    }
}