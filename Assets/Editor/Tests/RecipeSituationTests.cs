﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NSubstitute;
using UnityEngine;

namespace  CS.Tests
{
    

[TestFixture]
 public class RecipeSituationTests
{
    private Recipe r1;
    private Recipe r2;
    private RecipeCompendium rc;
    private INotifier notifier;
    private IElementsContainer container;

     [SetUp]
    public void Setup()
        {
         r1 = new Recipe() {Id="r1"}; //NSubstitute doesn't like returning null from mock properties?
        r2 = new Recipe() { Id = "r2" }; //NSubstitute doesn't like returning null from mock properties?
            rc = new RecipeCompendium(new List<Recipe>() {r1,r2},null);


            notifier = Substitute.For<INotifier>();
            container = Substitute.For<IElementsContainer>();
        }

        [Test]
        public void Recipe_WithoutLoopProperty_RequestsContinuation()
        {

            r1.Loop = null;
            RecipeSituation rs = new RecipeSituation(r1, 0);


            rs.Complete(notifier, container,rc);

            Assert.IsNull(rs.Recipe);


        }

        [Test]
        public void Recipe_WithLoopProperty_RequestsContinuation()
        {

            r1.Loop = r2.Id;

            RecipeSituation rs = new RecipeSituation(r1, 0);

            rs.Complete(notifier, container, rc);

            Assert.AreEqual(r2.Id, rs.Recipe.Id);


        }
    }




}