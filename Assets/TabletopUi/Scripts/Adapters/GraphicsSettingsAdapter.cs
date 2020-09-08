﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Assets.Core.Entities;
using Assets.CS.TabletopUI;
using Assets.TabletopUi.Scripts.Interfaces;
using Noon;
using UnityEngine;

namespace Assets.TabletopUi.Scripts.Services
{
    public class GraphicsSettingsAdapter: MonoBehaviour, ISettingSubscriber
    {
        private void Start()
        {
            var graphicsLevelSetting = Registry.Get<ICompendium>().GetEntityById<Setting>(NoonConstants.GRAPHICSLEVEL);
            if (graphicsLevelSetting != null)
            {
                graphicsLevelSetting.AddSubscriber(this);
                UpdateValueFromSetting(graphicsLevelSetting.CurrentValue);
            }
            else
                NoonUtility.Log("Missing setting entity: " + NoonConstants.GRAPHICSLEVEL);

        }
        public void UpdateValueFromSetting(object newValue)
        {
            
            if (Application.platform == RuntimePlatform.OSXPlayer)
                // Vsync doesn't seem to limit the FPS on the mac so well, so we set it to 0 and force a target framerate (setting it to 0 any other way doesn't work, has to be done in code, apparently in Start not Awake too) - FM
                QualitySettings.vSyncCount = 0;
            else
                QualitySettings.vSyncCount = 1; // Force VSync on in case user has tried to disable it. No benefit, just burns CPU - CP

            if(newValue is float)
                SetGraphicsLevel((float)newValue);
            else
                SetGraphicsLevel(1);

        }


        protected void SetGraphicsLevel(float level)
        {
            int levelAsInt = (int) level;
            QualitySettings.SetQualityLevel(levelAsInt);
            SetFrameRateForCurrentGraphicsLevel(levelAsInt);

        }

        protected void SetFrameRateForCurrentGraphicsLevel(int level)
        {
            
            if (level > 1)
                Application.targetFrameRate = 60;
            else
                Application.targetFrameRate = 30; //ram down the frame rate for v low quality
        }




    }
}
