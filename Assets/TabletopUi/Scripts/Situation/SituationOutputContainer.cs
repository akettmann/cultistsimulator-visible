﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.CS.TabletopUI.Interfaces;
using Assets.TabletopUi.Scripts;
using Assets.TabletopUi.Scripts.Services;

public class SituationOutputContainer : MonoBehaviour
{
    [SerializeField] private Transform outputParent;
    [SerializeField] private SituationWindow situationWindow;

    public void AddOutput(IEnumerable<IElementStack> stacks,INotification notification) {
        gameObject.SetActive(true);
        var newNote=PrefabFactory.CreateLocally<SituationOutputNote>(outputParent);
        newNote.transform.localPosition = Vector2.zero;
        newNote.Initialise(notification, stacks, this);
    }

    //all the outputs created have been consumed; tell upstream it can get back to what it was doing
    public void AllOutputsGone()
    {
        situationWindow.AllOutputsGone();
        gameObject.SetActive(false);
    }

    public IEnumerable<ISituationOutput> GetCurrentOutputs()
    {
        return outputParent.GetComponentsInChildren<SituationOutputNote>();
    }
 
}
