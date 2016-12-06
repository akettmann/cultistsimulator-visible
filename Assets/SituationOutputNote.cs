﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.CS.TabletopUI.Interfaces;
using Assets.TabletopUi;
using Assets.TabletopUi.Scripts;
using Assets.TabletopUi.Scripts.Infrastructure;
using Noon;
using TMPro;

public interface ISituationOutput
{
    string TitleText { get; }
    string DescriptionText { get; }
    ITokenTransformWrapper GetTokenTransformWrapper();
}

public class SituationOutputNote : MonoBehaviour, ITokenContainer,ISituationOutput
{
    [SerializeField]private TextMeshProUGUI Title;
    [SerializeField]private TextMeshProUGUI Description;
    private SituationOutputContainer parentSituationOutputContainer;

    public string TitleText { get { return Title.text; } }
    public string DescriptionText { get { return Title.text; } }

    public void Initialise(INotification notification, IEnumerable<IElementStack> stacks,SituationOutputContainer soc)
    {
        Title.text = notification.Title;
        Description.text = notification.Description;
        GetElementStacksManager().AcceptStacks(stacks);
        parentSituationOutputContainer = soc;
    }

    public void TokenPickedUp(DraggableToken draggableToken)
    {

        var stacks = GetElementStacksManager().GetStacks();
        //if no stacks left in output
        if (!stacks.Any())
        {
            parentSituationOutputContainer.AllOutputsGone();
            DestroyObject(this.gameObject);
        }
    }


    public bool AllowDrag { get { return true; } }
    public ElementStacksManager GetElementStacksManager()
    {
        ITokenTransformWrapper tabletopStacksWrapper = new TokenTransformWrapper(transform);
        return new ElementStacksManager(tabletopStacksWrapper);
    }

    public ITokenTransformWrapper GetTokenTransformWrapper()
    {
       return new TokenTransformWrapper(transform);
    }

    public string GetSaveLocationInfoForDraggable(DraggableToken draggable)
    {
        return (draggable.RectTransform.localPosition.x.ToString() + SaveConstants.SEPARATOR + draggable.RectTransform.localPosition.y).ToString();
    }
}


