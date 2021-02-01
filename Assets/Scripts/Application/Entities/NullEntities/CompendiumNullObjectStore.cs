﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SecretHistories.Entities;
using SecretHistories.Interfaces;
using SecretHistories.NullObjects;

namespace Assets.Scripts.Application.Entities.NullEntities
{
    public class CompendiumNullObjectStore
    {
        Dictionary<Type,object> NullObjectsForEntities=new Dictionary<Type, object>();

        public CompendiumNullObjectStore()
        {
            NullObjectsForEntities.Add(typeof(Element),NullElement.Create());

        }

        public object GetNullObjectForType(Type forType)
        {
            if (NullObjectsForEntities.ContainsKey(forType))
                return NullObjectsForEntities[forType];

            return null;
        }

    }
}
