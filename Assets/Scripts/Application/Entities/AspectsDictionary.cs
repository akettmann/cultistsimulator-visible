﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using SecretHistories.Abstract;
using SecretHistories.UI;
using SecretHistories.Fucine;

namespace SecretHistories.Core
{

    public class AspectsDictionary: Dictionary<string, int>
    {
        public AspectsDictionary():this(new Dictionary<string, int>())
        { }

        public static AspectsDictionary Empty()
        {
            return new AspectsDictionary();
        }

        public static AspectsDictionary GetFromStacks(IEnumerable<ITokenPayload> stacks,bool includingSelf=true)
        {
            AspectsDictionary totals = new AspectsDictionary();

            foreach (var elementCard in stacks)
            {
                var aspects = elementCard.GetAspects(includingSelf);

                foreach (string k in aspects.Keys)
                {
                    if (totals.ContainsKey(k))
                        totals[k] += aspects[k];
                    else
                        totals.Add(k, aspects[k]);
                }
            }

            return totals;

        }


        public AspectsDictionary(Dictionary<string, int> aspectsAsDictionary)
        {
            foreach(var kvp in aspectsAsDictionary)
                Add(kvp.Key,kvp.Value);
        }

        public List<string> KeysAsList()
        {
            return Keys.ToList();
        }

        public int AspectValue(string aspectId)
        {
            if (ContainsKey(aspectId))
                return this[aspectId];

            return 0;
        }

        public void CombineAspects(AspectsDictionary additionalAspects)
        {
            foreach (string k in additionalAspects.Keys)
            {
                if (this.ContainsKey(k))
                    this[k] += additionalAspects[k];
                else
                    Add(k, additionalAspects[k]);
            }
        }

        /// <summary>
        /// for stacks. There was a time when we didn't want to multiply aspects by stack quantity, but that time passed after we decided to use tabletop stacks as well.
        /// </summary>
        /// <param name="quantity"></param>
        public void MultiplyByQuantity(int quantity)
        {
            var keysCopiedForIteration = new List<string>(this.Keys);


            foreach (var k in keysCopiedForIteration)
                this[k] *= quantity;
        }

        /// <summary>
        /// But there is also a time when we want to show aspects without the stack quantity. For example, in a token details window,
        /// where we want to see the paradigmatic single-card, and multiplying the aspect * quantity is confusing. This method us to revert when we need to.
        /// Yes, though, it would be cleaner to have different kinds of AspectDictionary passed around.
        /// </summary>
        /// <param name="quantity"></param>
        public void DivideByQuantity(int quantity)
        {
            var keysCopiedForIteration = new List<string>(this.Keys);


            foreach (var k in keysCopiedForIteration)
                this[k] /= quantity;
        }

        public void ApplyMutations(Dictionary<string, int> mutations)
        {
            foreach (KeyValuePair<string, int> mutation in mutations)
            {
                if (mutation.Value > 0)
                {
                    if (ContainsKey(mutation.Key))
                        this[mutation.Key] += mutation.Value;
                    else
                        Add(mutation.Key, mutation.Value);
                }
                else if (mutation.Value < 0)
                {
                    if (ContainsKey(mutation.Key))
                    {
                        if (AspectValue(mutation.Key) + mutation.Value <= 0)
                            Remove(mutation.Key);
                        else
                            this[mutation.Key] += mutation.Value;
                    }
                    else
                    {
                        //do nothing. We used to log this, but it's an issue when we are eg adding a -1 to remove an element that was added in play.
                        // NoonUtility.Log("Tried to mutate an aspect (" + mutation.Key + ") off an element (" + this._element.Id + ") but the aspect wasn't there.");
                    }
                }
            }
        }
    }
}
