﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Application.Infrastructure.Events;
using SecretHistories.Commands;
using SecretHistories.Elements.Manifestations;
using SecretHistories.Enums;
using SecretHistories.Interfaces;
using SecretHistories.UI;
using UnityEngine;

namespace SecretHistories.Abstract
    {
    public interface ITokenPayload: IEncaustable,IDrivesManifestation
    {
        public event Action<TokenPayloadChangedArgs> OnChanged;
        public event Action<float> OnLifetimeSpent;

        Type GetManifestationType(SphereCategory sphereCategory);
        void InitialiseManifestation(IManifestation manifestation);
        bool IsValidElementStack();
        bool IsValidVerb();
        string UniquenessGroup { get; }
        bool Unique { get; }
        void ExecuteHeartbeat(float interval);
        bool CanMergeWith(ITokenPayload incomingTokenPayload);
        bool Retire(RetirementVFX vfx);
        void AcceptIncomingPayloadForMerge(ITokenPayload incomingTokenPayload);
        void ShowNoMergeMessage(ITokenPayload incomingTokenPayload);
        void SetQuantity(int quantityToLeaveBehind, Context context);
        void ModifyQuantity(int unsatisfiedChange, Context context);

        void ExecuteTokenEffectCommand(ITokenEffectCommand command);

        
    }
    }

