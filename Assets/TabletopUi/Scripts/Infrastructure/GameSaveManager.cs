﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assets.Core.Entities;
using Assets.Core.Interfaces;
using Assets.TabletopUi.Scripts.Interfaces;
using Assets.TabletopUi.Scripts.Services;
using Noon;
using OrbCreationExtensions;

namespace Assets.TabletopUi.Scripts.Infrastructure
{
    public class GameSaveManager
    {
        private readonly IGameDataImporter dataImporter;
        private readonly IGameDataExporter dataExporter;

        
        public GameSaveManager(IGameDataImporter dataImporter,IGameDataExporter dataExporter)
        {
            this.dataImporter = dataImporter;
            this.dataExporter = dataExporter;
        }

        public bool DoesGameSaveExist()
        {
            return File.Exists(NoonUtility.GetGameSaveLocation());
        }

        public bool IsSavedGameActive()
        {
            var htSave = RetrieveHashedSaveFromFile();
            return htSave.ContainsKey(SaveConstants.SAVE_ELEMENTSTACKS) || htSave.ContainsKey(SaveConstants.SAVE_SITUATIONS);
        }

        //copies old version in case of corruption
        private void BackupSave()
        {
            File.Copy(NoonUtility.GetGameSaveLocation(), NoonUtility.GetBackupGameSaveLocation(),true);

        }
        
        /// <summary>
        /// for saving from the game over or legacy choice screen, when the player is between active games. It's also used when restarting the game
        /// by reloading the tabletop scene - this is hacky, because we just don't save the availablelegacies and chosenlegacies, and rely on the
        /// ChosenLegacy remaining in cross-scene state. It would be better to go via  somewhere that inspects state and routes accordingly
        /// </summary>
        public void SaveInactiveGame()
        {
            var htSaveTable = dataExporter.GetHashtableForExtragameState();
            BackupSave();
            File.WriteAllText(NoonUtility.GetGameSaveLocation(), htSaveTable.JsonString());

        }

        //for saving from the tabletop
        public void SaveActiveGame(TabletopContainer tabletopContainer,Character character)
        {
            var allStacks = tabletopContainer.GetElementStacksManager().GetStacks();
            var allSituationTokens = tabletopContainer.GetAllSituationTokens();
            var allDecks = character.DeckInstances;

            var htSaveTable = dataExporter.GetSaveHashTable(character,allStacks,
                allSituationTokens,allDecks);

            BackupSave();
            File.WriteAllText(NoonUtility.GetGameSaveLocation(), htSaveTable.JsonString());
        }

        public Hashtable RetrieveHashedSaveFromFile()
        {
            string importJson = File.ReadAllText(NoonUtility.GetGameSaveLocation());
            Hashtable htSave = SimpleJsonImporter.Import(importJson);
            return htSave;
        }


        public void ImportHashedSaveToState(TabletopContainer tabletopContainer, IGameEntityStorage storage, Hashtable htSave)
        {
            dataImporter.ImportSavedGameToState(tabletopContainer, storage, htSave);
        }

        public SavedCrossSceneState RetrieveSavedCrossSceneState()
        {
            var htSave = RetrieveHashedSaveFromFile();
            return dataImporter.ImportCrossSceneState(htSave);

        }


    }
}
