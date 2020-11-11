﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Core.Enums;
using Assets.Core.Fucine;
using Assets.TabletopUi.Scripts.Infrastructure;

namespace Assets.TabletopUi.Scripts.TokenContainers
{
   public class FurnitureContainer: Sphere
   {
       public override ContainerCategory ContainerCategory => ContainerCategory.World;
        public override SpherePath GetPath()
        {
            return new SpherePath("furniture_temp");
        }
    }
}
