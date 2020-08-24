﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Assets.Core.Entities;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Noon;
using OrbCreationExtensions;
using UnityEngine;	// added for debug asserts - CP
using UnityEngine.Analytics;

namespace Assets.TabletopUi.Scripts.Infrastructure
{
    public class GameSaveManager
    {
        private readonly IGameDataImporter dataImporter;
        private readonly IGameDataExporter dataExporter;

		// Save game safety
		public static int failedSaveCount = 0;
		public static bool saveErrorWarningTriggered = false;	// so that tabletop knows not to unpause
#if UNITY_EDITOR
		public static bool simulateBrokenSave = false;	// For debugging
#endif
        public GameSaveManager(IGameDataImporter dataImporter,IGameDataExporter dataExporter)
        {
            this.dataImporter = dataImporter;
            this.dataExporter = dataExporter;
        }

        public bool DoesGameSaveExist()
        {
            return File.Exists(NoonUtility.GetGameSaveLocation());
        }

        public bool IsSavedGameActive(int index = 0, bool temp = false)
        {
            var htSave = RetrieveHashedSaveFromFile(index, temp);
            return htSave.ContainsKey(SaveConstants.SAVE_ELEMENTSTACKS) || htSave.ContainsKey(SaveConstants.SAVE_SITUATIONS);
        }

        //copies old version in case of corruption
        private void BackupSave(int index = 0)
        {
			const int MAX_BACKUPS = 5;
			// Back up a number of previous saves
            for (int i=MAX_BACKUPS-1; i>=1; i--)
			{
				if (File.Exists(NoonUtility.GetBackupGameSaveLocation(i)))	//otherwise we can't copy it
	                File.Copy  (NoonUtility.GetBackupGameSaveLocation(i), NoonUtility.GetBackupGameSaveLocation(i+1),true);
			}
			// Back up the main save
			if (File.Exists(NoonUtility.GetGameSaveLocation(index)))	//otherwise we can't copy it
                File.Copy  (NoonUtility.GetGameSaveLocation(index), NoonUtility.GetBackupGameSaveLocation(1),true);
		}

        /// <summary>
        /// for saving from the game over or legacy choice screen, when the player is between active games. It's also used when restarting the game
        /// by reloading the tabletop scene - hence withActiveLegacy
        /// </summary>
        public void SaveInactiveGame(Legacy withActiveLegacy)
        {
            BackupSave();

            var htSaveTable = dataExporter.GetHashtableForExtragameState(withActiveLegacy);
            File.WriteAllText(NoonUtility.GetGameSaveLocation(), htSaveTable.JsonString());
        }

        public void DeleteCurrentSave()
        {
            if(File.Exists(NoonUtility.GetGameSaveLocation()))
                File.Delete(NoonUtility.GetGameSaveLocation());
        }

        public IEnumerator<bool?> SaveActiveGameAsync(IEnumerable<IElementStack> allStacks, List<SituationController> currentSituationControllers, Character character, bool forceBadSave = false, int index = 0)
        {
    
              //  var allStacks = tabletop.GetElementStacksManager().GetStacks();
              //  var currentSituationControllers = Registry.Get<SituationsCatalogue>().GetRegisteredSituations();
                var metaInfo = Registry.Get<MetaInfo>();
                var allDecks = character.DeckInstances;


                // GetSaveHashTable now does basic validation of the data and might return null if it's bad - CP
                var htSaveTable = dataExporter.GetSaveHashTable(metaInfo,character,allStacks,currentSituationControllers,allDecks,forceBadSave);

                // Universal catch-all. If data is broken, abort save and retry 5 seconds later - assuming problem circumstance has completed - CP
                // If we fail several of these in a row then something is permanently broken in the data and we should alert user :(
                if (htSaveTable == null && !forceBadSave)
                {
                    HandleSaveError(allStacks,currentSituationControllers, character, index);
                    yield return false;	// Something went wrong with the save
                    yield break;
                }
                if (forceBadSave)
                {
                    var postSaveFilePath = NoonUtility.GetErrorSaveLocation(DateTime.Now, "post");
                    System.Threading.Tasks.Task.Run(() => RunSave(postSaveFilePath, htSaveTable)).Wait();
                }
                else
                {
                    // Write the save to a temporary file first, check that it is valid, and only then backup the old save
                    // This is to mitigate some bugs where save files would end up with invalid data written to them
                    var saveFilePath = NoonUtility.GetGameSaveLocation(index);
                    var tempSaveFilePath = NoonUtility.GetTemporaryGameSaveLocation(index);
                    var saveTask = System.Threading.Tasks.Task.Run(() => RunSave(tempSaveFilePath, htSaveTable));
                    while (!(saveTask.IsCompleted || saveTask.IsCompleted || saveTask.IsFaulted))
                    {
                        yield return null;
                    }

                    bool success;
                    if (saveTask.Exception == null)
                    {
                        success = IsSavedGameActive(index, true);
                    }
                    else
                    {
                        Debug.LogError("Failed to save game to temporary save file (see exception for details)");
                        Debug.LogException(saveTask.Exception);
                        success = false;
                    }

                    if (success)
                    {
                        try
                        {
                            BackupSave(index);
                            if (File.Exists(saveFilePath))
                            {
                                File.Delete(saveFilePath);
                            }

                            File.Move(tempSaveFilePath, saveFilePath);

                            if (failedSaveCount > 0)
                            {
                                Analytics.CustomEvent( "autosave_recovered", new Dictionary<string,object>{ { "failedSaveCount", failedSaveCount } } );
                            }

                            failedSaveCount = 0;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("Failed to move temporary file (see exception for details)");
                            Debug.LogException(e);
                            success = false;
                        }
                    }
                    if (!success)
                    {
                        HandleSaveError(allStacks,currentSituationControllers, character, index);
                        yield return false;
                        yield break;
                    }
                }
           
            yield return true;
        }

        public Hashtable RetrieveHashedSaveFromFile(int index = 0, bool temp = false)
        {
            string importJson = File.ReadAllText(
	            temp ? NoonUtility.GetTemporaryGameSaveLocation(index) : NoonUtility.GetGameSaveLocation(index));
            Hashtable htSave = SimpleJsonImporter.Import(importJson);
            return htSave;
        }


        public void ImportHashedSaveToState(TabletopTokenContainer tabletop, Character storage, Hashtable htSave)
        {
            dataImporter.ImportSavedGameToState(tabletop, storage, htSave);
        }



        public string GetLegacyIdFromSavedGame()
        {
            Hashtable htSave;
            string LEGACY_KEY = "activeLegacy";
            try
            {
                htSave = RetrieveHashedSaveFromFile();
            }
            catch(Exception e)
            {
                Debug.LogError("Failed to load game (see exception for details)");
                Debug.LogException(e);
                return "";
            }
            if (htSave == null)
                return string.Empty;
            var htCharacter = htSave.GetHashtable(SaveConstants.SAVE_CHARACTER_DETAILS);
            if (htCharacter == null)
                return string.Empty;
            if(!htCharacter.ContainsKey(LEGACY_KEY))
                return String.Empty;

            return htCharacter[LEGACY_KEY].ToString();
            
            
        }

        private void HandleSaveError(IEnumerable<IElementStack> stacks, List<SituationController> controllers, Character character, int index = 0)
        {
	        failedSaveCount++;
	        if (failedSaveCount != 3) 
		        return;
	        
	        // Back up main save
            var savePath = NoonUtility.GetGameSaveLocation(index);
            if (File.Exists(savePath))  // otherwise we can't copy it
	            File.Copy(savePath, NoonUtility.GetErrorSaveLocation(DateTime.Now, "pre"), true);
                
            // Force a bad save into a different filename
            var saveTask = SaveActiveGameAsync(stacks,controllers, character, true, index);
            while (saveTask.MoveNext())
            {
            }
            Analytics.CustomEvent("autosave_corrupt_notified");
                
            // Pop up warning message
            Registry.Get<INotifier>().ShowSaveError(true);
            saveErrorWarningTriggered = true;
        }

        private void RunSave(string saveFilePath, Hashtable saveData)
        {
	        File.WriteAllText(saveFilePath, saveData.JsonString());
        }
    }
}
