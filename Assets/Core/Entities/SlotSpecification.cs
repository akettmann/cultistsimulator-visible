﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Assets.Core;
using Assets.Core.Entities;
using Assets.Core.Fucine;
using Assets.Core.Interfaces;
using Noon;

/// <summary>
/// Entity class: a child slot for an element
/// </summary>
public class SlotSpecification:IEntityWithId
{
    private string _id;
    private readonly Hashtable _unknownProperties = CollectionsUtil.CreateCaseInsensitiveHashtable();

    [FucineId]
    public string Id
    {
        get => _id;
    }

    public void SetId(string id)
    {
        _id = id;
    }

    public void RefineWithCompendium(ContentImportLogger logger, ICompendium populatedCompendium)
    {
        Hashtable unknownProperties = PopAllUnknownProperties();
        if (unknownProperties.Keys.Count > 0)
        {
            foreach (var k in unknownProperties.Keys)
                logger.LogInfo($"Unknown property in import: {k} for {GetType().Name} with ID {Id}");
        }
    }

    public void PushUnknownProperty(object key, object value)
    {
        _unknownProperties.Add(key, value);
    }

    public Hashtable PopAllUnknownProperties()
    {
        Hashtable propertiesPopped = CollectionsUtil.CreateCaseInsensitiveHashtable(_unknownProperties);
        _unknownProperties.Clear();
        return propertiesPopped;
    }

    [FucineValue("")]
    public string Label { get; set; }
    [FucineValue("")]
    public string ActionId { get; set; }

    /// <summary>
    /// currently, this is only used by the primary slot specification
    /// </summary>
    [FucineValue("")]
    public string Description { get; set; }
    /// <summary>
    /// The element in this slot must possess at least one of these aspects
    /// </summary>
    [FucineAspects]
    public AspectsDictionary Required { get; set; }
    /// <summary>
    /// The element in this slot cannot possess any of these aspects
    /// </summary>
    [FucineAspects]
    public AspectsDictionary Forbidden { get; set; }

    /// <summary>
    /// A Greedy slot will find a card on the desktop that matches its specification, and insert it.
    /// </summary>
    [FucineValue(false)]
    public bool Greedy { get; set; }
    /// <summary>
    /// A Consuming slot will destroy its contents when a recipe begins
    /// </summary>
    [FucineValue(false)]
    public bool Consumes { get; set; }

    /// <summary>
    /// An slot with NoAnim set to true won't display the VFX/SFX when it appears as an ongoing slot. So! it has no effect on startingslots
    /// </summary>
    [FucineValue(false)]
    public bool NoAnim { get; set; }

private const string PRIMARY_SLOT="primary";

    public SlotSpecification(string id)
    {
        _id = id;
        Label = id;
        Required = new AspectsDictionary();
        Forbidden = new AspectsDictionary();
        ActionId = string.Empty;
    }

    public SlotSpecification()
    {
        Required = new AspectsDictionary();
        Forbidden = new AspectsDictionary();
    }

    public static SlotSpecification CreatePrimarySlotSpecification()
    {
        var spec=new SlotSpecification(PRIMARY_SLOT);
        spec.Label = "";
        spec.Description = LanguageTable.Get("UI_EMPTYSPACE");
        return spec;
    }

    public SlotMatchForAspects GetSlotMatchForAspects(IAspectsDictionary aspects)
    {

        foreach (string k in Forbidden.Keys)
        {
            if(aspects.ContainsKey(k))
            {
                return new SlotMatchForAspects(new List<string>() {k}, SlotMatchForAspectsType.ForbiddenAspectPresent);
            }
        }
        
        //passed the forbidden check
        //if there are no specific requirements, then we're now okay
        if(Required.Keys.Count==0)
            return new SlotMatchForAspects(null,SlotMatchForAspectsType.Okay);


        foreach (string k in Required.Keys) //only one needs to match
        {
            if (aspects.ContainsKey(k))
            { 
                int aspectAtValue = aspects[k];
                if (aspectAtValue >= Required[k])
                    return new SlotMatchForAspects(null, SlotMatchForAspectsType.Okay);
            }
        }


        return new SlotMatchForAspects(Required.Keys, SlotMatchForAspectsType.RequiredAspectMissing);


    }
}





public enum SlotMatchForAspectsType
{
Okay,
    RequiredAspectMissing,
    ForbiddenAspectPresent
}