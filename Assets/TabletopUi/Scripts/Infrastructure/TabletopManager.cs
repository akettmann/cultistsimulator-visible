﻿#pragma warning disable 0649

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assets.Core;
using Assets.Core.Commands;
using Assets.Core.Entities;
using Assets.Core.Enums;
using Assets.Core.Fucine;
using Assets.Core.Interfaces;
using Assets.Core.Services;
using Assets.Logic;
using Assets.TabletopUi;
using Assets.TabletopUi.Scripts.Infrastructure;
using Assets.TabletopUi.Scripts.Infrastructure.Modding;
using Assets.TabletopUi.Scripts.Interfaces;
using Assets.TabletopUi.Scripts.Services;
using Assets.TabletopUi.Scripts.UI;
using Assets.TabletopUi.UI;
using Noon;
using TabletopUi.Scripts.Elements;
using UIWidgets.Examples.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace Assets.CS.TabletopUI {
    public class TabletopManager : MonoBehaviour,IStacksChangeSubscriber {

        [Header("Game Control")]
        [SerializeField]
        private Heart _heart;
        [SerializeField]
        private SpeedController _speedController;
        [SerializeField]
        private IntermittentAnimatableController _intermittentAnimatableController;
        [SerializeField]
        private EndGameAnimController _endGameAnimController;

        [Header("Tabletop")]
        [SerializeField]
        public TabletopTokenContainer _tabletop;
        [SerializeField]
        TabletopBackground tabletopBackground;

        [SerializeField] private HighlightLocationsController _highlightLocationsController;

        [SerializeField]
        private Limbo Limbo;

        [Header("Detail Windows")]
        [SerializeField]
        private AspectDetailsWindow aspectDetailsWindow;
        [SerializeField]
        private TokenDetailsWindow tokenDetailsWindow;
        [SerializeField] 
        private CardHoverDetail cardHoverDetail;

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
        [SerializeField]
		private ScrollRect tableScroll;
		[SerializeField]
		public GameObject _dropZoneTemplate;


        [Header("Options Bar & Notes")]
        [SerializeField]
        private StatusBar StatusBar;

        [SerializeField]
        private DebugTools debugTools;
        [SerializeField]
        private BackgroundMusic backgroundMusic;

        [SerializeField]
        private Notifier _notifier;
		[SerializeField]
        private AutosaveWindow _autosaveNotifier;
        [SerializeField]
        private OptionsPanel _optionsPanel;
        [SerializeField]
        private ElementOverview _elementOverview;

        private SituationBuilder _situationBuilder;

        private bool disabled;
        private bool _initialised;

		// Internal cache - if ENABLE_ASPECT_CACHING disabled, if still uses these but recalcs every frame
		[NonSerialized]
		public bool					_enableAspectCaching = true;

		private AspectsDictionary	_tabletopAspects = null;
		private AspectsDictionary	_allAspectsExtant = null;
		private bool				_tabletopAspectsDirty = true;
		private bool				_allAspectsExtantDirty = true;

		public void NotifyAspectsDirty()
		{
			_tabletopAspectsDirty = true;
		}

		public enum NonSaveableType
		{
			Drag,		// Cannot save because held card gets lost
			Mansus,		// Cannot save by design
			Greedy,		// Cannot save during Magnet grab (spec fix for #1253)
			WindowAnim,	// Cannot save during situation window open
			NumNonSaveableTypes
		};
        static private bool[] isInNonSaveableState = new bool[(int)NonSaveableType.NumNonSaveableTypes];
        private SituationController mansusSituation;
		//private Vector2 preMansusTabletopPos; // Disabled cause it looks jerky -Martin

		public static bool IsInMansus()
		{
			return isInNonSaveableState[(int)NonSaveableType.Mansus];
		}

		private float housekeepingTimer = 0.0f;	// Now a float so that we can time autosaves independent of Heart.Beat - CP
		private float AUTOSAVE_INTERVAL = 300.0f;
		private static float gridSnapSize = 0.0f;
		private static bool highContrastMode = false;
		private static bool accessibleCards = false;
		private static bool stickyDragMode = false;
        private List<string> currentDoomTokens=new List<string>();

		public void ForceAutosave()	// Useful for forcing autosave to happen at tricky moments for debugging - CP
		{
			housekeepingTimer = AUTOSAVE_INTERVAL;
		}

		public void ToggleLog()
		{
			NoonUtility.ToggleLog();
		}

		public bool IsPaused()
		{
			return _heart.IsPaused;
		}

        public async void Update()
        {
            if (disabled)
                return; //we've had to shut down because of a critical error

            if (!_initialised)
                return; //still setting up

            _intermittentAnimatableController.CheckForCardAnimations();

			if (_heart.IsPaused)
			{
				_heart.AdvanceTime( 0.0f );		// If the game is now calling Heart.Beat, we still need to update cosmetic stuff like Decay timers
			}

			// Failsafe to ensure that NonSaveableType.Drag never gets left on due to unusual exits from drag state - CP
			if (DraggableToken.itemBeingDragged == null)
				TabletopManager.RequestNonSaveableState( TabletopManager.NonSaveableType.Drag, false );

			housekeepingTimer += Time.deltaTime;
			if (housekeepingTimer >= AUTOSAVE_INTERVAL && IsSafeToAutosave())	// Hold off autsave until it's safe, rather than waiting for the next autosave - CP
			{
				housekeepingTimer = 0.0f;

                var saveTask = SaveGameAsync(true, SourceForGameState.DefaultSave);
                var success = await saveTask;

                if(!success)
                housekeepingTimer = AUTOSAVE_INTERVAL - 5.0f;

			}
        }


        private void AppealToConscience()
        {
            string appealToConscienceLocation = Application.streamingAssetsPath + "/edition/please_buy_our_game.txt";
            if (File.Exists(appealToConscienceLocation))
            {
                var content = File.ReadLines(appealToConscienceLocation);
                DateTime expiry = Convert.ToDateTime(content.First());
                if (DateTime.Today > expiry)
                {
                    _notifier.ShowNotificationWindow("ERROR - PLEASE UPDATE GAME", @"CRITICAL UPDATE REQUIRED");
                    return;
                }
            }
        }


        void Start()
		{
            //AppealToConscience();
            
         Configuration.Setup();

            _situationBuilder = new SituationBuilder(tableLevelTransform, windowLevelTransform, _heart);

            var registry=new Registry();

           
                //register everything used gamewide
                SetupServices(registry,_situationBuilder, _tabletop);

                // This ensures that we have an ElementStackManager in Limbo & Tabletop
                InitializeTokenContainers();

                //we hand off board functions to individual controllers
                InitialiseSubControllers(
                    _speedController,
                    _intermittentAnimatableController,
                    _mapController,
                    _endGameAnimController,
                    _optionsPanel
                );

            InitialiseListeners();

            // Make sure dragging is reenabled
            DraggableToken.draggingEnabled = true;

            _initialised = true;

            
            if (Registry.Get<StageHand>().SourceForGameState==SourceForGameState.NewGame)
            {
                BeginNewGame(_situationBuilder);
            }
            else
            {
                LoadExistingGame(Registry.Get<StageHand>().SourceForGameState);
            }


        }

        /// <summary>
        /// if a game exists, load it; otherwise, create a fresh state and setup
        /// </summary>
        private void LoadExistingGame(SourceForGameState source)
		{

            bool shouldStartPaused = true;


            var saveGameManager = new GameSaveManager(new GameDataImporter(Registry.Get<ICompendium>()), new GameDataExporter());
                bool isSaveCorrupted = false;
                bool shouldContinueGame;
                try
                {
	                shouldContinueGame = saveGameManager.DoesGameSaveExist() && saveGameManager.IsSavedGameActive(source);
                }
                catch (Exception e)
                {
	                Debug.LogError("Failed to load game (see exception for details)");
	                Debug.LogException(e);
	                shouldContinueGame = false;
	                isSaveCorrupted = true;
                }

                LoadGame(source);

     

            if (!shouldContinueGame || isSaveCorrupted)
            {
                _notifier.ShowSaveError(true);
                GameSaveManager.saveErrorWarningTriggered = true;
            }
            
            _heart.StartBeatingWithDefaultValue();								// Init heartbeat duration...
            _speedController.SetPausedState(shouldStartPaused, false, true);	// ...but (optionally) pause game while the player gets their bearings.
            _elementOverview.UpdateDisplay(); //show initial correct count of everything we've just loaded
		}

        private void BeginNewGame(SituationBuilder builder)
        {
            SetupNewBoard(builder);
            var populatedCharacter =
                Registry.Get<Character>(); //should just have been set above, but let's keep this clean
            populatedCharacter.Reset(populatedCharacter.ActiveLegacy,null);
            Registry.Get<ICompendium>().SupplyLevers(populatedCharacter);
     Registry.Get<StageHand>().ClearRestartingGameFlag();
        }

        private void InitialiseSubControllers(SpeedController speedController,
            IntermittentAnimatableController intermittentAnimatableController,
                                              MapController mapController,
                                              EndGameAnimController endGameAnimController,
            OptionsPanel optionsPanel) {

            speedController.Initialise(_heart);

            intermittentAnimatableController.Initialise(_tabletop.GetElementStacksManager(),Registry.Get<SituationsCatalogue>());
            mapController.Initialise(mapTokenContainer, mapBackground, mapAnimation);
            endGameAnimController.Initialise();

            optionsPanel.Initialise(_speedController,true);
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



        private void SetupServices(Registry registry,SituationBuilder builder, TabletopTokenContainer container)
        {


            ICompendium compendium = Registry.Get<ICompendium>();


         
            //if (CrossSceneState.GetChosenLegacy() != null)
            //    character = new Character(CrossSceneState.GetChosenLegacy(), CrossSceneState.GetDefunctCharacter());
            //else
            //    character = new Character(compendium.GetEntitiesAsList<Legacy>().First());


            var choreographer = new Choreographer(container, builder, tableLevelTransform, windowLevelTransform);
            var chronicler = new Chronicler(Registry.Get<Character>(),compendium);

            var situationsCatalogue = new SituationsCatalogue();
            var stackManagersCatalogue = new StackManagersCatalogue();
            stackManagersCatalogue.Subscribe(this);

            var metaInfo = new MetaInfo(new VersionNumber(Application.version));
            //if(CrossSceneState.GetMetaInfo()==null)
            //{
            //              //We've stated running the scene in the editor, so it hasn't been set in menu screen
            //    NoonUtility.Log("Setting meta info in CrossSceneState in Tabletop scene - it hadn't already been set",0,VerbosityLevel.SystemChatter);
            //    CrossSceneState.SetMetaInfo(metaInfo);
            //        //also the graphics level keeps defaulting to lowest when I run the game in the editor, because it hasn't seen options in the menu
            //   Configuration.SetGraphicsLevel(3);
            //}

            var draggableHolder = new DraggableHolder(draggableHolderRectTransform);
            var character = Registry.Get<Character>();

            

            registry.Register<IDraggableHolder>(draggableHolder);
            registry.Register<IDice>(new Dice(debugTools));
            registry.Register<TabletopManager>(this);
            registry.Register<SituationBuilder>(builder);
            registry.Register<INotifier>(_notifier);

            registry.Register<Choreographer>(choreographer);
            registry.Register<Chronicler>(chronicler);
            registry.Register<MapController>(_mapController);
            registry.Register<Limbo>(Limbo);
            registry.Register<SituationsCatalogue>(situationsCatalogue);
            registry.Register<StackManagersCatalogue>(stackManagersCatalogue);
            registry.Register<MetaInfo>(metaInfo);

			registry.Register<DebugTools>(debugTools);
            registry.Register<HighlightLocationsController>(_highlightLocationsController);

            _highlightLocationsController.Initialise(stackManagersCatalogue);


            //element overview needs to be initialised with
            // - legacy - in case we're displaying unusual info
            // stacks catalogue - so it can subscribe for notifications re changes
            _elementOverview.Initialise(character.ActiveLegacy, stackManagersCatalogue,compendium);
            tabletopBackground.ShowTabletopFor(character.ActiveLegacy);


        }

 

        #region -- Build / Reset -------------------------------

        public void SetupNewBoard(SituationBuilder builder) {


     
            Character _character = Registry.Get<Character>();
            if(_character.ActiveLegacy==null)
                throw new ApplicationException("Trying to set up a new board for a character with no chosen legacy. Even fresh characters should have a legacy when created, but this code has always been hinky.");

            builder.CreateInitialTokensOnTabletop(_character.ActiveLegacy);

            ProvisionStartingElements(_character.ActiveLegacy, Registry.Get<Choreographer>());
            SetStartingCharacterInfo(_character.ActiveLegacy);
            StatusBar.UpdateCharacterDetailsView(Registry.Get<Character>());

            DealStartingDecks();

            _notifier.ShowNotificationWindow(_character.ActiveLegacy.Label, _character.ActiveLegacy.StartDescription);
        }

        private void SetStartingCharacterInfo(Legacy chosenLegacy)
		{
            Character newCharacter = Registry.Get<Character>();
            newCharacter.Name = Registry.Get<ILanguageManager>().Get("UI_CLICK_TO_NAME");
           // Registry.Retrieve<Chronicler>().CharacterNameChanged(NoonConstants.DEFAULT_CHARACTER_NAME);//so we never see a 'click to rename' in future history
            newCharacter.Profession = chosenLegacy.Label;
        }

        private void DealStartingDecks() {
            Character character = Registry.Get<Character>();
            var compendium = Registry.Get<ICompendium>();
            foreach (var ds in compendium.GetEntitiesAsList<DeckSpec>()) {
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


        public void PurgeElement(string elementId, int maxToPurge)
        {
            var compendium = Registry.Get<ICompendium>();

            Element purgedElement = compendium.GetEntityById<Element>(elementId);
            //I don't think MaxToPurge is being usefully decremented here - should return int

           _tabletop.GetElementStacksManager().PurgeElement(purgedElement, maxToPurge);

           var situationsCatalogue = Registry.Get<SituationsCatalogue>();
           foreach (var s in situationsCatalogue.GetRegisteredSituations())
           {

               if (s.SituationClock.State == SituationState.Unstarted)
               {
                   var slotsToTryPurge = new List<RecipeSlot>(s.situationWindow.GetStartingSlots());

                   slotsToTryPurge.Reverse();
                   foreach (var slot in slotsToTryPurge)
                       slot.TryPurgeElement(purgedElement, maxToPurge);
               }
               //If the situation has finished, purge any matching elements in the results.
                else if (s.SituationClock.State==SituationState.Complete)
                { 
                   s.situationWindow.GetResultsStacksManager()
                       .PurgeElement(purgedElement, maxToPurge);

                }
                else
                 {
                   //if the situation is still ongoing, any elements actually inside it are protected. However, elements in the slot are not protected.
                   s.situationWindow.GetOngoingSlots().FirstOrDefault()
                       ?.TryPurgeElement(purgedElement, maxToPurge);
                 }
           }
        }

        public void HaltVerb(string toHaltId, int maxToHalt)
        {
            var situationsCatalogue = Registry.Get<SituationsCatalogue>();
            int i = 0;
            //Halt the verb if the actionId matches BEARING IN MIND WILDCARD

            if (toHaltId.Contains('*'))
            {
                string wildcardToDelete = toHaltId.Remove(toHaltId.IndexOf('*'));

                foreach (var s in situationsCatalogue.GetRegisteredSituations())
                {
                    if (s.GetTokenId().StartsWith(wildcardToDelete))
                    {
                        s.Halt();
                        i++;
                    }

                    if (i >= maxToHalt)
                        break;
                }
            }

            else
            {
                foreach (var s in situationsCatalogue.GetRegisteredSituations())
                {
                    if (s.GetTokenId() == toHaltId.Trim())
                    {
                        s.Halt();
                        i++;
                    }
                    if (i >= maxToHalt)
                        break;
                }
            }
        }

        public void DeleteVerb(string toDeleteId, int maxToDelete)
        {
            var situationsCatalogue = Registry.Get<SituationsCatalogue>();
            int i = 0;
            //Delete the verb if the actionId matches BEARING IN MIND WILDCARD

            if (toDeleteId.Contains('*'))
            {
                string wildcardToDelete = toDeleteId.Remove(toDeleteId.IndexOf('*'));

                foreach (var s in situationsCatalogue.GetRegisteredSituations())
                {
                    if (s.GetTokenId().StartsWith(wildcardToDelete))
                    {
                        s.Retire();
                        i++;
                    }

                    if (i >= maxToDelete)
                        break;
                }
            }

            else
                {
                    foreach (var s in situationsCatalogue.GetRegisteredSituations())
                    {
                 if (s.GetTokenId() == toDeleteId.Trim())
                        {
                            s.Retire();
                            i++;
                        }
                        if (i >= maxToDelete)
                            break;
                    }
                }
        }


        public async void EndGame(Ending ending, SituationController endingSituation)
		{
			NoonUtility.Log("TabletopManager.EndGame()");

            var saveGameManager = new GameSaveManager(new GameDataImporter(Registry.Get<ICompendium>()), new GameDataExporter());

            var character = Registry.Get<Character>();
            var chronicler = Registry.Get<Chronicler>();

            chronicler.ChronicleGameEnd(Registry.Get<SituationsCatalogue>().GetRegisteredSituations(), Registry.Get<StackManagersCatalogue>().GetRegisteredStackManagers(),ending);
            character.Reset(null,ending);
            


            var saveTask = saveGameManager.SaveActiveGameAsync(new InactiveTableSaveState(), Registry.Get<Character>(),SourceForGameState.DefaultSave);
            var result = await saveTask;

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

        public void LoadGame(SourceForGameState gameStateSource) {
            ICompendium compendium = Registry.Get<ICompendium>();
            
            _speedController.SetPausedState(true, false, true);
            var saveGameManager = new GameSaveManager(new GameDataImporter(compendium), new GameDataExporter());
            try
            {
	            //var htSave = saveGameManager.RetrieveHashedSaveFromFile(index);
	        //    ClearGameState(_heart, character, _tabletop);
            var registry=new Registry();
           registry.Register(saveGameManager.LoadCharacterState(gameStateSource));
            saveGameManager.LoadTabletopState(_tabletop,gameStateSource);
                //saveGameManager.ImportHashedSaveToState(_tabletop, null, htSave);

                StatusBar.UpdateCharacterDetailsView(Registry.Get<Character>());

				// Reopen any windows that were open at time of saving. I think there can only be one, but checking all for robustness - CP
				var allSituationControllers = Registry.Get<SituationsCatalogue>().GetRegisteredSituations();
	            foreach (var s in allSituationControllers)
				{
					if (s.IsOpen)
					{
						Vector3 tgtPos = s.RestoreWindowPosition;
		                s.OpenWindow( tgtPos );
					}
				}

	            _notifier.ShowNotificationWindow(Registry.Get<ILanguageManager>().Get("UI_LOADEDTITLE"), Registry.Get<ILanguageManager>().Get("UI_LOADEDDESC"));
            }
            catch (Exception e)
            {
                _notifier.ShowNotificationWindow(Registry.Get<ILanguageManager>().Get("UI_LOADFAILEDTITLE"), Registry.Get<ILanguageManager>().Get("UI_LOADFAILEDDESC"));
                Debug.LogError("Failed to load game (see exception for details)");
                Debug.LogException(e, this);
            }
            _speedController.SetPausedState(true, false, true);

            var activeLegacy = Registry.Get<Character>().ActiveLegacy;

            _elementOverview.Initialise(activeLegacy, Registry.Get<StackManagersCatalogue>(), compendium);
            tabletopBackground.ShowTabletopFor(activeLegacy);

        }

        public async Task<bool> SaveGameAsync(bool withNotification, SourceForGameState source)
		{
			if (!IsSafeToAutosave())
            {
                return false;
            }

			bool success = true;	// Assume everything will be OK to begin with...

			// Check state so that autosave behaves correctly if called while paused - CP
			bool wasBeating = false;
			if (!_heart.IsPaused)
			{
		        _heart.StopBeating();
				wasBeating = true;
			}

			
            try
            {
	            var saveGameManager = new GameSaveManager(new GameDataImporter(Registry.Get<ICompendium>()), new GameDataExporter());

                ITableSaveState tableSaveState=new TableSaveState(_tabletop.GetElementStacksManager().GetStacks(), Registry.Get<SituationsCatalogue>().GetRegisteredSituations());
                 var   saveTask = saveGameManager.SaveActiveGameAsync(tableSaveState,  Registry.Get<Character>(), source);

                 success = await saveTask;

            }
            catch (Exception e)
            {
	            success = false;
	            _notifier.ShowSaveError(true);
	            GameSaveManager.saveErrorWarningTriggered = true;
	            Debug.LogError("Failed to save game (see exception for details)");
	            Debug.LogException(e);
            }

            if (wasBeating && _heart!=null)
            {
	            _heart.ResumeBeating();
            }
            

            if (success && withNotification && _autosaveNotifier!=null)
			{
				//_notifier.ShowNotificationWindow("SAVED THE GAME", "BUT NOT THE WORLD");
				_autosaveNotifier.SetDuration( 3.0f );
				_autosaveNotifier.Show();
			}

			if (GameSaveManager.saveErrorWarningTriggered)	// Do a full pause after resuming heartbeat (to update UI, SFX, etc)
			{
				bool pauseStateWhenErrorRequested = GetPausedState();
				if (!pauseStateWhenErrorRequested)			// only pause if we need to (since it triggers sfx)
					SetPausedState(true);
				GameSaveManager.saveErrorWarningTriggered = false;	// Clear after we've used it
			}

            return true;
        }

#endregion

#region -- Greedy Grabbing -------------------------------

        public HashSet<TokenAndSlot> FillTheseSlotsWithFreeStacks(HashSet<TokenAndSlot> slotsToFill) {
            var unprocessedSlots = new HashSet<TokenAndSlot>();
            var choreo = Registry.Get<Choreographer>();
            SituationController sit;

            foreach (var tokenSlotPair in slotsToFill) {
                if (NeedToFillSlot(tokenSlotPair) == false)
                    continue; // Skip it, we don't need to fill it

                var stack = FindStackForSlotSpecificationOnTabletop(tokenSlotPair.RecipeSlot.GoverningSlotSpecification) as ElementStackToken;

                if (stack != null) {
                    stack.SplitAllButNCardsToNewStack(1, new Context(Context.ActionSource.GreedySlot));
                    choreo.MoveElementToSituationSlot(stack, tokenSlotPair, choreo.ElementGreedyAnimDone);
                    continue; // we found a stack, we're done here
                }

                stack = FindStackForSlotSpecificationInSituations(tokenSlotPair.RecipeSlot.GoverningSlotSpecification, out sit) as ElementStackToken;

                if (stack != null) {
                    stack.SplitAllButNCardsToNewStack(1, new Context(Context.ActionSource.GreedySlot));
                    choreo.PrepareElementForGreedyAnim(stack, sit.situationToken as SituationToken); // this reparents the card so it can animate properly
                    choreo.MoveElementToSituationSlot(stack, tokenSlotPair, choreo.ElementGreedyAnimDone);
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
            if (tokenSlotPair.RecipeSlot.GoverningSlotSpecification==null || !tokenSlotPair.RecipeSlot.GoverningSlotSpecification.Greedy)
                return false; //it's not greedy any more; sometimes if we have a recipe with a greedy slot followed by a recipe with a non-greedy slot, the behaviour carries over for the moment the recipe changes

            return true;
        }

        private IElementStack FindStackForSlotSpecificationOnTabletop(SlotSpecification slotSpec) {

            var rnd = new Random();
            var stacks = _tabletop.GetElementStacksManager().GetStacks().OrderBy(x=>rnd.Next());

            foreach (var stack in stacks)
                if (CanPullCardToGreedySlot(stack as ElementStackToken, slotSpec))
                {
#pragma warning disable CS0253 // Possible unintended reference comparison; right hand side needs cast
                    if (DraggableToken.itemBeingDragged == stack)
#pragma warning restore CS0253 // Possible unintended reference comparison; right hand side needs cast
                    {
                        DraggableToken.SetReturn(false,"Drag aborted by greedy slot"); DraggableToken.itemBeingDragged = null;
                    }
                
                    return stack;
                }

            
            return null;
        }

        private bool CanPullCardToGreedySlot(ElementStackToken stack, SlotSpecification slotSpec)
        {
            if (slotSpec == null)
                return false; //We were seeing NullReferenceExceptions in the Unity analytics from the bottom line; stack is referenced okay so it shouldn't be stack, so probably a null slotspec is being specified somewhere

            if (stack == null) //..but just in case.
                return false;

            if (stack.Defunct)
                return false; // don't pull defunct cards
            else if (stack.IsBeingAnimated)
                return false; // don't pull animated cards

            if (PlayerPrefs.HasKey(NoonConstants.BIRDWORMSLIDER))
            {
                var allowExploits = PlayerPrefs.GetInt(NoonConstants.BIRDWORMSLIDER);
                if (allowExploits > 0)
                {
                    if (DraggableToken.itemBeingDragged == stack)
                        return false; // don't pull cards being dragged if Worm is set On}
                }
            }

            return slotSpec.GetSlotMatchForAspects(stack.GetAspects()).MatchType == SlotMatchForAspectsType.Okay;
        }

        private IElementStack FindStackForSlotSpecificationInSituations(SlotSpecification slotSpec, out SituationController sit) {
            var rnd = new Random();

            // Nothing on the table? Look at the Situations.
            var situationControllers = Registry.Get<SituationsCatalogue>().GetRegisteredSituations();

            // We grab output first
            foreach (var controller in situationControllers) {
                foreach (var stack in controller.GetOutputStacks().OrderBy(x=>rnd.Next())) {
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
            foreach (var controller in situationControllers.OrderBy(x => rnd.Next())) {
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

		public void CloseAllDetailsWindows()
		{
			if (aspectDetailsWindow != null)
				aspectDetailsWindow.Hide();
			if (tokenDetailsWindow != null)
				tokenDetailsWindow.Hide();
		}

        public void CloseAllSituationWindowsExcept(string exceptTokenId) {
            var situationControllers = Registry.Get<SituationsCatalogue>().GetRegisteredSituations();

            foreach (var controller in situationControllers) {
                if (controller.GetTokenId() != exceptTokenId)
                    controller.CloseWindow();
            }
        }

        public bool IsSituationWindowOpen() {
	        var situationControllers = Registry.Get<SituationsCatalogue>().GetRegisteredSituations();
	        return situationControllers.Any(c => c.IsOpen);
        }

        public void SetHighlightedElement(string elementId, int quantity = 1)
        {
	        if (!GetAccessibleCards())
		        return;
	        if (elementId == null || elementId == "dropzone")
	        {
		        cardHoverDetail.Hide();
		        return;
	        }
	        cardHoverDetail.Populate(elementId, quantity);
	        cardHoverDetail.Show();
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


        public void DecayStacksInResults(float interval)
        {
            var situationControllers = Registry.Get<SituationsCatalogue>().GetRegisteredSituations();

            foreach (var s in situationControllers)
            {
                s.TryDecayContents(interval);
            }

            //foreach (var d in decayingStacks)
              //  d.Decay(interval);
        }

        private void HandleDragStateChanged(bool isDragging) {
            // not dragging a stack? then do nothing. _tabletop was destroyed (end of game?)
            if (_tabletop == null)
                return;

            var draggedElement = DraggableToken.itemBeingDragged as ElementStackToken;

            if (mapTokenContainer != null)
                mapTokenContainer.ShowDestinationsForStack(draggedElement, isDragging);
        }

		static public void RequestNonSaveableState( NonSaveableType type, bool forbidden )
		{
			// This allows multiple systems to request overlapping NonSaveableStates - CP
			// Removed the counter, as it kept creeping up (must be a loophole if a drag is aborted oddly)
			// For safety I've changed it to array of separate flags (so you can drag in the Mansus without enabled autosave)
			// and added a failsafe in the update, which flushes the Drag flag whenever nothing is held (rather than relying on catching all exit points)
			Debug.Assert( type<NonSaveableType.NumNonSaveableTypes, "Bad nonsaveable type" );
			isInNonSaveableState[(int)type] = forbidden;
		}

		static public void FlushNonSaveableState()	// For use when we absolutely, definitely want to restore autosave permission - CP
		{
			for (int i=0; i<(int)NonSaveableType.NumNonSaveableTypes; i++)
			{
				isInNonSaveableState[i] = false;
			}
		}

		static public bool IsSafeToAutosave()
		{
			for (int i=0; i<(int)NonSaveableType.NumNonSaveableTypes; i++)
			{
				if (isInNonSaveableState[i])
					return false;
			}
			return true;
		}

        public void SetPausedState(bool paused) {
            _speedController.SetPausedState(paused);
        }

		public bool GetPausedState() {
            return _speedController.GetPausedState();
        }

        void LockSpeedController(bool enabled) {
            _speedController.LockToPause(enabled);
        }

		public void SetAutosaveInterval( float minutes )
		{
			AUTOSAVE_INTERVAL = minutes * 60.0f;
		}


		public void SetGridSnapSize( float snapsize )
		{
			int snap = Mathf.RoundToInt( snapsize );
			switch (snap)
			{
			default:
			case 0:		gridSnapSize = 0.0f; break;
			case 1:		gridSnapSize = 1.0f; break;		// 1 card
			case 2:		gridSnapSize = 0.5f; break;		// ½ card
			case 3:		gridSnapSize = 0.25f; break;	// ¼ card
			}
		}

		public static float GetGridSnapSize()
		{
			return gridSnapSize;
		}


		public static void SetHighContrast( bool on )
		{
			highContrastMode = on;
   //         Registry.Retrieve<Concursum>().CultureChangedEvent.Invoke();
			//LanguageManager.LanguageChangeHasOccurred();	// Fire language change to recreate all text, which will also apply contrast adjustments - CP 
		}

		public static bool GetHighContrast()
		{
			return highContrastMode;
		}

		public static void SetAccessibleCards( bool on )
		{
			accessibleCards = on;
		}





        public static bool GetAccessibleCards()
		{
			return accessibleCards;
		}

		public static void SetStickyDrag( bool on )
		{
			stickyDragMode = on;
		}

		public static bool GetStickyDrag()
		{
			return stickyDragMode;
		}

        public void ShowMansusMap(SituationController situation, Transform origin, PortalEffect effect) {
            CloseAllSituationWindowsExcept(null);

            DraggableToken.CancelDrag();
            LockSpeedController(true);
            RequestNonSaveableState( NonSaveableType.Mansus, true );

            SoundManager.PlaySfx("MansusEntry");
            // Play Mansus Music
            backgroundMusic.PlayMansusClip();

            // Build mansus cards and doors everything
            mansusSituation = situation; // so we can drop the card in the right place
            _mapController.SetupMap(effect);

            var chronicler = Registry.Get<Chronicler>();
            chronicler.ChronicleMansusEntry(effect);

			//preMansusTabletopPos = tableScroll.content.anchoredPosition;

            // Do transition
            _tabletop.Show(false);
            _mapController.ShowMansusMap(origin, true);
        }

        public void ReturnFromMansus(Transform origin, ElementStackToken mansusCard)
		{
            DraggableToken.CancelDrag();
            LockSpeedController(false);
            FlushNonSaveableState();	// On return from Mansus we can't possibly be overlapping with any other non-autosave state so force a reset for safety - CP

            // Play Normal Music
            backgroundMusic.PlayRandomClip();

            // Cleanup mansus cards and doors everything
            _mapController.CleanupMap(mansusCard);

            // Do transition
            _tabletop.Show(true);
            _mapController.ShowMansusMap(origin, false);
            SoundManager.PlaySfx("MansusExit");
            
            // Pause the game with a flashing notification
            _speedController.SetPausedState(true, false, true);

            // Put card into the original Situation Results
			mansusCard.lastTablePos = null;	// Flush last known desktop position so it's treated as brand new
            mansusSituation.AddToResults(mansusCard, new Context(Context.ActionSource.PlayerDrag));
            mansusSituation.AddNote(new Notification(string.Empty, mansusCard.IlluminateLibrarian.PopMansusJournalEntry()));
            mansusSituation.OpenWindow();

            // insta setting back to last position before the mansus was transformed, but I don't like it. Feels jerky. - martin
			//tableScroll.content.anchoredPosition = preMansusTabletopPos;
            mansusSituation = null;
        }

        public void BeginNewSituation(SituationCreationCommand scc,List<IElementStack> withStacksInStorage) {
            Registry.Get<Choreographer>().BeginNewSituation(scc,withStacksInStorage);
        }

        public void SignalImpendingDoom(ISituationAnchor situationToken) {
            if(!currentDoomTokens.Contains(situationToken.EntityId))
                currentDoomTokens.Add(situationToken.EntityId);
            backgroundMusic.PlayImpendingDoom();
        }


        public void NoMoreImpendingDoom(ISituationAnchor situationToken)
        {
            if (currentDoomTokens.Contains(situationToken.EntityId))
                currentDoomTokens.Remove(situationToken.EntityId);
            if(!currentDoomTokens.Any())
                backgroundMusic.NoMoreImpendingDoom();
        }

		static private float cardPingLastTriggered = 0.0f;

		public void HighlightAllStacksForSlotSpecificationOnTabletop(SlotSpecification slotSpec)
		{
			float time = Time.realtimeSinceStartup;
			if (time > cardPingLastTriggered + 1.0f)	// Don't want to trigger these within a second of the last trigger, otherwise they stack up too much
			{
				cardPingLastTriggered = time;

				var stacks = FindAllStacksForSlotSpecificationOnTabletop(slotSpec);

				foreach (var stack in stacks)
				{
					ShowFXonToken("FX/CardPingEffect", stack.transform);
				}
			}
		}

        public AspectsInContext GetAspectsInContext(IAspectsDictionary aspectsInSituation)
        {
			if (!_enableAspectCaching)
			{
				_tabletopAspectsDirty = true;
				_allAspectsExtantDirty = true;
			}

            if (_tabletopAspectsDirty)
			{
				if (_tabletopAspects==null)
					_tabletopAspects=new AspectsDictionary();
				else
					_tabletopAspects.Clear();


				var tabletopStacks = _tabletop.GetElementStacksManager()?.GetStacks();
                if(tabletopStacks!=null)
                { 
                    foreach(var tabletopStack in tabletopStacks)
                    {
                        IAspectsDictionary stackAspects = tabletopStack.GetAspects();
                        IAspectsDictionary multipliedAspects = new AspectsDictionary();
                        //If we just count aspects, a stack of 10 cards only counts them once. I *think* this is the only place we need to worry about this rn,
                        //but bear it in mind in case there's ever a similar issue inside situations <--there is! if multiple cards are output, they stack.
                        //However! To complicate matters, if we're counting elements rather than aspects, there is already code in the stack to multiply aspect * quality, and we don't want to multiply it twice
                        foreach (var aspect in stackAspects)
                        {

                          if(aspect.Key==tabletopStack.EntityId)
                              multipliedAspects.Add(aspect.Key, aspect.Value);
                          else
                              multipliedAspects.Add(aspect.Key, aspect.Value * tabletopStack.Quantity);
                        }
                        _tabletopAspects.CombineAspects(multipliedAspects);
                    }


                    if (_enableAspectCaching)
                        _tabletopAspectsDirty = false;		// If left dirty the aspects will recalc every frame
                }
                _allAspectsExtantDirty = true;		// Force the aspects below to recalc
			}

			if (_allAspectsExtantDirty)
			{
				if (_allAspectsExtant == null)
					_allAspectsExtant=new AspectsDictionary();
				else
					_allAspectsExtant.Clear();

				var allSituations = Registry.Get<SituationsCatalogue>();
				foreach (var s in allSituations.GetRegisteredSituations())
                {
                    var stacksInSituation = new List<IElementStack>();
                    stacksInSituation.AddRange(s.GetStartingStacks());
                    stacksInSituation.AddRange(s.GetOngoingStacks());
                    stacksInSituation.AddRange(s.GetStoredStacks());
                    stacksInSituation.AddRange(s.GetOutputStacks());

                    foreach (var situationStack in stacksInSituation)
                    {
                        IAspectsDictionary stackAspects = situationStack.GetAspects();
                        IAspectsDictionary multipliedAspects = new AspectsDictionary();
                        //See notes above. We need to multiply aspects to take account of stack quantities here too.
                        foreach (var aspect in stackAspects)
                        {

                            if (aspect.Key == situationStack.EntityId)
                                multipliedAspects.Add(aspect.Key, aspect.Value);
                            else
                                multipliedAspects.Add(aspect.Key, aspect.Value * situationStack.Quantity);
                        }
                        _allAspectsExtant.CombineAspects(multipliedAspects);
                    }

                }
                _allAspectsExtant.CombineAspects(_tabletopAspects);

				if (_enableAspectCaching)
					_allAspectsExtantDirty = false;		// If left dirty the aspects will recalc every frame
			}

            AspectsInContext aspectsInContext=new AspectsInContext(aspectsInSituation, _tabletopAspects, _allAspectsExtant);

            return aspectsInContext;

        }

        public void GroupAllStacks()
        {
	        var stacks = _tabletop.GetElementStacksManager().GetStacks();
	        var groups = stacks.OfType<ElementStackToken>()
		        .GroupBy(e => e.EntityWithMutationsId, e => e)
		        .Select(group => group.OrderByDescending(e => e.Quantity).ToList());
	        var mergedStacks = false;
	        foreach (var group in groups)
	        {
		        var primaryStack = group.First();
		        var mergedStack = false;
		        foreach (var stack in group.Skip(1))
			        if (primaryStack.CanMergeWith(stack))
			        {
				        primaryStack.MergeIntoStack(stack);
				        mergedStack = true;
			        }

		        if (mergedStack)
			        StartCoroutine(primaryStack.PulseGlow());

		        mergedStacks |= mergedStack;
	        }
	        
	        if (mergedStacks)
		        SoundManager.PlaySfx("CardPutOnStack");
        }

		private List<ElementStackToken> FindAllStacksForSlotSpecificationOnTabletop(SlotSpecification slotSpec) {
			var stackList = new List<ElementStackToken>();
			var stacks = _tabletop.GetElementStacksManager().GetStacks();
			ElementStackToken stackToken;

			foreach (var stack in stacks) {
				stackToken = stack as ElementStackToken;

				if (stackToken != null && CanPullCardToGreedySlot(stackToken, slotSpec))
					stackList.Add(stackToken);
			}

			return stackList;
		}

		private void ShowFXonToken(string name, Transform parent) {
			var prefab = Resources.Load(name);

			if (prefab == null)
				return;

			var obj = Instantiate(prefab) as GameObject;

			if (obj == null)
				return;

			obj.transform.SetParent(parent);
			obj.transform.localScale = Vector3.one;
			obj.transform.localPosition = Vector3.zero;
			obj.transform.localRotation = Quaternion.identity;
			obj.gameObject.SetActive(true);
		}

		private void OnGUI()
		{
#if UNITY_EDITOR
			// Extra tools for debugging autosave.

			// Toggle to simulate bad save
			if (GUI.Button( new Rect(Screen.width * 0.5f - 300f, 10f, 180f, 20f), "Simulate bad save: " + (GameSaveManager.simulateBrokenSave?"ON":"OFF") ))
			{
				GameSaveManager.simulateBrokenSave = !GameSaveManager.simulateBrokenSave;		// Click
			}

			// Counter to show time to next autosave. Click it to reduce to a five second countdown
			if (GUI.Button( new Rect(Screen.width * 0.5f - 100f, 10f, 150f, 20f), "Autosave in " + (int)(AUTOSAVE_INTERVAL-housekeepingTimer) ))
			{
				housekeepingTimer = AUTOSAVE_INTERVAL - 5f;		// Click
			}

			if (!IsSafeToAutosave())
			{
				GUI.TextArea( new Rect(Screen.width * 0.5f + 50f, 10f, 70f, 20f), "BLOCKED" );
			}

			#if ENABLE_ASPECT_CACHING
			if (GUI.Button( new Rect(Screen.width * 0.5f - 300f, 35f, 180f, 20f), "Aspect caching: " + (_enableAspectCaching?"ON":"OFF") ))
			{
				_enableAspectCaching = !_enableAspectCaching;		// Click
			}
			#endif
#endif
			// Allowing this in final build to allow users to screengrab errors
			NoonUtility.DrawLog();
		}


        public void NotifyStacksChanged()
        {
          NotifyAspectsDirty();
        }
    }


}
