﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SecretHistories.Entities;
using SecretHistories.Interfaces;
using SecretHistories.UI;
using SecretHistories.Constants.Events;
using SecretHistories.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SecretHistories.UI
{
    public class CreditsWindow: MonoBehaviour,ISphereEventSubscriber
    {
        [SerializeField] public ExhibitCards CardsExhibit;
        [SerializeField] public TextMeshProUGUI Responsibilities;
        [SerializeField] public TextMeshProUGUI Names;


        public void OnEnable()
        {
            List<Element> creditCards = Watchman.Get<Compendium>().GetEntitiesAsList<Element>()
                .Where(e => e.Id.StartsWith("credits.")).ToList();


            foreach (var cc in creditCards)
            {
                var card=CardsExhibit.ProvisionElementStackToken(cc.Id, 1,new Context(Context.ActionSource.UI));
            }

            var firstCard = creditCards[0];

            CardsExhibit.HighlightCardWithId(firstCard.Id);
            Responsibilities.text = firstCard.Label;
            Names.text = firstCard.Description;
        }

        public void OnDisable()
        {
         CardsExhibit.RetireAllTokens();

        }

        public void OnTokensChangedForSphere(SphereContentsChangedEventArgs args)
        {
        //
        }

        public void OnTokenInteractionInSphere(TokenInteractionEventArgs args)
        {
           if(args.Interaction==Interaction.OnClicked || args.Interaction == Interaction.OnDoubleClicked)
           {
               CardsExhibit.HighlightCardWithId(args.Token.Payload.Id);
             Responsibilities.text = args.Token.Element.Label;
            Names.text = args.Element.Description;
           }

           if(args.Interaction==Interaction.OnPointerEntered)
               args.Token.Emphasise();

           if (args.Interaction == Interaction.OnPointerExited)
           {
               if (Responsibilities.text != args.Token.Element.Label) // don't remove the highlight if the card is currently selected
                   args.Token.Understate();
           }
        }

    

        
    }
}
