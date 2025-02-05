﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SecretHistories.Entities;
using SecretHistories.UI;
using SecretHistories.Constants.Events;
using SecretHistories.Events;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SecretHistories.Fucine
{
    public interface ISphereEventSubscriber
    {
        void OnSphereChanged(SphereChangedArgs args);
        void OnTokensChangedForSphere(SphereContentsChangedEventArgs args);
        void OnTokenInteractionInSphere(TokenInteractionEventArgs args);
        


    }
}
