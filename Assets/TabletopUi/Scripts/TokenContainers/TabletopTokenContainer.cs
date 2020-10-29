﻿using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Core.Commands;
using Assets.Core.Entities;
using Assets.Core.Enums;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.CS.TabletopUI.Interfaces;
using Assets.TabletopUi.Scripts;
using Assets.TabletopUi.Scripts.Infrastructure;
using Assets.TabletopUi.Scripts.Interfaces;
using Assets.TabletopUi.Scripts.Services;
using Assets.TabletopUi.Scripts.TokenContainers;
using Noon;
using UnityEngine.EventSystems;

public class TabletopTokenContainer : TokenContainer,IBeginDragHandler,IEndDragHandler {
#pragma warning disable 649
    [SerializeField] TabletopBackground _background;
    [SerializeField] protected CanvasGroupFader canvasGroupFader;
#pragma warning disable 649

    public override ContainerCategory ContainerCategory => ContainerCategory.World;

    public EnRouteTokenContainer SendViaContainer;

    public override bool AllowDrag { get { return true; } }
    public override bool AllowStackMerge { get { return true; } }

    // This is used to determine if this component is the tabletop.
    // Needed because the MapTokenContainer inherits from TabletopTokenContainer but is not the Tabletop
    public virtual bool IsTabletop { get { return true; } }

    public override void Start() {
        EnforceUniqueStacksInThisContainer = true; // Martin: This ensures that this stackManager kills other copies when a unique is dropped in

        InitialiseListeners();
        _notifiersForContainer.Add(Registry.Get<INotifier>());
        base.Start();
    }

    private void InitialiseListeners() {
        // Init Listeners to pre-existing DisplayHere Objects
        _background.onDropped += HandleOnTableDropped;
        _background.onClicked += HandleOnTableClicked;
    }

    public override void OnDestroy() {
        base.OnDestroy();

        // Static event so make sure to de-init once this object is destroyed
        }



    public override void DisplayHere(IToken token, Context context) {
        // We're not setting the location; this is used to display a token dragged and dropped to an arbitrary position
        // (or loaded and added to an arbitrary position)
        token.transform.SetParent(transform, true);
        token.TryReturnToOriginalPosition();
        token.transform.localRotation = Quaternion.identity;
		token.SnapToGrid();
        token.SetTokenContainer(this, context);
        token.DisplayAtTableLevel(); // This puts it on the table, so now the choreographer will pick it up
        CheckOverlappingTokens(token);


    }

    // Tabletop specific
    public void CheckOverlappingTokens(IToken token) {
        // Verify if we are overlapping with anything. If so: move it.
        Registry.Get<Choreographer>().MoveAllTokensOverlappingWith(token);
    }

    public override void TryMoveAsideFor(VerbAnchor potentialUsurper, AbstractToken incumbent, out bool incumbentMoved) {
        //incumbent.RectTransform.anchoredPosition = GetFreeTokenPos(incumbent);
        incumbentMoved = true;
        DisplayHere(potentialUsurper, new Context(Context.ActionSource.PlayerDrag));
    }

    public override void TryMoveAsideFor(ElementStackToken potentialUsurper, AbstractToken incumbent, out bool incumbentMoved) {
        // We don't merge here. We assume if we end up here no merge was possible
        //incumbent.RectTransform.anchoredPosition = GetFreeTokenPos(incumbent);
        incumbentMoved = true;
        AcceptStack(potentialUsurper, new Context(Context.ActionSource.PlayerDrag));
        CheckOverlappingTokens(potentialUsurper);
    }

    Vector2 GetFreeTokenPos(AbstractToken incumbent) {
        var choreo = Registry.Get<Choreographer>();
        var currentPos = incumbent.RectTransform.anchoredPosition;

        return choreo.GetFreePosWithDebug(incumbent, currentPos);
    }


    public override string GetPath()
    {
        return "tabletop";

    }



  
    void HandleOnTableDropped(PointerEventData eventData)
    {
        var potentialVerbToken = eventData.pointerDrag.GetComponent<VerbAnchor>();

        var potentialElementStack = eventData.pointerDrag.GetComponent<ElementStackToken>();


        if (potentialVerbToken != null)
        {
            potentialVerbToken.SetXNess(TokenXNess.DroppedOnTableContainer);
            DisplayHere(potentialVerbToken,
                new Context(Context.ActionSource.PlayerDrag));
            CheckOverlappingTokens(potentialVerbToken);
            SoundManager.PlaySfx("CardDrop");

        }
        else if (potentialElementStack!=null) {
            potentialElementStack.SetXNess(TokenXNess.DroppedOnTableContainer);

            AcceptStack(potentialElementStack,
                    new Context(Context.ActionSource.PlayerDrag));
            CheckOverlappingTokens(potentialElementStack);
            SoundManager.PlaySfx("CardDrop");
        }

        else 
            NoonUtility.Log("Tried to put something weird on the table: " + eventData.pointerDrag.name,1);
        

        

        
    }

    void HandleOnTableClicked(PointerEventData eventData) {
        //Close all open windows if we're not dragging (multi tap stuff)
        // Situation windows get closed first, then details windows.

            var tabletopManager = Registry.Get<TabletopManager>();
            if (tabletopManager.IsSituationWindowOpen())
                tabletopManager.CloseAllSituationWindowsExcept(null);
            else
                tabletopManager.CloseAllDetailsWindows();
        
    }

    public void OnBeginDrag(PointerEventData eventData) {
 //can we make the table draggable rather than do the rect scroll thing?
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        //can we make the table draggable rather than do the rect scroll thing?
    }




    // Returns a rect for use by the Choreographer
    public Rect GetRect() {
        var rectTrans = transform as RectTransform;
        return rectTrans.rect;
    }

    // Returns all visual tokens for use by the Choreographer
    public virtual IEnumerable<AbstractToken> GetTokens() {
        return transform.GetComponentsInChildren<AbstractToken>();
    }

    // Hide / Show for use with Mansus Map transition
    public virtual void Show(bool show) {
        if (show)
            canvasGroupFader.Show();
        else
            canvasGroupFader.Hide();
    }

}
