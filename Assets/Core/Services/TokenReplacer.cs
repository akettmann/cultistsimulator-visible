﻿using System;
using Assets.Core.Interfaces;
using Noon;

namespace Assets.Core.Services
{
    /// <summary>
    /// tokens as in arbitrary
    /// </summary>
    public class TokenReplacer
    {
        private IGameEntityStorage _character;
        private ICompendium _compendium;
        //'token' as in text-to-be-replaced, not as in DraggableToken
        public TokenReplacer(IGameEntityStorage ch,ICompendium co)
        {
            _character = ch;
            _compendium = co;
        }

        public string ReplaceTextFor(string text)
        {
            string previousCharacterName = _character.GetPastLegacyEventRecord(LegacyEventRecordId.LastCharacterName);
            string lastDesireId = _character.GetPastLegacyEventRecord(LegacyEventRecordId.LastDesire);
            string lastBookId = _character.GetPastLegacyEventRecord(LegacyEventRecordId.LastBook);
            string lastBookLabel = "";
            string lastDesireLabel = "";
            try
            {
                lastBookLabel = _compendium.GetElementById(lastBookId).Label;
                lastDesireLabel = _compendium.GetElementById(lastDesireId).Label;
            }
            catch (Exception e)
            {
                NoonUtility.Log("Duff elementId in PastLegacyEventRecord",1);
            }
            if (text == null)
                return null; //huh. It really shouldn't be - I should be assigning empty string on load -  and yet sometimes it is. This is a guard clause to stop a basic nullreferenceexception
            string replaced = text;

            replaced= replaced.Replace(NoonConstants.TOKEN_PREVIOUS_CHARACTER_NAME, previousCharacterName);

            replaced = replaced.Replace(NoonConstants.TOKEN_LAST_DESIRE, lastDesireLabel);
            replaced = replaced.Replace(NoonConstants.TOKEN_LAST_BOOK, lastBookLabel);

            return replaced;
        }
    }
}