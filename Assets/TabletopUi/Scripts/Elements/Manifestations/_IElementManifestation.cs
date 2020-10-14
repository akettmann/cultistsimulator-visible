﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Core.Entities;
using Assets.Core.Enums;
using UnityEngine;

namespace Assets.TabletopUi.Scripts.Elements.Manifestations
{
    public enum HighlightType
    {
        Hover,
        CanFitSlot,
        CanMerge,
        None
    }
    public interface IElementManifestation
    {
        void DisplayVisuals(Element element);
        void UpdateText(Element element, int quantity);
        void ResetAnimations();
        bool Retire(CanvasGroup canvasGroup);
        void SetVfx(CardVFX vfxName);
        void UpdateDecayVisuals(float lifetimeRemaining, Element element, float interval,bool currentlyBeingDragged);
        void BeginArtAnimation(string icon);
        bool CanAnimate();
        void OnBeginDragVisuals();
        void OnEndDragVisuals();
        void Highlight(HighlightType highlightType);
        bool NoPush { get; }
    }
}
