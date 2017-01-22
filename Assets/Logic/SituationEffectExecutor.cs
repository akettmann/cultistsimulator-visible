﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Core.Interfaces;

namespace Assets.Logic
{
   public class SituationEffectExecutor
    {
        public void RunEffects(ISituationEffectCommand command, IElementStacksManager stacksManager)
        {
            var aspectsPresent = stacksManager.GetTotalAspects();

            foreach (var stack in stacksManager.GetStacks())
            {
                var xTriggers = stack.GetXTriggers();
               foreach (var k in xTriggers.Keys)
                   if (aspectsPresent.ContainsKey(k))
                   {
                       var existingQuantity = stack.Quantity;

                       stacksManager.ModifyElementQuantity(stack.Id, -existingQuantity);
                      stacksManager.ModifyElementQuantity(xTriggers[k],existingQuantity);
                   }
            }
            foreach (var kvp in command.GetElementChanges())
            {
                stacksManager.ModifyElementQuantity(kvp.Key, kvp.Value);
            }
        }
    }
}
