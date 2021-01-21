﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SecretHistories.Constants;
using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Interfaces;
using SecretHistories.Services;
using SecretHistories.UI;

namespace Assets.Scripts.Application.Commands.SituationCommands
{
    public class TokenCreationCommand
    {
        private readonly IVerb _forVerb;
        private readonly TokenLocation _location;
        private readonly Token _sourceToken;

        public TokenCreationCommand(IVerb forVerb,TokenLocation location,Token sourceToken)
        {
            _forVerb = forVerb;
            _location = location;
            _sourceToken = sourceToken;
        }


        public Token Execute(SphereCatalogue sphereCatalogue)
        {
            var sphere = sphereCatalogue.GetSphereByPath(_location.AtSpherePath);
            var token = Watchman.Get<PrefabFactory>().CreateLocally<Token>(sphere.transform);
            token.SetVerb(_forVerb);
    
            sphere.AcceptToken(token, new Context(Context.ActionSource.Unknown));
            token.transform.localPosition = _location.Anchored3DPosition;

            if (_sourceToken != null)
            {
                var enRouteSpherePath =
                    new SpherePath(Watchman.Get<Compendium>().GetSingleEntity<Dictum>().DefaultWindowSpherePath);

                var enrouteSphere = sphereCatalogue.GetSphereByPath(enRouteSpherePath);
                
                var spawnedTravelItinerary = new TokenTravelItinerary(_sourceToken.TokenRectTransform.anchoredPosition3D,
                        token.Sphere.Choreographer.GetFreeLocalPosition(token, _sourceToken.ManifestationRectTransform.anchoredPosition))
                    .WithDuration(1f)
                    .WithSphereRoute(enrouteSphere, token.Sphere)
                    .WithScaling(0f, 1f);

                token.TravelTo(spawnedTravelItinerary, new Context(Context.ActionSource.SpawningAnchor));
            }

            SoundManager.PlaySfx("SituationTokenCreate");

            return token;
        }
    }
}
