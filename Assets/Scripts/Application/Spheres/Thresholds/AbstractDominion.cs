﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SecretHistories.Abstract;
using SecretHistories.Assets.Scripts.Application.Spheres;
using SecretHistories.Commands;
using SecretHistories.Commands.SituationCommands;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Spheres;
using UnityEditorInternal;
using UnityEngine;

namespace SecretHistories.UI
{
    [IsEncaustableClass(typeof(PopulateDominionCommand))]
    public abstract class AbstractDominion: MonoBehaviour, IEncaustable

  {
      [Encaust]
        public string Identifier { get; protected set; }

        [DontEncaust]
      public OnSphereAddedEvent OnSphereAdded
      {
          get => _onSphereAdded;
          set => _onSphereAdded = value;
      }

      [DontEncaust]
        public OnSphereRemovedEvent OnSphereRemoved
      {
          get => _onSphereRemoved;
          set => _onSphereRemoved = value;
      }

        [Encaust]
        public List<Sphere> Spheres=>new List<Sphere>(_spheres);

        protected readonly List<Sphere> _spheres = new List<Sphere>();
        protected OnSphereAddedEvent _onSphereAdded = new OnSphereAddedEvent();
        protected OnSphereRemovedEvent _onSphereRemoved = new OnSphereRemovedEvent();
        protected IManifestable _manifestable;

        public abstract Sphere TryCreateSphere(SphereSpec spec);
        public abstract bool VisibleFor(string state);
        public abstract bool RelevantTo(string state, Type sphereType);
        public abstract bool RemoveSphere(string id,SphereRetirementType retirementType);
        public abstract bool CanCreateSphere(SphereSpec spec);

        [DontEncaust]
        public bool CurrentlyEvoked {
            get
            {
                if (canvasGroupFader == null)
                    return false;
                return canvasGroupFader.IsVisible();
            }}

        [SerializeField] private CanvasGroupFader canvasGroupFader;

        public virtual void Awake()
        {
            //nothing right now, but a couple of base classes call it
        }

        public virtual void Evoke()
        {
            canvasGroupFader?.Show(); //some subclasses don't need or use it
            
        }

        public virtual void Dismiss()
        {
            canvasGroupFader?.Hide(); //some subclasses don't need or use it

        }

        public virtual void RegisterFor(IManifestable manifestable)
        {
            _manifestable = manifestable;
            manifestable.RegisterDominion(this);

            //if we have permanent spheres created in editor, as for instance in otherworld prefabs, find and add them here.
            //I decided not to make the references explicit in the editor; worth reconsidering that decision if it causes problems later
            var permanentSpheres = gameObject.GetComponentsInChildren<PermanentSphereSpec>();
            foreach (var permanentSphere in permanentSpheres)
            {
                var actualSphere = permanentSphere.GetSphereComponent();
                permanentSphere.ApplySpecToSphere(actualSphere);
                _spheres.Add(actualSphere);
            }

            foreach (Sphere s in Spheres)
            {
                manifestable.AttachSphere(s);
                s.SetContainer(manifestable);
            }


        }



        public  Sphere GetSphereById(string Id)
        {
            return Spheres.SingleOrDefault(s => s.Id == Id && !s.Defunct);
        }


     
  }
}
