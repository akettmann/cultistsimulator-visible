﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Logic;
using Assets.Scripts.Application.Infrastructure.Events;
using SecretHistories.Abstract;
using SecretHistories.Assets.Scripts.Application.Spheres;
using SecretHistories.Assets.Scripts.Application.UI;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Infrastructure;
using SecretHistories.Spheres;
using SecretHistories.Tokens.TokenPayloads;
using UnityEngine;

namespace SecretHistories.UI
{
    public class Numa: MonoBehaviour
    {

        [Space]
        [SerializeField] List<Otherworld> Otherworlds;



        public void Awake()
        {
            var w=new Watchman();
            w.Register(this);

            foreach(var o in Otherworlds)
                o.Prepare();
        }

        public void Open(RectTransform atRectTransform,Ingress ingress)
        {
            

            var otherworldToOpen = Otherworlds.SingleOrDefault(o => o.EntityId == ingress.GetOtherworldId());
            if(otherworldToOpen==null)
                NoonUtility.LogWarning("Can't find otherworld with id " + ingress.GetOtherworldId());
            else
                otherworldToOpen.Show(atRectTransform,ingress);

        }



    }
}
