﻿#pragma warning disable 0649
using System;
using System.Collections.Generic;
using System.Numerics;
using Assets.Scripts.Application.UI.Situation;
using SecretHistories.Commands;
using SecretHistories.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SecretHistories.Entities;
using SecretHistories.Fucine;
using SecretHistories.Constants;
using SecretHistories.Spheres;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace SecretHistories.UI {

    [Serializable]
    public class OnSphereAddedEvent : UnityEvent<Sphere> { }

    [Serializable]
    public class OnSphereRemovedEvent : UnityEvent<Sphere> { }


    [RequireComponent(typeof(SituationWindowPositioner))]
    public class SituationWindow : MonoBehaviour, ISituationSubscriber {

        [Header("Visuals")]
		[SerializeField] CanvasGroupFader canvasGroupFader;
        public SituationWindowPositioner positioner;

        [Space]
        [SerializeField] Image artwork;
        [SerializeField] TextMeshProUGUI title;
		public PaginatedText PaginatedNotes;


        [Space]
        [SerializeField] List<Dominion> Dominions;


        [Space]
        [SerializeField] AspectsDisplay aspectsDisplay;

		[SerializeField] Button startButton;
		[SerializeField] TextMeshProUGUI startButtonText;

        public UnityEvent OnStart;
        public UnityEvent OnCollect;
        public UnityEvent OnWindowClosed;
        public OnSphereAddedEvent OnSphereAdded;
        public OnSphereRemovedEvent OnSphereRemoved;

        public TokenLocation LastOpenLocation;

        private Situation situation; //Ideally, we would reduce this to an ITokenPayload
        private bool windowIsWide = false;

        public bool IsVisible {
            get { return canvasGroupFader.IsVisible(); }
        }

		public string Title {
			get { return title.text; }
			set { title.text = value; }
        }

		public Vector3 Position
		{
			get { return positioner.GetPosition(); }
			set { positioner.SetPosition( value ); }
		}

        public void Attach(Situation newSituation, TokenLocation initialLocation)
        {
            situation = newSituation;

            situation.AddSubscriber(this);
            

            OnWindowClosed.AddListener(newSituation.Close);
            OnStart.AddListener(newSituation.TryStart);
            OnCollect.AddListener(newSituation.Conclude);
            OnSphereAdded.AddListener(newSituation.AttachSphere);
            OnSphereRemoved.AddListener(newSituation.RemoveSphere);

            name = "Window_" + situation.Id;
            DisplayIcon(situation.Id);

            Title = situation.Label;
            PaginatedNotes.SetText(newSituation.Description);
            startButton.onClick.AddListener(OnStart.Invoke);


            foreach (var d in Dominions)
                d.RegisterFor(situation);
            
            startButton.gameObject.SetActive(true);
 

            SituationStateChanged(situation);
            TimerValuesChanged(situation);

            positioner.SetInitialPosition(initialLocation.Anchored3DPosition);
     
        }

        public void TryResizeWindow(int slotsCount)
        {
            SetWindowSize(slotsCount > 3);
        }
        public void DisplayIcon(string icon)
        {
            Sprite sprite = ResourcesManager.GetSpriteForVerbLarge(icon);
            artwork.sprite = sprite;
        }
         
    

        public void Closed() {
          OnWindowClosed.Invoke();
        }


        public void Show( Vector3 startPosition)
		{
			if (!IsVisible)
			{
				SoundManager.PlaySfx("SituationWindowShow");
                canvasGroupFader.Show();
                SituationStateChanged(situation);
            }
            
            positioner.Show(canvasGroupFader.durationTurnOn, startPosition); // Animates the window (position allows optional change is position)

            PaginatedNotes.SetFinalPage();

        }

		public void Hide(Situation s) {
            if (IsVisible)
            {
				SoundManager.PlaySfx("SituationWindowHide");
                SituationStateChanged(s);
                canvasGroupFader.Hide();
                
            }
            
        }

        public void Collect()
        {
            OnCollect.Invoke();
        }


        public void DisplayPredictedRecipe(Situation s)
        {

            Title = s.CurrentRecipePrediction.Title;
            
            if(s.CurrentRecipePrediction. HintOnly)
                PaginatedNotes.SetText("<i>" + s.CurrentRecipePrediction.DescriptiveText + "</i>");
            else
                PaginatedNotes.SetText(s.CurrentRecipePrediction.DescriptiveText);


            if(s.Recipe.Craftable)
            {
                SoundManager.PlaySfx("SituationAvailable");
              //  ongoingDisplay.UpdateDisplay(s); //Ensures that the time bar is set to 0 to avoid a flicker - commented this out while refactoring, but bear it in mind
            }
            
        }

        
       public void SetWindowSize(bool wide) {
            RectTransform rectTrans = transform as RectTransform;

            if (wide)
                rectTrans.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 900f);
            else
                rectTrans.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 700f);

            if (wide != windowIsWide) {
                if (wide)
                    rectTrans.anchoredPosition = rectTrans.anchoredPosition + new Vector2(100f, 0f);
                else
                    rectTrans.anchoredPosition = rectTrans.anchoredPosition - new Vector2(100f, 0f);
            }

            windowIsWide = wide;

        }


        void DisplayButtonState(Situation situation) {

            if (situation.TimeRemaining > 0)
            {
                startButtonText.GetComponent<Babelfish>().UpdateLocLabel(NoonConstants.SITUATION_RUNNING);
                startButton.interactable = false;
            }
            else if (situation.CurrentRecipePrediction!=null && situation.CurrentRecipePrediction.Craftable)
            {
                startButtonText.GetComponent<Babelfish>().UpdateLocLabel(NoonConstants.SITUATION_STARTABLE);
                startButton.interactable = true;
            }
            else
            {
                startButtonText.GetComponent<Babelfish>().UpdateLocLabel(NoonConstants.SITUATION_STARTABLE);
                startButton.interactable = false;
            }

        }

        // ACTIONS

        public IEnumerable<ISituationNote> GetNotes() {
            return PaginatedNotes.GetCurrentTexts();
        }

        public void SituationStateChanged(Situation situation)
        {
            
                DisplayButtonState(situation);
        }

        public void TimerValuesChanged(Situation situation)
        {
  //
        }


        public void SituationSphereContentsUpdated(Situation s)
        {
            DisplayPredictedRecipe(s);
            
            var allAspectsToDisplay =s.GetAspects(false);
            aspectsDisplay.DisplayAspects(allAspectsToDisplay);
            DisplayButtonState(s);

        }

        public void ReceiveNotification(INotification n)
        {
            PaginatedNotes.AddText(n.Description);
        }

        public void ReceiveCommand(IAffectsTokenCommand command)
        {
            //can't make use of  it
        }


        //public override void MoveObject(PointerEventData eventData) {
        //    Vector3 dragPos;
        //    RectTransformUtility.ScreenPointToWorldPointInRectangle(Registry.Get<IDraggableHolder>().RectTransform, eventData.position, eventData.pressEventCamera, out dragPos);

        //    // Potentially change this so it is using UI coords and the RectTransform?
        //    rectTransform.position = new Vector3(dragPos.x + dragOffset.x, dragPos.y + dragOffset.y, dragPos.z + dragHeight);

        //    // rotate object slightly based on pointer Delta
        //    if (rotateOnDrag && eventData.delta.sqrMagnitude > 10f) {
        //        // This needs some tweaking so that it feels more responsive, physica. Card rotates into the direction you swing it?
        //        perlinRotationPoint += eventData.delta.sqrMagnitude * 0.001f;
        //        transform.localRotation = Quaternion.Euler(new Vector3(0, 0, -10 + Mathf.PerlinNoise(perlinRotationPoint, 0) * 20));
        //    }
        //}
        public void Retire()
        {
            Destroy(gameObject);
        }

 
    }
}
