﻿using UnityEngine;
using System.Collections;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.CS.TabletopUI.Interfaces;
using Assets.TabletopUi.Scripts;
using System;
using Noon;

public class SituationStorage : MonoBehaviour, ITokenContainer
{
    private ElementStacksManager _stacksManager;
    public void TryMoveAsideFor(SituationToken potentialUsurper, DraggableToken incumbent, out bool incumbentMoved)
    {
        //do nothing, ever
        incumbentMoved = false;
    }

    public void TryMoveAsideFor(ElementStackToken potentialUsurper, DraggableToken incumbent, out bool incumbentMoved)
    {
        //do nothing, ever
        incumbentMoved = false;
    }


    public void Initialise()
    {
        ITokenTransformWrapper stacksWrapper = new TokenTransformWrapper(transform);
        _stacksManager = new ElementStacksManager(stacksWrapper,"storage");
    }

    public bool AllowDrag { get { return false; } }
    public bool AllowStackMerge { get { return false; } }

    public void ElementStackRemovedFromContainer(ElementStackToken elementStackToken)
    {

    }

    public void TokenDropped(DraggableToken draggableToken) { }

    public ElementStacksManager GetElementStacksManager()
    {
        return _stacksManager;
    }

    public string GetSaveLocationInfoForDraggable(DraggableToken draggable) {
        return "slot_storage";
    }
}
