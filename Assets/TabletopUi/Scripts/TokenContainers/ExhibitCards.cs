﻿#pragma warning disable 0649
using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Core.Entities;
using Assets.Core.Enums;
using Assets.Core.Fucine;
using Assets.Core.Interfaces;
using Assets.TabletopUi;
using Assets.CS.TabletopUI;
using Assets.CS.TabletopUI.Interfaces;
using Assets.TabletopUi.Scripts;
using Assets.TabletopUi.Scripts.Services;
using Assets.TabletopUi.Scripts.Infrastructure;
using Noon;
using TMPro;

public class ExhibitCards : Sphere {

    public override bool AllowDrag { get { return false; } }
    public override bool AllowStackMerge { get { return false; } }
    public override bool AlwaysShowHoverGlow { get { return true; } }



    public override SpherePath GetPath()
    {
        return new SpherePath("");
    }

    public override ContainerCategory ContainerCategory => ContainerCategory.Meta;

    public override ElementStackToken ProvisionElementStack(string elementId, int quantity, Source stackSource, Context context)
    {
        var token = Registry.Get<PrefabFactory>().CreateLocally<ElementStackToken>(transform);

        token.Populate(elementId,quantity,stackSource);

         AcceptStack(token, context);

         return token;
    }

    public override void DisplayHere(ElementStackToken stack, Context context)
    {
        base.DisplayHere(stack, context);
        stack.Understate();
    }

    public void HighlightCardWithId(string elementId)
    {
        var cards = GetStacks();

        foreach (var card in cards.Select(c=>c as ElementStackToken))
        {
            if (card.EntityId == elementId)
                card.Emphasise();
            else
                card.Understate();
        }

    }
}
