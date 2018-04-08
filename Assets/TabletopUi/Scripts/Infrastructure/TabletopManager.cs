﻿#pragma warning disable 0649
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Core;
using Assets.Core.Commands;
using Assets.Core.Entities;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI.Interfaces;
using Assets.Logic;
using Assets.TabletopUi;
using Assets.TabletopUi.Scripts;
using Assets.TabletopUi.Scripts.Infrastructure;
using Assets.TabletopUi.Scripts.Interfaces;
using Assets.TabletopUi.Scripts.Services;
using Assets.TabletopUi.UI;
using Noon;
using OrbCreationExtensions;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.WSA;

namespace Assets.CS.TabletopUI {
    public class TabletopManager : MonoBehaviour {

        [Header("Game Control")]
        [SerializeField]
        private Heart _heart;
        [SerializeField]
        private SpeedController _speedController;
        [SerializeField]
        private CardAnimationController _cardAnimationController;
        [SerializeField]
        private EndGameAnimController _endGameAnimController;

        [Header("Tabletop")]
        [SerializeField]
        public TabletopTokenContainer _tabletop;
        [SerializeField]
        private Limbo Limbo;

        [Header("Mansus Map")]
        [SerializeField]
        private MapController _mapController;
        [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("mapContainsTokens")]
        public MapTokenContainer mapTokenContainer;
        [SerializeField]
        TabletopBackground mapBackground;
        [SerializeField]
        MapAnimation mapAnimation;

        [Header("Drag & Window")]
        [SerializeField]
        private RectTransform draggableHolderRectTransform;
        [SerializeField]
        Transform tableLevelTransform;
        [SerializeField]
        Transform windowLevelTransform;

        [Header("Options Bar & Notes")]
        [SerializeField]
        private StatusBar StatusBar;

        [SerializeField]
        private DebugTools debugTools;
        [SerializeField]
        private BackgroundMusic backgroundMusic;
        [SerializeField]
        private HotkeyWatcher _hotkeyWatcher;

        [SerializeField]
        private Notifier _notifier;
        [SerializeField]
        private OptionsPanel _optionsPanel;
        [SerializeField]
        private ElementOverview _elementOverview;

        private SituationBuilder _situationBuilder;

        bool isInNonSaveableState;
        private SituationController mansusSituation;

        public void Update() {
            _hotkeyWatcher.WatchForGameplayHotkeys();
            _cardAnimationController.CheckForCardAnimations();
        }

        #region -- Intialisation -------------------------------

        void Start() {
            _situationBuilder = new SituationBuilder(tableLevelTransform, windowLevelTransform);

            //register everything used gamewide
            SetupServices(_situationBuilder, _tabletop);

            // This ensures that we have an ElementStackManager in Limbo & Tabletop
            InitializeTokenContainers();

            //we hand off board functions to individual controllers
            InitialiseSubControllers(
                _speedController, 
                _hotkeyWatcher, 
                _cardAnimationController, 
                _mapController, 
                _endGameAnimController, 
                _notifier,
                _optionsPanel
                );

            InitialiseListeners();

            // Make sure dragging is reenabled
            DraggableToken.draggingEnabled = true;

            BeginGame(_situationBuilder);
            _heart.StartBeatingWithDefaultValue();
        }

        /// <summary>
        /// if a game exists, load it; otherwise, create a fresh state and setup
        /// </summary>
        private void BeginGame(SituationBuilder builder) {
            var saveGameManager = new GameSaveManager(new GameDataImporter(Registry.Retrieve<ICompendium>()), new GameDataExporter());

            if (saveGameManager.DoesGameSaveExist() && saveGameManager.IsSavedGameActive()) {
                LoadGame();
            }
            else {
                SetupNewBoard(builder);
                var populatedCharacter = Registry.Retrieve<Character>(); //should just have been set above, but let's keep this clean
                Registry.Retrieve<ICompendium>().ReplaceTokens(populatedCharacter);
            }
        }

        private void InitialiseSubControllers(SpeedController speedController,
                                              HotkeyWatcher hotkeyWatcher,
                                              CardAnimationController cardAnimationController,
                                              MapController mapController,
                                              EndGameAnimController endGameAnimController,
                                              Notifier notifier,
                                              OptionsPanel optionsPanel) {

            speedController.Initialise(_heart);
            hotkeyWatcher.Initialise(_speedController, debugTools, _optionsPanel);
            cardAnimationController.Initialise(_tabletop.GetElementStacksManager());
            mapController.Initialise(mapTokenContainer, mapBackground, mapAnimation);
            endGameAnimController.Initialise();
            notifier.Initialise();
            optionsPanel.InitAudioSettings();
        }

        private void InitialiseListeners() {
            // Init Listeners to pre-existing DisplayHere Objects
            DraggableToken.onChangeDragState += HandleDragStateChanged;
            mapBackground.onDropped += HandleOnMapBackgroundDropped;
        }

        private void OnDestroy() {
            // Static event so make sure to de-init once this object is destroyed
            DraggableToken.onChangeDragState -= HandleDragStateChanged;
            mapBackground.onDropped -= HandleOnMapBackgroundDropped;
        }

        void InitializeTokenContainers() {
            _tabletop.Initialise();
            Limbo.Initialise();
            mapTokenContainer.Initialise();
        }

        private void SetupServices(SituationBuilder builder, TabletopTokenContainer container) {
            var registry = new Registry();
            var compendium = new Compendium();
            var character = new Character();
            var choreographer = new Choreographer(container, builder, tableLevelTransform, windowLevelTransform);
            var situationsCatalogue = new SituationsCatalogue();
            var elementStacksCatalogue = new StackManagersCatalogue();

            //ensure we get updates about stack changes
            _elementOverview.Initialise(elementStacksCatalogue);

            var metaInfo=new MetaInfo(NoonUtility.VersionNumber);
            if(CrossSceneState.GetMetaInfo()==null)
            {
                          //This can happen if we start running the scene in the editor, so it hasn't been set in menu screen
                NoonUtility.Log("Setting meta info in CrossSceneState in Tabletop scene - it hadn't already been set");
                CrossSceneState.SetMetaInfo(metaInfo);
            }

            var draggableHolder = new DraggableHolder(draggableHolderRectTransform);

            registry.Register<ICompendium>(compendium);
            registry.Register<IDraggableHolder>(draggableHolder);
            registry.Register<IDice>(new Dice(debugTools));
            registry.Register<TabletopManager>(this);
            registry.Register<SituationBuilder>(builder);
            registry.Register<INotifier>(_notifier);
            registry.Register<Character>(character);
            registry.Register<Choreographer>(choreographer);
            registry.Register<MapController>(_mapController);
            registry.Register<Limbo>(Limbo);
            registry.Register<SituationsCatalogue>(situationsCatalogue);
            registry.Register<StackManagersCatalogue>(elementStacksCatalogue);
            registry.Register<MetaInfo>(metaInfo);

            var contentImporter = new ContentImporter();
            contentImporter.PopulateCompendium(compendium);
        }

        #endregion

        #region -- Build / Reset -------------------------------

        public void SetupNewBoard(SituationBuilder builder) {
            var chosenLegacy = CrossSceneState.GetChosenLegacy();

            if (chosenLegacy == null) {
                NoonUtility.Log("No initial Legacy specified");
                chosenLegacy = Registry.Retrieve<ICompendium>().GetAllLegacies().First();
            }

            builder.CreateInitialTokensOnTabletop();
            ProvisionStartingElements(chosenLegacy, Registry.Retrieve<Choreographer>());
            SetStartingCharacterInfo(chosenLegacy, CrossSceneState.GetDefunctCharacterName());
            StatusBar.UpdateCharacterDetailsView(Registry.Retrieve<Character>());

            DealStartingDecks();

            _notifier.ShowNotificationWindow(chosenLegacy.Label, chosenLegacy.StartDescription, 30);
        }

        private void SetStartingCharacterInfo(Legacy chosenLegacy, string previousCharacterName) {
            Character newCharacter = Registry.Retrieve<Character>();
            newCharacter.Name = "[click to name]";
            newCharacter.Profession = chosenLegacy.Label;
            newCharacter.PreviousCharacterName = previousCharacterName;
        }

        private void DealStartingDecks() {
            IGameEntityStorage character = Registry.Retrieve<Character>();
            var compendium = Registry.Retrieve<ICompendium>();
            foreach (var ds in compendium.GetAllDeckSpecs()) {
                IDeckInstance di = new DeckInstance(ds);
                character.DeckInstances.Add(di);
                di.Reset();
            }
        }

        public void ProvisionStartingElements(Legacy chosenLegacy, Choreographer choreographer) {
            AspectsDictionary startingElements = new AspectsDictionary();
            startingElements.CombineAspects(chosenLegacy.Effects);  //note: we don't reset the chosen legacy. We assume it remains the same until someone dies again.

            foreach (var e in startingElements) {
                ElementStackToken token = _tabletop.ProvisionElementStack(e.Key, e.Value, Source.Existing()) as ElementStackToken;
                choreographer.ArrangeTokenOnTable(token, new Context(Context.ActionSource.Loading));
            }
        }

        public void ClearGameState(Heart h, IGameEntityStorage s, TabletopTokenContainer tc) {
            h.Clear();
            s.DeckInstances = new List<IDeckInstance>();

            foreach (var sc in Registry.Retrieve<SituationsCatalogue>().GetRegisteredSituations())
                sc.Retire();

            foreach (var element in tc.GetElementStacksManager().GetStacks())
                element.Retire(true); //looks daft but pretty on reset
        }

        public void RestartGame() {
            var saveGameManager = new GameSaveManager(new GameDataImporter(Registry.Retrieve<ICompendium>()), new GameDataExporter());
            saveGameManager.SaveInactiveGame();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void EndGame(Ending ending, SituationController endingSituation) {
            var ls = new LegacySelector(Registry.Retrieve<ICompendium>());
            var saveGameManager = new GameSaveManager(new GameDataImporter(Registry.Retrieve<ICompendium>()), new GameDataExporter());

            CrossSceneState.SetCurrentEnding(ending);
            CrossSceneState.SetDefunctCharacter(Registry.Retrieve<Character>());
            CrossSceneState.SetAvailableLegacies(ls.DetermineLegacies(ending, null));

            //#if !DEBUG
            saveGameManager.SaveInactiveGame();
            //#endif

            string animName;

            if (string.IsNullOrEmpty(ending.Anim))
                animName = "DramaticLight";
            else
                animName = ending.Anim;

            // TODO: Get effect name from ending?
            _endGameAnimController.TriggerEnd((SituationToken)endingSituation.situationToken, animName);
        }

#endregion

#region -- Load / Save GameState -------------------------------

        public void LoadGame() {
            ICompendium compendium = Registry.Retrieve<ICompendium>();
            IGameEntityStorage storage = Registry.Retrieve<Character>();

            _speedController.SetPausedState(true);
            var saveGameManager = new GameSaveManager(new GameDataImporter(compendium), new GameDataExporter());
            //try
            //{
            var htSave = saveGameManager.RetrieveHashedSaveFromFile();
            ClearGameState(_heart, storage, _tabletop);
            saveGameManager.ImportHashedSaveToState(_tabletop, storage, htSave);
            StatusBar.UpdateCharacterDetailsView(storage);
            _notifier.ShowNotificationWindow("Where were we?", " - we have loaded the game.");

            //}
            //catch (Exception e)
            //{
            //    _notifier.ShowNotificationWindow("Couldn't load game - ", e.Message);
            //}
            _speedController.SetPausedState(false);
        }

        public void SaveGame(bool withNotification) {
            if (isInNonSaveableState)
                return;

            _heart.StopBeating();

            //Close all windows and dump tokens to desktop before saving.
            //We don't want or need to track half-started situations.
            var allSituationControllers = Registry.Retrieve<SituationsCatalogue>().GetRegisteredSituations();
            foreach (var s in allSituationControllers)
                s.CloseWindow();

            // try
            //  {
            var saveGameManager = new GameSaveManager(new GameDataImporter(Registry.Retrieve<ICompendium>()), new GameDataExporter());
            saveGameManager.SaveActiveGame(_tabletop, Registry.Retrieve<Character>());
            if (withNotification)
                _notifier.ShowNotificationWindow("SAVED THE GAME", "BUT NOT THE WORLD");

            //}
            //catch (Exception e)
            //{

            //      _notifier.ShowNotificationWindow("Couldn't save game - ", e.Message); ;
            // }

            _heart.ResumeBeating();
        }

#endregion

#region -- Greedy Grabbing -------------------------------

        public HashSet<TokenAndSlot> FillTheseSlotsWithFreeStacks(HashSet<TokenAndSlot> slotsToFill) {
            var unprocessedSlots = new HashSet<TokenAndSlot>();
            var choreo = Registry.Retrieve<Choreographer>();
            SituationController sit;

            foreach (var tokenSlotPair in slotsToFill) {
                if (NeedToFillSlot(tokenSlotPair) == false) 
                    continue; // Skip it, we don't need to fill it

                var stack = FindStackForSlotSpecificationOnTabletop(tokenSlotPair.RecipeSlot.GoverningSlotSpecification) as ElementStackToken;

                if (stack != null) {
                    stack.SplitAllButNCardsToNewStack(1, new Context(Context.ActionSource.GreedySlot));
                    choreo.MoveElementToSituationSlot(stack, tokenSlotPair);
                    continue; // we found a stack, we're done here
                }

                stack = FindStackForSlotSpecificationInSituations(tokenSlotPair.RecipeSlot.GoverningSlotSpecification, out sit) as ElementStackToken;

                if (stack != null) {
                    stack.SplitAllButNCardsToNewStack(1, new Context(Context.ActionSource.GreedySlot));
                    choreo.PrepareElementForGreedyAnim(stack, sit.situationToken as SituationToken); // this reparents the card so it can animate properly
                    choreo.MoveElementToSituationSlot(stack, tokenSlotPair);
                    continue; // we found a stack, we're done here
                }
                
                unprocessedSlots.Add(tokenSlotPair);
            }

            return unprocessedSlots;
        }

        private bool NeedToFillSlot(TokenAndSlot tokenSlotPair) {
            if (tokenSlotPair.Token.Equals(null))
                return false; // It has been destroyed
            if (tokenSlotPair.Token.Defunct)
                return false;
            if (!tokenSlotPair.Token.SituationController.IsOngoing)
                return false;
            if (tokenSlotPair.RecipeSlot.Equals(null))
                return false; // It has been destroyed
            if (tokenSlotPair.RecipeSlot.Defunct)
                return false;
            if (tokenSlotPair.RecipeSlot.IsBeingAnimated)
                return false; // We're animating something into the slot.
            if (tokenSlotPair.RecipeSlot.GetElementStackInSlot() != null)
                return false; // It is already filled

            return true;
        }

        private IElementStack FindStackForSlotSpecificationOnTabletop(SlotSpecification slotSpec) {

    
            var stacks = _tabletop.GetElementStacksManager().GetStacks();

            foreach (var stack in stacks)
                if (CanPullCardToGreedySlot(stack as ElementStackToken, slotSpec))
                    return stack;
    
            return null;
        }

        private bool CanPullCardToGreedySlot(ElementStackToken stack, SlotSpecification slotSpec) {
            if (stack.Defunct)
                return false; // don't pull defunct cards
            else if (stack.IsBeingAnimated)
                return false; // don't pull animated cards
            else if (DraggableToken.itemBeingDragged == stack)
                return false; // don't pull cards being dragged

            return slotSpec.GetSlotMatchForAspects(stack.GetAspects()).MatchType == SlotMatchForAspectsType.Okay;
        }

        private IElementStack FindStackForSlotSpecificationInSituations(SlotSpecification slotSpec, out SituationController sit) {
            // Nothing on the table? Look at the Situations.
            var situationControllers = Registry.Retrieve<SituationsCatalogue>().GetRegisteredSituations();

            // We grab output first
            foreach (var controller in situationControllers) {
                foreach (var stack in controller.GetOutputStacks()) {
                    if (CanPullCardToGreedySlot(stack as ElementStackToken, slotSpec)) {
                        sit = controller;
                        return stack;
                    }
                }
            }

            // Nothing? Then We grab starting
            foreach (var controller in situationControllers) {
                foreach (var stack in controller.GetStartingStacks()) {
                    if (CanPullCardToGreedySlot(stack as ElementStackToken, slotSpec)) {
                        sit = controller;
                        return stack;
                    }
                }
            }

            // Nothing? Then we grab ongoing
            foreach (var controller in situationControllers) {
                foreach (var slot in controller.GetOngoingSlots()) {
                    if (slot.IsGreedy)
                        continue; // Greedy? Don't grab.

                    var stack = slot.GetElementStackInSlot();

                    if (stack == null)
                        continue; // Empty? Nothing to grab either

                    if (CanPullCardToGreedySlot(stack as ElementStackToken, slotSpec)) {
                        sit = controller;
                        return stack;
                    }
                }
            }

            sit = null;
            return null;
        }

#endregion

        public void CloseAllSituationWindowsExcept(string exceptTokenId) {
            var situationControllers = Registry.Retrieve<SituationsCatalogue>().GetRegisteredSituations();

            foreach (var controller in situationControllers) {
                if (controller.GetTokenId() != exceptTokenId)
                    controller.CloseWindow();
            }
        }

        void HandleOnMapBackgroundDropped() {
            if (DraggableToken.itemBeingDragged != null) {

                DraggableToken.SetReturn(false, "dropped on the map background");
                DraggableToken.itemBeingDragged.DisplayAtTableLevel();
                mapTokenContainer.DisplayHere(DraggableToken.itemBeingDragged, new Context(Context.ActionSource.PlayerDrag));

                SoundManager.PlaySfx("CardDrop");
            }
        }

        public void DecayStacksOnTable(float interval) {
            var decayingStacks = _tabletop.GetElementStacksManager().GetStacks().Where(s => s.Decays);
            foreach (var d in decayingStacks)
                d.Decay(interval);
        }

        private void HandleDragStateChanged(bool isDragging) {
            // not dragging a stack? then do nothing. _tabletop was destroyed (end of game?)
            if (_tabletop == null)
                return;

            var draggedElement = DraggableToken.itemBeingDragged as ElementStackToken;

            if (mapTokenContainer != null)
                mapTokenContainer.ShowDestinationsForStack(draggedElement, isDragging);
        }

        public void SetPausedState(bool paused) {
            _speedController.SetPausedState(paused);
        }

        void LockSpeedController(bool enabled) {
            _speedController.LockToPause(enabled);
        }

        public void ShowMansusMap(SituationController situation, Transform origin, PortalEffect effect) {
            CloseAllSituationWindowsExcept(null);

            DraggableToken.CancelDrag();
            LockSpeedController(true);
            isInNonSaveableState = true;

            // Play Mansus Music
            backgroundMusic.PlayMansusClip();

            // Build mansus cards and doors everything
            mansusSituation = situation; // so we can drop the card in the right place
            _mapController.SetupMap(effect); 

            // Do transition
            _tabletop.Show(false);
            _mapController.ShowMansusMap(origin, true);
        }

        public void HideMansusMap(Transform origin, ElementStackToken mansusCard) {
            DraggableToken.CancelDrag();
            LockSpeedController(false);
            isInNonSaveableState = false;

            // Play Normal Music
            backgroundMusic.PlayRandomClip();

            // Cleanup mansus cards and doors everything
            _mapController.CleanupMap();

            // Do transition
            _tabletop.Show(true);
            _mapController.ShowMansusMap(origin, false);

            // Put card into the original Situation Results
            mansusSituation.AddToResults(mansusCard, new Context(Context.ActionSource.PlayerDrag));
            mansusSituation = null;

            // Add message to the situation notes
            // center on origin token
        }

        public void BeginNewSituation(SituationCreationCommand scc) {
            Registry.Retrieve<Choreographer>().BeginNewSituation(scc);
        }

        public void SignalImpendingDoom(ISituationAnchor situationToken) {
            backgroundMusic.PlayImpendingDoom();
        }


    }

}
