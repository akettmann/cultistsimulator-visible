﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Core.Fucine;
using Assets.CS.TabletopUI;
using Assets.TabletopUi.Scripts.Infrastructure;
using UnityEngine;

namespace Assets.TabletopUi
{
  public  class TokenLocation
  {
      public Vector3 Position;
      public SpherePath AtSpherePath;
        public TokenLocation(float x,float y,float z, SpherePath atSpherePath)
        {
            Position.x = x;
            Position.y = y;
            Position.z = z;
            AtSpherePath = atSpherePath;

        }

        public TokenLocation(Vector2 position, SpherePath atSpherePath)
        {
            Position = position;
            AtSpherePath = atSpherePath;
        }

        public TokenLocation(Vector3 position,SpherePath atSpherePath)
        {
            Position = position;
            AtSpherePath = atSpherePath;
        }

        public TokenLocation(Token token)
        {
            Position = token.Location.Position;
            AtSpherePath = token.Sphere.GetPath();
        }


    }
}
