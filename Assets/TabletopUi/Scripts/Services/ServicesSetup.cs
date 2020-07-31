﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.CS.TabletopUI;
using Assets.TabletopUi.Scripts.Infrastructure;
using UnityEngine;

namespace Assets.TabletopUi.Scripts.Services
{
    public class ServicesSetup: MonoBehaviour
    {
        public void Awake()
        {
            var registryAccess=new Registry();

            var storeClientProvider = new StorefrontServicesProvider();
            storeClientProvider.InitialiseForStorefrontClientType(StoreClient.Steam);
            storeClientProvider.InitialiseForStorefrontClientType(StoreClient.Gog);
            registryAccess.Register<StorefrontServicesProvider>(storeClientProvider);
        }

    }
}
