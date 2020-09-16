﻿using System.Collections.Generic;
using Noon;
using UnityEngine;

namespace Assets.TabletopUi.Scripts.Services
{
    //max lines
    //enforce max lines
    //autodisplay when warning
    //control to hide
    //we probably want multiple text lines
    //and there's likely an asset store thing
    public class SecretHistory:MonoBehaviour,ILogSubscriber
    {
        [SerializeField] private GameObject LogMessageEntryPrefab;
        [SerializeField] private Transform logMessageEntriesHere;
        [SerializeField] private CanvasGroup canvasGroup;

        public bool IsVisible => canvasGroup.alpha > 0f;

        public void SetVisible(bool visible)
        {
            if (visible)
                canvasGroup.alpha = 1f;
            else
                canvasGroup.alpha = 0f;
        }

        
        public void AddMessage(NoonLogMessage message)
        {

            SecretHistoryLogMessageEntry entry = Instantiate(LogMessageEntryPrefab, logMessageEntriesHere).GetComponent<SecretHistoryLogMessageEntry>();
            entry.TextComponent.text = message.Description;

        }

    }
}
