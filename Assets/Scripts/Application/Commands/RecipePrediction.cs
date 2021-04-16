﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Services;

namespace SecretHistories.Commands
{
   public class RecipePrediction: IEquatable<RecipePrediction>,INotification
   {
       private readonly Recipe _predictingRecipe;
       public string RecipeId => _predictingRecipe.Id;
        public string Title { get; protected set; }
        public string Description { get; protected set; }
        public bool Additive => true;
        public EndingFlavour SignalEndingFlavour => _predictingRecipe.SignalEndingFlavour;
        public bool Craftable => _predictingRecipe.Craftable;
        public bool HintOnly => _predictingRecipe.HintOnly;

        //public static RecipePrediction DefaultFromVerb(Verb verb)
        //{
        //    Recipe hintRecipe= Recipe.CreateSpontaneousHintRecipe(verb);
        //    return new RecipePrediction(hintRecipe, new AspectsDictionary());
        //}

        
        public RecipePrediction(Recipe predictingRecipe, AspectsDictionary aspectsAvailable,Verb withVerb)
        {
            if (!predictingRecipe.IsValid())
                predictingRecipe = Recipe.CreateSpontaneousHintRecipe(withVerb);

            _predictingRecipe = predictingRecipe;

            TextRefiner tr = new TextRefiner(aspectsAvailable);
            Title = tr.RefineString(predictingRecipe.Label);
            Description = tr.RefineString(predictingRecipe.StartDescription);
            Description = predictingRecipe.StartDescription;

            Description = tr.RefineString(Description);
        }

        public bool Equals(RecipePrediction other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(_predictingRecipe, other._predictingRecipe) && Title == other.Title && Description == other.Description;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RecipePrediction) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (_predictingRecipe != null ? _predictingRecipe.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Title != null ? Title.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Description != null ? Description.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(RecipePrediction left, RecipePrediction right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(RecipePrediction left, RecipePrediction right)
        {
            return !Equals(left, right);
        }



        public bool AddsMeaningfulInformation(RecipePrediction currentRecipePrediction)
        {

            if (currentRecipePrediction == null) //if there's no existing prediction, this has to be an improvement
                return true;

            if (currentRecipePrediction == this) //if this and we are the same, forget about it
                return false;

            //we often add a . to indicate that the description is intentionally empty.
            //if we do that, or if it's a mistaken empty string, just go back.


            if (!Situation.TextIntendedForDisplay(Description))
                return false;

            return true;
        }
   }
}
