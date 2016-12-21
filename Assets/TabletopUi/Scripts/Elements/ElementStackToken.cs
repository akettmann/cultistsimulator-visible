﻿using System.Collections.Generic;
using Assets.Core;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI.Interfaces;
using Assets.TabletopUi.Scripts;
using Assets.TabletopUi.Scripts.Services;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Should inherit from a "TabletopToken" base class same as VerbBox

namespace Assets.CS.TabletopUI
{
    public class ElementStackToken : DraggableToken, IElementStack
    {

        [SerializeField] Image artwork;
        [SerializeField] TextMeshProUGUI text;
        [SerializeField] GameObject selectedMarker;
		[SerializeField] GameObject stackBadge;
		[SerializeField] TextMeshProUGUI stackCountText;

        private Element _element;
        private int _quantity;
        private ITokenTransformWrapper currentWrapper;

        public override string Id
        {
            get { return _element == null ? null : _element.Id; }
        }



        public string Label
        {
            get { return _element == null ? null : _element.Label; }
        }

        public int Quantity
        {
            get { return _quantity; }
        }

        public bool Defunct { get; private set; }


        public void SetQuantity(int quantity)
        {
            _quantity = quantity;
            if (quantity <= 0)
            {
                Retire();
                return;
            }
            DisplayInfo();
        }


        public void ModifyQuantity(int change)
        {
            SetQuantity(_quantity + change);
        }

        public override bool Retire()
        {
            DestroyObject(gameObject);
            if (Defunct)
                return false;
            Defunct = true;
            return true;
        }

        public void Populate(string elementId, int quantity)
        {

            _element = Registry.Retrieve<ICompendium>().GetElementById(elementId);
            SetQuantity(quantity);

            name = "Card_" + elementId;
            if (_element == null)
                return;

            DisplayInfo();
            DisplayIcon();
        }


        private void DisplayInfo()
		{
			text.text = _element.Label;
			stackBadge.gameObject.SetActive(Quantity > 1);
			stackCountText.text = Quantity.ToString();
        }

        private void DisplayIcon()
        {
            Sprite sprite = ResourcesManager.GetSpriteForElement(_element.Id);
            artwork.sprite = sprite;

            if (sprite == null)
                artwork.color = Color.clear;
            else
                artwork.color = Color.white;
        }

        public IAspectsDictionary GetAspects()
        {
            return _element.AspectsIncludingSelf;
        }

        public List<SlotSpecification> GetChildSlotSpecifications()
        {
            return _element.ChildSlotSpecifications;
        }


        public bool HasChildSlots()
        {
            return _element.HasChildSlots();
        }

        public Sprite GetSprite()
        {
            return artwork.sprite;
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            notifier.ShowElementDetails(_element);
        }

        public override void OnDrop(PointerEventData eventData)
        {
            if (DraggableToken.itemBeingDragged != null)
                DraggableToken.itemBeingDragged.InteractWithTokenDroppedOn(this);
        }


        public override void InteractWithTokenDroppedOn(IElementStack stackDroppedOn)
        {
            if (stackDroppedOn.Id == this.Id && stackDroppedOn.AllowMerge())
            {
                stackDroppedOn.SetQuantity(stackDroppedOn.Quantity + this.Quantity);
                DraggableToken.resetToStartPos = false;
                this.SetQuantity(0);
            }
        }

        public void SplitAllButNCardsToNewStack(int n)
        {
            if (Quantity > n)
            {
                var cardLeftBehind = PrefabFactory.CreateToken<ElementStackToken>(transform.parent);

                cardLeftBehind.Populate(Id, Quantity - n);
                //goes weird when we pick things up from a slot. Do we need to refactor to Accept/Gateway in order to fix?
                SetQuantity(1);
                cardLeftBehind.transform.position = transform.position;
                var gateway = container.GetElementStacksManager();

               gateway.AcceptStack(cardLeftBehind);
            }
   
        }

        public bool AllowMerge()
        {
            return container.AllowStackMerge;
        }

        protected override void StartDrag(PointerEventData eventData)
        {
			// A bit hacky, but it works: DID NOT start dragging from badge? Split cards 
			if (eventData.hovered.Contains(stackBadge) == false) 
            	SplitAllButNCardsToNewStack(1);

            base.StartDrag(eventData);
        }
    }
}
