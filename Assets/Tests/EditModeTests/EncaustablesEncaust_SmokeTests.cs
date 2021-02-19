﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Application.Commands;
using Assets.Scripts.Application.Entities.NullEntities;
using NUnit.Framework;
using NUnit.Framework.Internal;
using SecretHistories.Commands;
using SecretHistories.Entities;
using SecretHistories.Entities.Verbs;
using SecretHistories.Fucine;
using SecretHistories.NullObjects;
using SecretHistories.Spheres;
using SecretHistories.UI;
using UnityEngine;
using Object = UnityEngine.Object;


[TestFixture]
    public class EncaustablesEncaust_SmokeTests
    {
        private Sphere sphere;

        [SetUp]
        public void Setup()
        {
            Watchman.ForgetEverything();
            var sphereObject = new GameObject();
            sphere = sphereObject.AddComponent<NullSphere>();
        }

        [Test]
        public void DeckInstance_Encausts()
        {
            var encaustery = new Encaustery<DeckInstanceCreationCommand>();
            var deckInstance = new GameObject().AddComponent<DeckInstance>();
            encaustery.Encaust(deckInstance);
        }
    [Test]
        public void ElementStack_Encausts()
        {
            var encaustery = new Encaustery<ElementStackCreationCommand>();
            var elementStack = new ElementStack();
            encaustery.Encaust(elementStack);
        }



    [Test]
        public void Character_Encausts()
        {

         var encaustery=new Encaustery<CharacterCreationCommand>();
         var characterObject = new GameObject();
         characterObject.AddComponent<Character>();
        characterObject.GetComponent<Character>().ActiveLegacy=new NullLegacy();
        characterObject.GetComponent<Character>().EndingTriggered=NullEnding.Create();
        //pretty horrible, right? worth considering not passing Monobehaviours to encausting, OR use the CreationCommand in the first place!


        encaustery.Encaust(characterObject.GetComponent<Character>());
                }

        [Test]
        public void ElementStackToken_Encausts()
        {
            var encaustery = new Encaustery<TokenCreationCommand>();
          
            var tokenObject=new GameObject();
            var token=tokenObject.AddComponent<Token>();
            var elementStack = new ElementStack();
            sphere.AcceptToken(token,new Context(Context.ActionSource.Unknown));
            token.SetPayload(elementStack);
            encaustery.Encaust(token);
        }
        [Test]
        public void Situation_Encausts()
        {
            var situationEncaustery = new Encaustery<SituationCreationCommand>();
            var situation = new Situation(NullVerb.Create(), new FucinePath("./s1!t1"));
            situationEncaustery.Encaust(situation);
        }

    
    [Test]
        public void SituationToken_Encausts()
        {
           var encaustery=new Encaustery<TokenCreationCommand>();
           var tokenObject = new GameObject();
           var token=tokenObject.AddComponent<Token>();

           var situation=new Situation(NullVerb.Create(),new FucinePath("./s1!t1"));
           sphere.AcceptToken(token, new Context(Context.ActionSource.Unknown));
           token.SetPayload(situation);

           encaustery.Encaust(token);
        }

        [Test]
        public void Sphere_Encausts()
        {
            var encaustery = new Encaustery<SphereCreationCommand>();
        var sphereObject = new GameObject();
        Sphere sphere = sphereObject.AddComponent<ThresholdSphere>();

        encaustery.Encaust(sphere);
        }


        [Test]
        public void SituationToken_EncaustsWithMemberSpheres()
        {
            var encaustery=new Encaustery<TokenCreationCommand>();
        var tokenObject = new GameObject();
        var token = tokenObject.AddComponent<Token>();

        var situation = new Situation(NullVerb.Create(), new FucinePath("./s1!t1"));
        sphere.AcceptToken(token, new Context(Context.ActionSource.Unknown));
        token.SetPayload(situation);

        var encaustedToken= encaustery.Encaust(token);
        Assert.AreEqual(situation.Spheres.Count, encaustedToken.Payload.Spheres);
        }

    [Test]
        public void DropzoneToken_Encausts()
        {
            var encaustery = new Encaustery<DropzoneCreationCommand>();
           var dropzone=new Dropzone();
           encaustery.Encaust(dropzone);

        }

}
