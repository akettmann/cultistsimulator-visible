﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using OrbCreationExtensions;
using UnityEngine.Rendering;


public class RecipeCompendium
{
    private List<Recipe> _recipes;
    private IDice _dice;

    public RecipeCompendium(List<Recipe> allRecipes,IDice dice)
    {
        _recipes = allRecipes;
        _dice = dice;
    }

    public Recipe GetFirstRecipeForAspectsWithVerb(Dictionary<string, int> aspects,string verb)
    {
        //for each recipe,
        foreach (var recipe in _recipes.Where(r=>r.ActionId==verb))
        {
            //for each requirement in recipe, check if that aspect does *not* exist at that level in Aspects
            bool matches = true;
            foreach (string requirementId in recipe.Requirements.Keys)
            {
                if (!aspects.Any(a => a.Key == requirementId && a.Value >= recipe.Requirements[requirementId]))
                    matches = false;
            }
            //if none fail, return that recipe
            if (matches) return recipe;
            //if any fail, continue
        }

        return null;
    }

    public List<Recipe> GetAllRecipesAsList()
    {
        return _recipes;
    }

    public Recipe GetRecipeById(string recipeId)
    {
        return _recipes.SingleOrDefault(r => r.Id == recipeId);   
    }

    /// <summary>
    ///Determines whether the original recipe, an alternative, or something else should actually be run.
    /// Alternative recipes which match requirements on elements possessed and % chance are run in place of the original recipe.
    /// Alternatives which match, but which specify additional are run after the original recipe.
    /// There may be multiple additional alternatives.
    /// However, if an alternative ever does *not* specify additional, it replaces the entire list (although it may have alternatives of its own)
    /// Alternatives are recursive, and may have additionals of their own.
    /// A non-additional alternative always takes precedence over everything earlier; if a recursive alternative has additionals of its own, they'll replace everything earlier in the execution sequence.
    /// </summary>

    /// <returns> this may be the original recipe, or it may be an alternative recipe, it may be any number of recipes possible including the original</returns>
    public List<Recipe> GetActualRecipesToExecute(Recipe recipe, IElementsContainer elementsContainer)
    {
        List<Recipe> actualRecipesToExecute=new List<Recipe>() { recipe }; ;
        if (recipe.AlternativeRecipes.Count == 0)
            return actualRecipesToExecute;


        foreach (var ar in recipe.AlternativeRecipes)
        {
            int diceResult = _dice.Rolld100();
            if (diceResult<= ar.Chance)
            {
                Recipe candidateRecipe = GetRecipeById(ar.Id);
                if(candidateRecipeRequirementsAreSatisfied(candidateRecipe,elementsContainer))
                { 
                    if(ar.Additional)
                        actualRecipesToExecute.Add(candidateRecipe); //add the additional recipe, and keep going
                    else
                    { List<Recipe> recursiveRange=GetActualRecipesToExecute(candidateRecipe,elementsContainer);//check if this recipe has any substitutes in turn, and then

                        return recursiveRange;//this recipe, or its furtheralternatives, supersedes everything else! return it.
                    }
                }
            }
        }

        return actualRecipesToExecute; //we either found no matching candidates and are returning the original, or we added one or more additional recipes to the list
    }


    private bool candidateRecipeRequirementsAreSatisfied(Recipe candidateRecipe, IElementsContainer elementsContainer)
    {
        //must be satisfied by concrete elements in possession, not by aspects (tho this may some day change)
        foreach (var req in candidateRecipe.Requirements)
        {
            if (req.Value == -1) //req -1 means there must be none of the element
            {
                if (elementsContainer.GetCurrentElementQuantity(req.Key) > 0)
                    return false;
            }
            else if (!(elementsContainer.GetCurrentElementQuantity(req.Key) >= req.Value))
            { 
                //req >0 means there must be >=req of the element
                return false;
            }
        }
        return true;
    }
}
