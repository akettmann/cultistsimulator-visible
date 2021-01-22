﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Interfaces;
using SecretHistories.UI;
using SecretHistories.Elements;
using SecretHistories.Elements.Manifestations;
using SecretHistories.Constants.Events;
using SecretHistories.Spheres;
using UnityEngine;

namespace SecretHistories.NullObjects
{
   public class NullToken: IElementStackHost
   {
       
       public void Populate(ElementStack elementStack)
       {
       //
       }

       public bool Defunct { get; private set; }
       public Sphere Sphere { get; }

       public bool Retire(RetirementVFX rvfx)
       {
           Defunct = true;
           return true;
       }

        public void Remanifest(RetirementVFX vfx)
        {
        //
        }

        public void onElementStackQuantityChanged(ElementStack stack,Context context)
        {
           //
        }

        

    }
}
