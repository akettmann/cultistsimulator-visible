﻿using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Core.Commands;
using Assets.Core.Entities;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.CS.TabletopUI.Interfaces;
using Assets.TabletopUi.Scripts;
using Assets.TabletopUi.Scripts.Elements;
using Assets.TabletopUi.Scripts.Infrastructure;
using Assets.TabletopUi.Scripts.Interfaces;
using Assets.TabletopUi.Scripts.Services;
using Noon;

public class TabletopContainer : MonoBehaviour, ITokenContainer {
    private ElementStacksManager _stacksManager;


    public void TokenPickedUp(DraggableToken draggableToken) {
        draggableToken.lastTablePos = draggableToken.transform.position;
    }

    public void TokenDropped(DraggableToken draggableToken) {
    }

    public void TryMoveAsideFor(DraggableToken potentialUsurper, DraggableToken incumbent, out bool incumbentMoved)
    {
        //we're starting with the assumption that we don't want to attempt a merge if both tokens are elementstacks; that should be catered for elsewhere

        incumbent.transform.localPosition += transform.right * incumbent.RectTransform.rect.width * 1.3f;
        incumbentMoved = true;
        PutOnTable(potentialUsurper);
    }

    public IEnumerable<ISituationAnchor> GetAllSituationTokens() {
        return GetComponentsInChildren<ISituationAnchor>();
    }



    public void CloseAllSituationWindowsExcept(SituationToken except) {
        var situationTokens = GetTokenTransformWrapper().GetSituationTokens().Where(sw => sw != except);
        foreach (var situationToken in situationTokens) {
            if (DraggableToken.itemBeingDragged == null ||
                DraggableToken.itemBeingDragged.gameObject != situationToken.gameObject)

                situationToken.CloseSituation();
        }
    }

    public SituationToken GetOpenToken() {
        return GetTokenTransformWrapper().GetSituationTokens().FirstOrDefault(s => s.IsOpen);
    }

    public ISituationAnchor CreateSituation(SituationCreationCommand creationCommand, string locatorInfo = null) {
        return Registry.Retrieve<TabletopObjectBuilder>().CreateTokenWithAttachedControllerAndSituation(creationCommand, locatorInfo);
    }

    public void PutOnTable(DraggableToken token) {
        GetTokenTransformWrapper().Accept(token);

        token.RectTransform.anchoredPosition3D = new Vector3(token.RectTransform.anchoredPosition3D.x, token.RectTransform.anchoredPosition3D.y, 0f);
        token.RectTransform.localRotation = Quaternion.identity;
    }

    public bool AllowDrag { get { return true; } }
    public bool AllowStackMerge { get { return true; } }


    public ElementStacksManager GetElementStacksManager() {
        //In some places we've done it Initialise. Here, we're testing if it's null and then assigning on the fly
        //This is because I'm going through and refactoring. Perhaps it should be consistent YOU TELL ME it's likely to get refactored further anyhoo
        if (_stacksManager == null)
        {
            _stacksManager = new ElementStacksManager(GetTokenTransformWrapper());
        }
        return _stacksManager;
    }

    public ITokenTransformWrapper GetTokenTransformWrapper() {
        return new TabletopContainerTokenTransformWrapper(transform);
    }

    public string GetSaveLocationInfoForDraggable(DraggableToken draggable) {
        return (draggable.RectTransform.localPosition.x.ToString() + SaveConstants.SEPARATOR + draggable.RectTransform.localPosition.y).ToString();
    }
}
