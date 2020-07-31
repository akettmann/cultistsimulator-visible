﻿using System;
using System.Linq;
using Assets.CS.TabletopUI;
using Assets.TabletopUi.Scripts.Infrastructure.Modding;
using Noon;
using Steamworks;

namespace Assets.TabletopUi.Scripts.Infrastructure
{
    public class SteamworksStorefrontClientProvider : IStoreFrontClientProvider
    {
        private CGameID _gameId;


        private CallResult<CreateItemResult_t> r_itemCreated;
        private CallResult<DeleteItemResult_t> r_itemDeleted;
        private CallResult<SubmitItemUpdateResult_t> r_itemUpdateCompleted;

        public SteamworksStorefrontClientProvider()
        {
            if (!SteamManager.Initialized)
                return;

            // Cache the GameID for use in the Callbacks
            _gameId = new CGameID(SteamUtils.GetAppID());

            
            // Set up Steam callbacks
            Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);

            r_itemCreated = CallResult<CreateItemResult_t>.Create(OnWorkshopItemCreated);
            r_itemDeleted = CallResult<DeleteItemResult_t>.Create(OnWorkshopItemDeleted);
            r_itemUpdateCompleted = CallResult<SubmitItemUpdateResult_t>.Create(OnWorkshopItemUpdateCompleted);

            // Fetch the initial list of achievements
            SteamUserStats.RequestCurrentStats();
        }

        public void SetAchievement(string achievementId, bool setStatus)
        {
            if (string.IsNullOrEmpty(achievementId))
                return;

            if (!SteamManager.Initialized)
            {
                NoonUtility.Log($"No Steamworks client initialised: not setting a Steam achievement for {achievementId}");
                return;
            }

            if (!SteamUserStats.GetAchievement(achievementId, out var state))
            {
                NoonUtility.Log($"Trying to set Steam achievement {achievementId}, but it doesn't exist");
                return;
            }
            if (state != setStatus)
            {
                if (setStatus)
                    SteamUserStats.SetAchievement(achievementId);
                else
                    SteamUserStats.ClearAchievement(achievementId);
                SteamUserStats.StoreStats();
                NoonUtility.Log((setStatus ? "Set" : "Unset") + $" Steam achievement: {achievementId}");
            }
            else
                NoonUtility.Log(
                    "Trying to " + (setStatus ? "set" : "unset") 
                                 + $" Steam achievement {achievementId}, but it's already " 
                                 + (state ? "set" : "unset"), 
                    1);
        }
        
        private void OnUserStatsReceived(UserStatsReceived_t pCallback) 
        {
            if (!SteamManager.Initialized)
                return;

            // Ignore callbacks from other games
            if ((ulong) _gameId != pCallback.m_nGameID) 
                return;

            NoonUtility.Log(pCallback.m_eResult == EResult.k_EResultOK
                ? "Received achievements from Steam"
                : $"Failed to fetch achievements: Code {pCallback.m_eResult}");
        }


        public void Inbook()
        {
            //make a call to the API and give it a handle
            SteamAPICall_t handle = SteamUGC.CreateItem(_gameId.AppID(),
                EWorkshopFileType.k_EWorkshopFileTypeCommunity);

            //associate the previously created call result with it
            r_itemCreated.Set(handle);
            //and when it's completed, the call result has a delegate that it calls in turn
        }

        private void OnWorkshopItemCreated(CreateItemResult_t callback, bool ioFailure)
        {
            NoonUtility.Log(callback.m_bUserNeedsToAcceptWorkshopLegalAgreement);
            NoonUtility.Log(callback.m_nPublishedFileId);

            StartItemUpdate(callback.m_nPublishedFileId);
        }

        private void StartItemUpdate(PublishedFileId_t callbackPublishedFileId)
        {
            UGCUpdateHandle_t updateHandle = SteamUGC.StartItemUpdate(_gameId.AppID(),
                callbackPublishedFileId);

            var modToUpload = GetModForUpload();

            SteamUGC.SetItemTitle(updateHandle, modToUpload.Name);
            SteamUGC.SetItemDescription(updateHandle, modToUpload.Description);
            SteamUGC.SetItemContent(updateHandle, modToUpload.ModRootFolder);


            SteamAPICall_t updateCompleteHandle = SteamUGC.SubmitItemUpdate(updateHandle, "update at: " + DateTime.Now);
            r_itemUpdateCompleted.Set(updateCompleteHandle);
        }


        private void OnWorkshopItemUpdateCompleted(SubmitItemUpdateResult_t callback, bool IOFailure)
        {
            if (IOFailure)
            {
                NoonUtility.Log("ARKK IO UPDATE FAILURE");
            }
            else
            {
                NoonUtility.Log("Update completed for item " + callback.m_nPublishedFileId + " with result " + callback.m_eResult);
            }
        }


        public void Outbook(string publishedFileId)
        {
            UInt32 FileId = Convert.ToUInt32(publishedFileId);

            //make a call to the API and give it a handle
            SteamAPICall_t handle = SteamUGC.DeleteItem((PublishedFileId_t)FileId);

            //associate the previously created call result with it
            r_itemDeleted.Set(handle);
            //and when it's completed, the callresult has a delegate that it calls in turn
        }

        private void OnWorkshopItemDeleted(DeleteItemResult_t pCallback, bool bIOFailure)
        {

            NoonUtility.Log(pCallback.m_nPublishedFileId);
            NoonUtility.Log(pCallback.m_eResult);
        }

        private Mod GetModForUpload()
        {
            var modManager = Registry.Retrieve<ModManager>();
            var firstMod = modManager.GetAllActiveMods().First();
            NoonUtility.Log("first mod active: " + firstMod.Name);

            return firstMod;
        }
    }
}
