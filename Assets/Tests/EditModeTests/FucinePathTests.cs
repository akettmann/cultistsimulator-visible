﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Application.Fucine;
using NUnit.Framework;
using SecretHistories.Fucine;

namespace Assets.Tests.EditModeTests
{
    [TestFixture]
   public class FucinePathTests
    {

        [Test]
        public void TokenPathId_CanBeSpecifiedWithBang()
        {
            var tokenPathId = new TokenPathPart("!foo");
            Assert.AreEqual("!foo", tokenPathId.ToString());
        }

        [Test]
        public void TokenPathId_SpecifiedWithoutABang_PrependsBang()
        {
            var tokenPathId = new TokenPathPart("foo");
            Assert.AreEqual( "!foo", tokenPathId.ToString());

        }


        [Test]
        public void SpherePathId_CanBeSpecifiedWithForwardSlash()
        {
            var spherePathId=new SpherePathPart("/foo");
            Assert.AreEqual("/foo",spherePathId.ToString());
        }

        [Test]
        public void SpherePathId_SpecifiedWithoutForwardSlash_PrependsSlash()
        {
            var spherePathId = new SpherePathPart("foo");
            Assert.AreEqual("/foo", spherePathId.ToString());

        }

        [Test]
        public void FucineRootPath_ParsesCorrectly()
        {
            var rootPath=new FucinePath(".");
            Assert.AreEqual(".",rootPath.ToString());
        }

        
        [Test]
        public void FucinePath_WithRootAsOnlyMember_IsAbsolute()
        {
            var absolutePath = new FucinePath(".");
            Assert.IsTrue(absolutePath.IsAbsolute());
        }

        
        [Test]
        public void FucinePath_WithRootInFirstPosition_IsAbsolute()
        {
            var absolutePath=new FucinePath("./tabletop");
            Assert.IsTrue(absolutePath.IsAbsolute());
        }

        [Test]
        public void FucinePath_WithoutRootInFirstPosition_IsNotAbsolute()
        {
            var relativePath=new FucinePath("/somesphereid");
            Assert.IsFalse(relativePath.IsAbsolute());

        }

        [Test]
        public void FucinePath_Parse_RenderToString()
        {
            var rootPath=new FucinePath(".");
            Assert.AreEqual(".",rootPath.ToString());

            var absoluteSpherePath = new FucinePath("./sphereid");
            Assert.AreEqual("./sphereid",absoluteSpherePath.ToString());


            var relativeSpherePath = new FucinePath("/sphereId");
            Assert.AreEqual("/sphereId",relativeSpherePath.ToString());

            var relativeTokenPath = new FucinePath("!tokenId");
            Assert.AreEqual("!tokenId",relativeTokenPath.ToString() );

            var complexPath= new FucinePath("./sphereId1/tokenIdA/sphereId2/tokenIdB");
            Assert.AreEqual("./sphereId1/tokenIdA/sphereId2/tokenIdB",complexPath.ToString());

        }

        [Test]
        public void FucinePath_WithTokenXAtEndOfList_ReturnsPathUpToXAsToken()
        {
            var pathWithTokenAtEnd=new FucinePath("./spherez!tokenx");
            Assert.AreEqual("./spherez!tokenx", pathWithTokenAtEnd.TokenPath.ToString());
            
        }

        [Test]
        public void FucinePath_WithSphereAtEndOfList_ReturnsPathUpToPreviousToken_AsToken()
        {
            var pathWithSphereAtEnd=new FucinePath("./spherez!tokenx/spherey");
            Assert.AreEqual("./spherez!tokenx",pathWithSphereAtEnd.TokenPath.ToString());

        }

        [Test]
        public void FucinePath_WithSphereYAtEndOfList_ReturnsPathUpToYAsSphere()
        {
            var pathWithSphereAtEnd = new FucinePath("./spherez!tokenx/spherey");
            Assert.AreEqual("./spherez!tokenx/spherey",pathWithSphereAtEnd.SpherePath.ToString());

        }

        [Test]
        public void FucinePath_WithTokenAtEndOfList_ReturnsPathUpToPreviousYAsSphere()
        {
            var pathWithTokenAtEnd = new FucinePath("./spherey!tokenx");
            Assert.AreEqual("./spherey",pathWithTokenAtEnd.SpherePath.ToString());

        }

        [Test]
        public void RootPathFollowedBySituationIsInvalid()
        {
         var pathFollowedBySituation=new FucinePath(".!token");
         Assert.IsFalse(pathFollowedBySituation.IsValid());
        }

        [Test]
        public void WeDoSomethingSensibleWhenAFucinePathIsInitialisedWithEmptyString()
        {
            var emptyPath=new FucinePath("");
            Assert.IsFalse(emptyPath.IsValid());
            var whitespaceEmptyPath=new FucinePath(" ");
            Assert.IsFalse(whitespaceEmptyPath.IsValid());

        }

        [Test]
        public void WeDoSomethingSensibleWhenAFucinePathIsInitialisedWithNull()
        {
            var nullPath = new FucinePath(null as FucinePathPart);
            Assert.IsFalse(nullPath.IsValid());
            var nullStringPath=new FucinePath(null as string);
            Assert.IsFalse(nullStringPath.IsValid());
        }

        [Test]
        public void Current_DoesSomethingSensible()
        {
            throw new NotImplementedException();
        }

    }
}
