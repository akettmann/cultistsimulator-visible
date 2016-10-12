﻿using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;

public class SlotReceiveVerb : BoardMonoBehaviour, IDropHandler {


    public DraggableVerbToken VerbTokenInSlot
    {
        get
        {
            if (transform.childCount > 0)
            {
                Transform child = transform.GetChild(0);
                return child.gameObject.GetComponent<DraggableVerbToken>();
            }
               
                return null;

        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (BM.itemBeingDragged.tag=="Verb")
       {
           if (VerbTokenInSlot && VerbTokenInSlot.GetComponent<DraggableToken>())
           {
               DraggableToken itemInSlotComponent = VerbTokenInSlot.GetComponent<DraggableToken>();
                VerbTokenInSlot.transform.SetParent(itemInSlotComponent.originSlot);
            }

            BM.itemBeingDragged.transform.SetParent(transform);
             BM.MakeFirstSlotAvailable(transform.localPosition);
            BM.UpdateAspectDisplay();
        }
    }

    public string GetCurrentVerbId()
    {
        return VerbTokenInSlot.VerbId;
    }
}
