﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Assets.Scripts.Application.Entities.NullEntities;
using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

namespace SecretHistories.Entities
{
    public enum EndingFlavour
    {
        None=0,
        Grand=1,
        Melancholy=2,
        Pale=3,
        Vile=4
    }
    [FucineImportable("endings")]
    public class Ending: AbstractEntity<Ending>
    {

        [FucineValue(DefaultValue = "", Localise = true)]
        public string Label { get; set; }

        [FucineValue(DefaultValue = "", Localise = true)]
        public string Description { get; set; }

        [FucineValue("")]
        public string Comments { get; set; }

        [FucineValue("")]
        public string Image { get; set; }

        [FucineValue((int)EndingFlavour.Melancholy)]
        public EndingFlavour Flavour { get; set; }

        [FucineValue("")]
        public string Anim { get; set; }

        [FucineValue("")]
        public string Achievement { get; set; }

        public virtual bool IsValid()
        {
            if (string.IsNullOrEmpty(Id))
                return false;

            return true;
        }

        public Ending(EntityData importDataForEntity, ContentImportLog log) : base(importDataForEntity, log)
        {

        }


        public Ending()
        {
        }

        //if we use subclasses for null, that borks serialization
        public static Ending NotEnded()
        {
            var nullEnding=new Ending();
            return nullEnding;
        }

        public static Ending DefaultEnding()
        {
            Ending defaultEnding = new Ending
            {
                _id = "default",
                Label = "IT'S ALWAYS TOO LATE, EVENTUALLY",
                Description = "'... but until then, it's not.'",
                Image = "suninrags",
                Flavour = EndingFlavour.Melancholy,
                Anim = "DramaticLight",
                Achievement = null
            };


            return defaultEnding;

        }

        protected override void OnPostImportForSpecificEntity(ContentImportLog log, Compendium populatedCompendium)
        {
            
        }
    }
}
