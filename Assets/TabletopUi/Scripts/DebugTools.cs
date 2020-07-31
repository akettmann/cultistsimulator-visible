﻿#pragma warning disable 0649
using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Core;
using Assets.Core.Commands;
using Assets.Core.Entities;
using Assets.Core.Fucine;
using Assets.Core.Interfaces;
using Assets.CS.TabletopUI;
using Assets.TabletopUi.Scripts;
using Assets.TabletopUi.Scripts.Infrastructure;
using Assets.TabletopUi.Scripts.Infrastructure.Modding;
using Assets.TabletopUi.Scripts.Services;
using Noon;
using Steamworks;
using TabletopUi.Scripts.Interfaces;
using TMPro;
using UnityEngine.UI;
using UnityEngine.VR;
using static Noon.NoonUtility;

public class DebugTools : MonoBehaviour,IRollOverride
{
    private const int MaxAutoCompletionSuggestions = 50;

    [SerializeField] private TabletopTokenContainer tabletop;
    [SerializeField] private Heart heart;
    [SerializeField] private InputField input;
    [SerializeField] private ScrollRect autoCompletionBox;
    [SerializeField] private VerticalLayoutGroup autoCompletionSuggestions;
    [SerializeField] private Button btnPlusOne;
    [SerializeField] private Button btnMinusOne;
    [SerializeField] private Button btnBeginSituation;
    [SerializeField] private Button btnHaltVerb;
    [SerializeField] private Button btnDeleteVerb;
    [SerializeField] private Button btnPurgeElement;
    [SerializeField] public Button btnTriggerAchievement;
    [SerializeField] private Button btnResetAchivement;
    [SerializeField] private Button btnFastForward;
    [SerializeField] private Button btnNextTrack;
    [SerializeField] private Button btnUpdateContent;
    [SerializeField] private Button btnEndGame;
    [SerializeField] private Button btnLoadGame;
    [SerializeField] private Button btnSaveGame;
    [SerializeField] private Button btnResetDecks;
    [SerializeField] private BackgroundMusic backgroundMusic;
    [SerializeField] private Button btnQueueRoll;
    [SerializeField] private TMP_InputField rollToQueue;
    [SerializeField] private TextMeshProUGUI rollsQueued;

    // Debug Load/Save/Delete buttons
    [SerializeField] private List<Button> saveButtons;
    [SerializeField] private List<Button> loadButtons;
    [SerializeField] private List<Button> delButtons;

    public string endingAnimFXName = "DramaticLightEvil";

    public List<int> QueuedRollsList;

    public Transform AutoCompletionSuggestionPrefab;

    // Indicates the last selected auto-completion suggestion
    // -1 means no previous suggestion was selected
    private int currentAutoCompletionSuggestion = -1;

    public void Awake()
    {
        autoCompletionBox.gameObject.SetActive(false);
        input.onValueChanged.AddListener(AttemptAutoCompletion);
        btnPlusOne.onClick.AddListener(() => AddCard(input.text));
        btnMinusOne.onClick.AddListener(() => RemoveItem(input.text));
        btnFastForward.onClick.AddListener(() => FastForward(30));
        btnUpdateContent.onClick.AddListener(UpdateCompendiumContent);
        btnEndGame.onClick.AddListener(()=>EndGame(input.text));
        btnLoadGame.onClick.AddListener(LoadGame);
        btnSaveGame.onClick.AddListener(SaveGame);
        btnResetDecks.onClick.AddListener(ResetDecks);
        btnNextTrack.onClick.AddListener(NextTrack);
        btnBeginSituation.onClick.AddListener(()=>BeginSituation(input.text));
        btnHaltVerb.onClick.AddListener(() => HaltVerb(input.text));
        btnDeleteVerb.onClick.AddListener(() => DeleteVerb(input.text));
        btnPurgeElement.onClick.AddListener(() => PurgeElement(input.text));


        btnTriggerAchievement.onClick.AddListener(()=>TriggerAchievement(input.text));
        btnResetAchivement.onClick.AddListener(() => ResetAchievement(input.text));


        btnQueueRoll.onClick.AddListener(()=>QueueRoll(rollToQueue.text));

        QueuedRollsList=new List<int>();

        for (int i = 0; i < saveButtons.Count; i++)
        {
            var index = i;
            saveButtons[i].onClick.AddListener(() => SaveDebugSave(index));
        }
        for (int i = 0; i < loadButtons.Count; i++)
        {
            var index = i;
            loadButtons[i].onClick.AddListener(() => LoadDebugSave(index));
            if (!CheckDebugSaveExists(i))
                loadButtons[i].interactable = false;
        }
        for (int i = 0; i < delButtons.Count; i++)
        {
            var index = i;
            delButtons[i].onClick.AddListener(() => DeleteDebugSave(index));
            if (!CheckDebugSaveExists(i))
                delButtons[i].interactable = false;
        }




    }



    public bool ProcessInput()
    {
        if (!autoCompletionBox.isActiveAndEnabled)
            return false;

        // If the user has right-clicked, close the suggestions box
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
        {
            AttemptAutoCompletion(null);
            return true;
        }

        // Only process the rest when the main input field is open
        if (!input.isFocused)
            return false;

        List<AutoCompletionSuggestion> suggestions = new List<AutoCompletionSuggestion>();
        autoCompletionSuggestions.GetComponentsInChildren(suggestions);

        if (suggestions.Count == 0)
            return false;

        // Check if the user is tab-completing
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            currentAutoCompletionSuggestion = 0;
            AutoCompletionSuggestion suggestion = suggestions.First();
            SetInput(suggestion.GetText());
            input.MoveTextEnd(false);
            return true;
        }

        // Check if the user is navigating suggestions with the arrow keys
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            // Get the next suggestion based on what was previously used
            if (currentAutoCompletionSuggestion < 0)
                currentAutoCompletionSuggestion = 0;
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                currentAutoCompletionSuggestion++;
            else if (Input.GetKeyDown(KeyCode.UpArrow))
                currentAutoCompletionSuggestion--;

            // Fold back to beginning and end of the suggestions if we overflow
            if (currentAutoCompletionSuggestion >= suggestions.Count)
                currentAutoCompletionSuggestion = 0;
            else if (currentAutoCompletionSuggestion < 0)
                currentAutoCompletionSuggestion = suggestions.Count - 1;

            SetInput(suggestions[currentAutoCompletionSuggestion].GetText());
            input.MoveTextEnd(false);
            return true;
        }

        return false;
    }

    public void SetInput(string text)
    {
        // Do nothing if it's not open
        if (!isActiveAndEnabled || text == null)
            return;

        // Temporarily disable suggestions so that this doesn't trigger a new auto-completion attempt
        input.onValueChanged.RemoveListener(AttemptAutoCompletion);
        input.text = text;
        input.onValueChanged.AddListener(AttemptAutoCompletion);
    }

    void AttemptAutoCompletion(string value)
    {
        // Don't show the suggestion box if the field is empty
        if (string.IsNullOrEmpty(value))
        {
            autoCompletionBox.gameObject.SetActive(false);
            return;
        }
        autoCompletionBox.gameObject.SetActive(true);

        // Clear the list
        foreach (Transform child in autoCompletionSuggestions.transform)
            Destroy(child.gameObject);

        // Re-populate it with updated suggestions
        // Disable the suggestion box if there are no suggestions
        ICompendium compendium = Registry.Retrieve<ICompendium>();
        List<AutoCompletionSuggestion> suggestions = GetElementAutoCompletionSuggestions(compendium, value)
            .Concat(GetRecipeAutoCompletionSuggestions(compendium, value))
            .OrderBy(acs => acs.GetText())
            .ToList();
        if (suggestions.Count == 0)
        {
            autoCompletionBox.gameObject.SetActive(false);
            return;
        }
        foreach (var suggestion in suggestions)
            suggestion.transform.SetParent(autoCompletionSuggestions.transform, false);
    }

    void ApplySuggestion(string suggestion)
    {
        SetInput(suggestion);
        autoCompletionBox.gameObject.SetActive(false);
    }

    List<AutoCompletionSuggestion> GetElementAutoCompletionSuggestions(ICompendium compendium, string prompt)
    {
        return compendium.GetEntitiesAsList<Element>().
            Where(e => e.Id.StartsWith(prompt)).Select(e => MakeAutocompleteSuggestion(compendium, e.Id, true)).ToList();
    }

    List<AutoCompletionSuggestion> GetRecipeAutoCompletionSuggestions(ICompendium compendium, string prompt)
    {
        return compendium.GetEntitiesAsList<Recipe>().
            Where(r => r.Id.StartsWith(prompt)).Select(r => MakeAutocompleteSuggestion(compendium, r.Id, false)).ToList();
    }

    AutoCompletionSuggestion MakeAutocompleteSuggestion(ICompendium compendium, string suggestedId, bool isElement)
    {
        AutoCompletionSuggestion suggestion = Instantiate(AutoCompletionSuggestionPrefab).GetComponent<AutoCompletionSuggestion>();
        suggestion.SetText(suggestedId);
        suggestion.AddClickListener(() => ApplySuggestion(suggestedId));

        // Show the element image if applicable
        if (isElement)
            suggestion.SetIconForElement(compendium.GetEntityById<Element>(suggestedId));

        return suggestion;
    }

    void AddCard(string elementId)
    {
        var stackManager = tabletop.GetElementStacksManager();
        var existingStacks = stackManager.GetStacks();

        var element = Registry.Retrieve<ICompendium>().GetEntityById<Element>(elementId);

        if (element == null) {
            Debug.LogWarning("No Element with ID " + elementId + " found!");
            return;
        }

        //check if there's an existing stack of that type to increment
        if (!element.Unique) {
            foreach (var stack in existingStacks)
            {
                if (stack.EntityId == elementId && !stack.GetCurrentMutations().Any())
                {
                    stack.ModifyQuantity(1,new Context(Context.ActionSource.Debug));
                    return;
                }
            }
        }
        //if we didn't jump out of loop with return, above
		Context debugContext = new Context(Context.ActionSource.Debug);
        stackManager.ModifyElementQuantity(elementId,1, Source.Fresh(), debugContext);

		// Find the card we just added and move it to the dropzone
		existingStacks = stackManager.GetStacks();
		foreach (var stack in existingStacks)
        {
            if(stack.EntityId==elementId && !stack.GetCurrentMutations().Any())
            {
				ElementStackToken token = stack as ElementStackToken;
				Vector2 dropPos = token.GetDropZoneSpawnPos();
				
	            Registry.Retrieve<Choreographer>().ArrangeTokenOnTable(token, debugContext, dropPos, false);	// Never push other cards aside - CP
            }
        }
    }

    void RemoveItem(string itemId)
    {
        //do we have an inactive empty verb with this id?
       var possibleEmptyVerb= Registry.Retrieve<SituationsCatalogue>().GetRegisteredSituations().FirstOrDefault(s => s.situationToken.EntityId==itemId);
        if(possibleEmptyVerb!=null)
        { if(!possibleEmptyVerb.situationWindow.GetOutputStacks().Any() && !possibleEmptyVerb.IsOngoing)
            possibleEmptyVerb.Retire();
        }
        else
        tabletop.GetElementStacksManager().ModifyElementQuantity(itemId, -1, Source.Existing(), new Context(Context.ActionSource.Debug));
    }

    void BeginSituation(string recipeId)
    {
        var compendium = Registry.Retrieve<ICompendium>();
        var recipe = compendium.GetEntityById<Recipe>(recipeId.Trim());
        if (recipe!=null)
        {
            IVerb verbForNewSituation = compendium.GetEntityById<BasicVerb>(recipe.Id);
            if(verbForNewSituation==null)
                verbForNewSituation = new CreatedVerb(recipe.ActionId, recipe.Label, recipe.Description);

            SituationCreationCommand scc = new SituationCreationCommand(verbForNewSituation, recipe, SituationState.FreshlyStarted);
        Registry.Retrieve<ITabletopManager>().BeginNewSituation(scc,new List<IElementStack>());
        }
        else
        Debug.Log("couldn't find this recipe: " + recipeId);
    }

    void HaltVerb(string verbId)
    {
        Registry.Retrieve<ITabletopManager>().HaltVerb(verbId, 1);

    }

    private void DeleteVerb(string verbId)
    {
        Registry.Retrieve<ITabletopManager>().DeleteVerb(verbId,1);
    }

    private void PurgeElement(string elementId)
    {
        Registry.Retrieve<ITabletopManager>().PurgeElement(elementId, 1);
    }


    void BeginLegacy(string legacyId)
    {
        var l = Registry.Retrieve<ICompendium>().GetEntityById<Legacy>(legacyId);
        if (l == null)
            return;


    }

    void TriggerAchievement(string achievementId)
    {
        var storefrontServicesProvider = Registry.Retrieve<StorefrontServicesProvider>();
        storefrontServicesProvider.SetAchievementForCurrentStorefronts(achievementId,true);
    }

    void ResetAchievement(string achievementId)
    {
        var storefrontServicesProvider = Registry.Retrieve<StorefrontServicesProvider>();
        storefrontServicesProvider.SetAchievementForCurrentStorefronts(achievementId, false);
    }

    void FastForward(float interval)
    {
            heart.AdvanceTime(interval);
    }

    void UpdateCompendiumContent()
    {
        Registry.Retrieve<ModManager>().CatalogueMods();
            
           var existingCompendium = Registry.Retrieve<ICompendium>();
           var compendiumLoader = new CompendiumLoader();

           var startImport = DateTime.Now;
           var log=compendiumLoader.PopulateCompendium(existingCompendium);
        foreach(var m in log.GetMessages())
            Log(m.Description,m.MessageLevel);

        Log("Total time to import: " + (DateTime.Now-startImport));

        // Populate current decks with new cards (this will shuffle the deck)
        IGameEntityStorage storage = Registry.Retrieve<Character>();
        foreach (var ds in existingCompendium.GetEntitiesAsList<DeckSpec>())
        {
               
            if (storage.GetDeckInstanceById(ds.Id) == null)
            {
                IDeckInstance di = new DeckInstance(ds);
                storage.DeckInstances.Add(di);
                di.Reset();
            }
        }

           

       
    }

    public void WordCount()
    {
        var compendium = Registry.Retrieve<ICompendium>();
        var log=new ContentImportLog();
        compendium.CountWords(log);
        foreach (var m in log.GetMessages())
            Log(m.Description, m.MessageLevel);

    }

    public void FnordCount()
    {
        var compendium = Registry.Retrieve<ICompendium>();
        var log = new ContentImportLog();
        compendium.LogFnords(log);
        foreach (var m in log.GetMessages())
            Log(m.Description, m.MessageLevel);

    }

    public void ImageCheck()
    {

        var compendium = Registry.Retrieve<ICompendium>();
        var log = new ContentImportLog();
        compendium.LogMissingImages(log);
        foreach (var m in log.GetMessages())
            Log(m.Description, m.MessageLevel);


    }



    void NextTrack()
    {
        backgroundMusic.PlayNextClip();
    }

    // to allow access from HotkeyWatcher
    public void EndGame(string endingId)
    {
        var compendium = Registry.Retrieve<ICompendium>();

        var ending = compendium.GetEntityById<Ending>(endingId);

        ending.Anim = endingAnimFXName;

        // Get us a random situation that killed us!
        var situationControllers = Registry.Retrieve<SituationsCatalogue>().GetRegisteredSituations();
        var deathSit = situationControllers[UnityEngine.Random.Range(0, situationControllers.Count)];

        Registry.Retrieve<ITabletopManager>().EndGame(ending, deathSit);
    }

    public void LoadGame()
    {
        Registry.Retrieve<ITabletopManager>().LoadGame();
    }

    public void SaveGame()
    {
        var saveTask = Registry.Retrieve<ITabletopManager>().SaveGameAsync(true);
        StartCoroutine(saveTask);
    }

    void ResetDecks()
    {
        var character= Registry.Retrieve<Character>();
        foreach(var di in character.DeckInstances)
        {di.Reset();
            Log("Reset " + di.Id + " - now contains ");
            foreach (var card in di.GetCurrentCardsAsList())
            {
                Log(card + "\n" );
            }
        }
    }

    void QueueRoll(string roll)
    {
        int rollValue;
        int.TryParse(roll, out rollValue);
        if(rollValue>=1 && rollValue<=100)
            QueuedRollsList.Add(rollValue);

        UpdatedQueuedRollsDisplay();

    }

    public void UpdatedQueuedRollsDisplay()
    {
        rollsQueued.text = string.Empty;
        foreach(var i in QueuedRollsList)
        {
            if (rollsQueued.text!="")
                rollsQueued.text += ", ";

            rollsQueued.text += i.ToString();
        }
    }

    public int PopNextOverrideValue(Recipe recipe = null)
    {
        if (!QueuedRollsList.Any())
            return 0;
        else
        {
        int result = QueuedRollsList.First();
            QueuedRollsList.RemoveAt(0);
            UpdatedQueuedRollsDisplay();
            return result;
        }
    }

    void SaveDebugSave(int index)
    {
        ITabletopManager tabletopManager = Registry.Retrieve<ITabletopManager>();
        StartCoroutine(tabletopManager.SaveGameAsync(true, index + 1, success =>
        {
            loadButtons[index].interactable = success;
            delButtons[index].interactable = success;
        }));
    }

    void LoadDebugSave(int index)
    {
        if (!CheckDebugSaveExists(index))
            return;
        ITabletopManager tabletopManager = Registry.Retrieve<ITabletopManager>();
        tabletopManager.LoadGame(index + 1);
    }

    void DeleteDebugSave(int index)
    {
        if (!CheckDebugSaveExists(index))
            return;
        File.Delete(GetGameSaveLocation(index + 1));
        loadButtons[index].interactable = false;
        delButtons[index].interactable = false;
    }

    private bool CheckDebugSaveExists(int index)
    {
        return File.Exists(GetGameSaveLocation(index + 1));
    }
}

public interface IRollOverride
{
    //if at least one override is queued, pop it and return it
    //if none are queued, return 0
    // If a contextual recipe is passed, it can affect the dice roll
    int PopNextOverrideValue(Recipe recipe = null);

}
