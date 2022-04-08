﻿using System.Collections;
using SecretHistories.Assets.Scripts.Application.Abstract;
using SecretHistories.UI;
using UnityEngine;

namespace SecretHistories.Spheres
{
    public class WalkableFloor : AbstractPlacementGuide
    {
        
        public override bool TokenAllowedHere(Token token)
        {
            return true;
        }
    }
}