﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.CS.TabletopUI
{
    public class NotificationWindow: MonoBehaviour
    {
        [SerializeField]
        CanvasGroup canvasGroup;
        [SerializeField]
        CanvasGroupFader canvasGroupFader;
        [SerializeField]
        Image artwork;
        [SerializeField]
        TextMeshProUGUI _titleTxt;
        [SerializeField]
        TextMeshProUGUI _descriptionTxt;

        public void Awake()
        {
			canvasGroupFader.Show();
      		Invoke("Hide", 5);
        }

        public void SetDetails(string title, string description)
        {
            _titleTxt.text = title;
            _descriptionTxt.text = description;
        }

        public void Hide()
        {
            canvasGroupFader.Hide();
        }
    }
}
