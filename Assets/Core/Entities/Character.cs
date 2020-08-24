﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Core.Entities;
using Assets.Core.Interfaces;
using Assets.Core.Services;
using Assets.CS.TabletopUI;
using JetBrains.Annotations;
using Noon;

public enum LegacyEventRecordId
{
    lastcharactername,
    lastdesire,
    lasttool,
    lastbook,
    lastsignificantpainting,
    lastcult,
    lastheadquarters,
    lastfollower,
    lastpersonkilled

}

public class Character
{
    private string _name="[unnamed]";

    public CharacterState State
    {
        get
        {
            if (EndingTriggered != null)
                return CharacterState.Extinct;
            if (ActiveLegacy != null)
                return CharacterState.Viable;

            return CharacterState.Unformed;
        }


    }
    public List<IDeckInstance> DeckInstances { get; set; } = new List<IDeckInstance>();
    private Dictionary<string, string> _futureLegacyEventRecords;
    private Dictionary<string, string> _pastLegacyEventRecords = new Dictionary<string, string>();
    public Legacy ActiveLegacy { get; set; }
    public Character PreviousCharacter { get; set; }

    private Dictionary<string, int> recipeExecutions = new Dictionary<string, int>();

    public Character():this(null,null)
    {
        
    }

    public Character(Legacy activeLegacy) : this(activeLegacy,null)
    {

    }

    public Character(Legacy activeLegacy, Character previousCharacter)
    {
        Reset(activeLegacy);

        //if we have a previous character, base our past on their future
        if (previousCharacter != null)
        {
            _pastLegacyEventRecords = previousCharacter.GetAllFutureLegacyEventRecords(); //THEIR FUTURE IS OUR PAST
        }
    }

    public void Reset(Legacy activeLegacy)
    {
       
            ActiveLegacy = activeLegacy;
       
        //otherwise, create a blank slate
        //the history builder will then provide a default value for any empty ones.
        HistoryBuilder hb = new HistoryBuilder();
        _pastLegacyEventRecords = hb.FillInDefaultPast(_pastLegacyEventRecords);

        //finally, set our starting future to be our present, ie our past.
        _futureLegacyEventRecords = new Dictionary<string, string>(_pastLegacyEventRecords);
    }

    // Turns this character into a defunct character based on the past of the current, active character
    public static Character MakeDefunctCharacter(Character currentCharacter)
    {
        return new Character(null)
        {
            recipeExecutions = new Dictionary<string, int>(),
            DeckInstances = new List<IDeckInstance>(),
            _futureLegacyEventRecords = currentCharacter.GetAllPastLegacyEventRecords(),

            // Turn all past records back into future records, to simulate a character whose run ended
            _pastLegacyEventRecords = new HistoryBuilder().FillInDefaultPast(new Dictionary<string, string>()),
            
            // Load in a default legacy, since it doesn't matter for the defunct character
            ActiveLegacy = Registry.Get<ICompendium>().GetEntitiesAsList<Legacy>().First()
        };
    }

    public void ClearExecutions()
    {
        recipeExecutions.Clear();
    }

    public Dictionary<string, int> GetAllExecutions()
    {
        return new Dictionary<string, int>(recipeExecutions);
    }

    public void AddExecutionsToHistory(string forRecipeId,int executions)
    {
        if (recipeExecutions.ContainsKey(forRecipeId))
            recipeExecutions[forRecipeId]+=executions;
        else
            recipeExecutions[forRecipeId] = executions;
    }

    public int GetExecutionsCount(string forRecipeId)
    {
        if (recipeExecutions.ContainsKey(forRecipeId))
            return recipeExecutions[forRecipeId];

        return 0;
    }

    public bool HasExhaustedRecipe(Recipe forRecipe)
    {
        if (forRecipe.UnlimitedExecutionsPermitted())
            return false;

        return forRecipe.MaxExecutions <= GetExecutionsCount(forRecipe.Id);
    }

    public void SetOrOverwritePastLegacyEventRecord(string id, string value)
    {
if(string.IsNullOrEmpty(value))
    throw new ApplicationException("Error in LegacyEventRecord overwrite: shouldn't overwrite with an empty value, trying to erase the past for " + id.ToString());
        if (_pastLegacyEventRecords.ContainsKey(id))
            _pastLegacyEventRecords[id] = value;
        else
            _pastLegacyEventRecords.Add(id, value);
    }


    public void SetFutureLegacyEventRecord(string id, string value)
    {
        if (_futureLegacyEventRecords.ContainsKey(id))
            _futureLegacyEventRecords[id] = value;
        else
            _futureLegacyEventRecords.Add(id, value);
    }
    public string GetFutureLegacyEventRecord(string forId)
    {
        if (_futureLegacyEventRecords.ContainsKey(forId))
            return _futureLegacyEventRecords[forId];
        else
            return null;
    }


    public string GetPastLegacyEventRecord(string forId)
    {
        if (_pastLegacyEventRecords.ContainsKey(forId))
            return _pastLegacyEventRecords[forId];
        else
            return null;
    }

    public IDeckInstance GetDeckInstanceById(string id)
    {
        try
        {

            return  DeckInstances.SingleOrDefault(d => d.Id == id);
        }
        catch (Exception e)
        {
            NoonUtility.Log(e.Message + " for deck instance id " + id,2);
            throw;
        }
    }

    public string Name
    {
        get { return _name; }
        set { _name = value; }
    }

    public string Profession { get ; set; }


    public Ending EndingTriggered { get; set; }




    public Dictionary<string, string> GetAllFutureLegacyEventRecords()
    {
        return new Dictionary<string, string>(_futureLegacyEventRecords);
    }

    public Dictionary<string, string> GetAllPastLegacyEventRecords()
    {
        return new Dictionary<string, string>(_pastLegacyEventRecords);
    }


}

