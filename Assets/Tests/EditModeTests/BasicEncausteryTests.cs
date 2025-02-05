﻿using System;
using Newtonsoft.Json;
using SecretHistories.Abstract;

using NUnit.Framework;
using SecretHistories.Commands;
using SecretHistories.Commands.Encausting;
using SecretHistories.Entities;
using SecretHistories.Fucine;
using UnityEditor;

namespace BasicEncaustingTests
{
    [TestFixture]
    public class BasicEncausteryTests
    {
        [Test]
        public void Encausting_ReturnsInstanceOfSpecifiedCommandType_ForValidEncaustable()
        {
            var encaustery = new Encaustery<EncaustedCommandX>();
            var vex = new ValidEncaustableX();
            Assert.IsInstanceOf<EncaustedCommandX>(encaustery.Encaust(vex));
        }

        [Test]
        public void EncaustingThrowsException_WhenEncausteryPassedNonEncaustableClass()
        {
            var encaustery = new Encaustery<EncaustedCommandX>();
            var nex = new NonEncaustableX();
            Assert.Throws<ApplicationException>(() => encaustery.Encaust(nex));
        }

        [Test]
        public void EncaustingThrowsException_WhenGenericArgumentDoesntMatch_ToTypeForAttribute()
        {
            var encaustery = new Encaustery<SituationCreationCommand>();
            var vx = new ValidEncaustableX();
            Assert.Throws<ApplicationException>(() => encaustery.Encaust(vx));
        }


        [Test]
        public void EncaustingThrowsError_WhenPropertyEncaustmentStatusIsUnmarked()
        {
            var encaustery = new Encaustery<EncaustedCommandX>();
            var eupx = new EncaustableWithUnmarkedPropertyX();
            Assert.Throws<ApplicationException>(() => encaustery.Encaust(eupx));
        }

        [Test]
        public void EncaustingDoesntThrowError_WhenPropertyEncaustmentStatusInBaseClassIsUnmarked()
        {
            var encaustery = new Encaustery<EncaustedCommandX>();
            var dx = new DerivedEncaustableClassX();
            encaustery.Encaust(dx);
        }

        [Test]
        public void EncaustablePropertyValue_PopulatesMatchInIEncaustment()
        {
            var encaustery = new Encaustery<EncaustedCommandX>();
            var vx = new ValidEncaustableX { MarkedProperty = 1, OtherMarkedProperty = 2 };
            var encaustedCommand = encaustery.Encaust(vx);
            Assert.AreEqual(1, encaustedCommand.MarkedProperty);
            Assert.AreEqual(2, encaustedCommand.OtherMarkedProperty);
        }

        [Test]
        public void EncaustablePropertyWithoutCommandPropertyOfSameName_ThrowsApplicationException()
        {
            var encaustery = new Encaustery<EncaustedCommandX>();
            var ex = new EncaustableWithAPropertyThatCommandXDoesntHave { MarkedPropertyWithoutMatchInCommmand = 1 };
            Assert.Throws<ApplicationException>(() => encaustery.Encaust(ex));
        }

        [Test]
        public void CommandPropertyWithoutEncaustablePropertyOfSameName_ThrowsApplicationException()
        {
            var encaustery = new Encaustery<EncaustedCommandX>();
            var ex = new EncaustableMissingAPropertyThatCommandXHas();

            Assert.Throws<ApplicationException>(() => encaustery.Encaust(ex));
        }
        [Test]
        public void IEncaustment_Serialises()
        {
            var encaustery = new Encaustery<EncaustedCommandX>();
            var vx = new ValidEncaustableX();
            var encaustedCommand = encaustery.Encaust(vx);
            var jsonPortal=new SerializationHelper();

            var json = jsonPortal.SerializeToJsonString(encaustedCommand);
            Assert.IsInstanceOf<string>(json);
        }


        [Test]
        public void IEncaustment_ValuesMaintainedOnReserialisation()
        {
            var encaustery = new Encaustery<EncaustedCommandX>();
            var vx = new ValidEncaustableX { MarkedProperty = 1, OtherMarkedProperty = 2 };
            var encaustedCommand = encaustery.Encaust(vx);

            var jsonPortal = new SerializationHelper();
            var json = jsonPortal.SerializeToJsonString(encaustedCommand);


            var dx = jsonPortal.DeserializeFromJsonString<EncaustedCommandX>(json);
            Assert.AreEqual(vx.MarkedProperty,dx.MarkedProperty);
            Assert.AreEqual(vx.OtherMarkedProperty,dx.OtherMarkedProperty);
        }

        [Test]
        public void EmulousEncaustable_UsesEncaustmentInformationFromBaseEncaustable()
        {
            var encaustery=new Encaustery<EncaustedCommandX>();
            var emulite=new EmulousEncaustableX();
            emulite.MarkedProperty = 1;
            var commandx= encaustery.Encaust(emulite);
            Assert.AreEqual(emulite.MarkedProperty,commandx.MarkedProperty);
        }

    }

public class NonEncaustableX : IEncaustable
{ }

public class UsefulBaseClassThatIsntIntendedEncaustableX
{
    public int PropertyThatShouldntBeCheckedForEncaustAttributes { get; }
}

[IsEncaustableClass(typeof(EncaustedCommandX))]
public class DerivedEncaustableClassX: UsefulBaseClassThatIsntIntendedEncaustableX,IEncaustable
{
    [Encaust]
    public int MarkedProperty { get; set; }
    [Encaust]
    public int OtherMarkedProperty { get; set; }
    }

    [IsEncaustableClass(typeof(EncaustedCommandX))]
public class EncaustableWithUnmarkedPropertyX : IEncaustable
{
    [Encaust]
    public int MarkedProperty { get; set; }
    public int UnmarkedProperty { get; set; }
}

[IsEncaustableClass(typeof(EncaustedCommandX))]
public class ValidEncaustableX : IEncaustable
{
    [Encaust]
    public int MarkedProperty { get; set; }
    [Encaust]
    public int OtherMarkedProperty { get; set; }
    [DontEncaust]
    public int MarkedAsDontEncaustProperty { get; set; }

    public int UnmarkedFIeldShouldBeOkay;
}

[IsEmulousEncaustable(typeof(ValidEncaustableX))]
public class EmulousEncaustableX : ValidEncaustableX
    {
public int PropertyExistsOnlyOnEmulousSubclass { get; set; }
}


    [IsEncaustableClass(typeof(EncaustedCommandX))]
public class EncaustableWithAPropertyThatCommandXDoesntHave : IEncaustable
{
    [Encaust]
    public int MarkedProperty { get; set; }
    [Encaust]
    public int OtherMarkedProperty { get; set; }
    [Encaust]
    public int MarkedPropertyWithoutMatchInCommmand { get; set; }
}

[IsEncaustableClass(typeof(EncaustedCommandX))]
public class EncaustableMissingAPropertyThatCommandXHas : IEncaustable
{
    [Encaust]
    public int MarkedProperty { get; set; }
    [DontEncaust]
    public int OtherMarkedProperty { get; set; }
}


public class EncaustedCommandX:IEncaustment
{
    public int MarkedProperty { get; set; }
    public int OtherMarkedProperty { get; set; }

}



}