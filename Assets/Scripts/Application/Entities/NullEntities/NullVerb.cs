﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Application.Infrastructure.Events;
using Assets.Scripts.Application.Logic;
using SecretHistories.Abstract;
using SecretHistories.Commands;
using SecretHistories.Core;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Interfaces;
using SecretHistories.UI;
using SecretHistories.Elements.Manifestations;
using SecretHistories.Fucine;

namespace SecretHistories.NullObjects
{
    public class NullVerb:IVerb
    {
        public event Action<TokenPayloadChangedArgs> OnChanged;
        public event Action<float> OnLifetimeSpent;
        public string Id => string.Empty;
        public int Quantity =>0;
        public Dictionary<string, int> Mutations { get; }

        public Timeshadow GetTimeshadow()
        {
            return Timeshadow.CreateTimelessShadow();
        }


        public string Label { get; set; }
        public string Description { get; set; }
        
        public bool Spontaneous { get; }
        public string Icon => string.Empty;


        public Type GetManifestationType(SphereCategory forSphereCategory)
        {
            return typeof(NullManifestation);
        }

        public void InitialiseManifestation(IManifestation manifestation)
        {
           //
        }

        public bool IsValidElementStack()
        {
            return false;
        }

        public bool IsValidVerb()
        {
            return false;
        }

        public List<SphereSpec> Thresholds { get; set; }

        public bool ExclusiveOpen => false;

        protected NullVerb()
        {
            Thresholds=new List<SphereSpec>();
        }


        public static NullVerb Create()
        {
            return new NullVerb();
        }

        public AspectsDictionary GetAspects(bool includeSelf)
        {
            return new AspectsDictionary();
        }



    }
}
