﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SecretHistories.Abstract;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.Fucine;
using SecretHistories.UI;
using SecretHistories.Constants;
using SecretHistories.Constants.Events;
using SecretHistories.Elements;
using SecretHistories.Ghosts;
using SecretHistories.Manifestations;
using SecretHistories.Services;
using SecretHistories.Spheres;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SecretHistories.Manifestations
{
    [RequireComponent(typeof(RectTransform))]
    public class CardManifestation : BasicManifestation, IManifestation,IPointerEnterHandler,IPointerExitHandler
    {

        [SerializeField] private Image artwork;
        [SerializeField] private Image backArtwork;
        [SerializeField] public Image textBackground;
        [SerializeField] public TextMeshProUGUI text;
        [SerializeField] public ElementStackBadge stackBadge;
        [SerializeField] public TextMeshProUGUI stackCountText;
        [SerializeField] public GameObject decayView;
        [SerializeField] public TextMeshProUGUI decayCountText;
        [SerializeField] public Sprite spriteDecaysTextBG;
        [SerializeField] public Sprite spriteUniqueTextBG;
        [SerializeField] public BasicShadowImplementation shadow;
        [SerializeField] public CanvasGroup canvasGroup;
        [SerializeField] public GraphicFader glowImage;


     
        private Image decayBackgroundImage;
        private Color cachedDecayBackgroundColor;
       [SerializeField] private float decayAlpha = 0.0f;
        private Coroutine animCoroutine;
        private List<Sprite> frames;
        private FlipHelper _flipHelper;
        private string _entityId;
        private int _quantity;
        private float _originalDuration = 0f;
        private float _durationRemaining = 0f;
        private bool _forceDisplayTimeRemaining = false;

        private Coroutine _showingDecayTimer;
        private Coroutine _hidingDecayTimer;

        //yes, we really do have to do this, or find a more sophisticated wrapper for coroutines.
        //We can't check if a coroutine is running, StopCoRoutine doesn't set it to null, and using
        //setting a coroutine to null when it might be running can cause race condition issues.
        //This is probably the cause of the shadow that doesn't disappear.
        private bool _showingDecayTimerFlag = false;
        private bool _hidingDecayTimerFlag = false;
        
        public bool RequestingNoDrag => _flipHelper.FlipInProgress;
        public bool RequestingNoSplit => stackBadge.PointerAboveThis;


        public void Awake()
        {
            _flipHelper = new FlipHelper(this);
        }


        
        public void Initialise(IManifestable manifestable)
        {
            
               Sprite sprite = ResourcesManager.GetSpriteForElement(manifestable.Icon);
            artwork.sprite = sprite;

            if (sprite == null)
                artwork.color = Color.clear;
            else
                artwork.color = Color.white;

            SetCardBackground(manifestable.Unique, manifestable.GetTimeshadow().Transient);

            name = "CardManifestation_" + manifestable.Id;
            decayBackgroundImage = decayView.GetComponent<Image>();
            cachedDecayBackgroundColor = decayBackgroundImage.color;

            frames = ResourcesManager.GetAnimFramesForElement(manifestable.EntityId);
            _entityId = manifestable.EntityId;
            _quantity = manifestable.Quantity;

        }




        public void SendNotification(INotification notification)
        {
            NoonUtility.LogWarning("CardManifestation doesn't support SendNotification");
        }

        public void Emphasise()
        {
            canvasGroup.alpha = 1f;
        }

        public void Understate()
        {
            canvasGroup.alpha = 0.3f;
        }
        

        public bool HandlePointerClick(PointerEventData eventData, Token token)
        {
            return false;

        }

        public void DisplaySpheres(IEnumerable<Sphere> spheres)
        {
            NoonUtility.LogWarning("CardManifestation doesn't support DisplaySpheres");
        }

        public IGhost CreateGhost()
        {
            var newGhost = Watchman.Get<PrefabFactory>()
                .CreateGhostPrefab(typeof(CardGhost), this.RectTransform);
            return newGhost;
        }

        public void OverrideIcon(string icon)
        {
            NoonUtility.LogWarning("CardManifestation doesn't support OverrideIcon");
        }


        public override void UpdateLocalScale(Vector3 newScale)
        {
            RectTransform.localScale = newScale;
            text.rectTransform.localScale = newScale;
            text.ForceMeshUpdate(false, false); //Without this, the scale of the textmesh object may not match the parent object, which means the text goes blurry.
        }

        public void OnBeginDragVisuals(Token token)
        {
            ShowCardShadow(true); // Ensure we always have a shadow when dragging
        }


        public void OnEndDragVisuals(Token token)
        {
            ShowCardShadow(false);
        }

        public void Highlight(HighlightType highlightType, IManifestable manifestable)
        {
            if (highlightType == HighlightType.WillInteract)
            {
                SetGlowColor(UIStyle.GlowPurpose.Default);
                ShowGlow(true,false);
            }
            else if (highlightType == HighlightType.AttentionPls)
            {
                SetGlowColor(UIStyle.GlowPurpose.Default);
                StartCoroutine(PulseGlow());
            }
            else if (highlightType == HighlightType.PotentiallyRelevant)
            {
                SetGlowColor(UIStyle.brightPink);
                ShowGlow(true, false);
            }
            else if (highlightType == HighlightType.Hover)
            {
                ShowHoverGlow(true);
            }

            if (Decays())
                _forceDisplayTimeRemaining = true;
            
            
            UpdateVisuals(manifestable);
            

        }

        public void Unhighlight(HighlightType highlightType, IManifestable manifestable)
        {
            if(highlightType==HighlightType.All)
            {
                ShowGlow(false,false);
                ShowHoverGlow(false);
                return;
            }

            if (highlightType == HighlightType.Hover)
            {
                ShowHoverGlow(false);
            }
            else if (highlightType == HighlightType.PotentiallyRelevant || highlightType == HighlightType.WillInteract)
            {
                ShowGlow(false, false);
            }

            if (Decays())
                _forceDisplayTimeRemaining = false;

            UpdateVisuals(manifestable);


        }

        private IEnumerator PulseGlow()
        {
            ShowHoverGlow(true, false, Color.white);
            yield return new WaitForSeconds(0.5f);
            ShowHoverGlow(false);
        }

        public void DoMove(RectTransform tokenRectTransform)
        {
           shadow.DoMove(tokenRectTransform);
        }


        private void ShowGlow(bool glowState, bool instant)
        {
            if(glowImage.Equals(null))
                return; //just in case this manifestation is currently undergoing Destroy

            if (glowState)
                glowImage.Show(instant);
            else
                glowImage.Hide(instant);
        }

        private void ShowCardShadow(bool show)
        {
            shadow.gameObject.SetActive(show);
        }



        private void SetCardBackground(bool unique, bool decays)
        {
            if (unique)
                textBackground.overrideSprite = spriteUniqueTextBG;
            else if (decays)
                textBackground.overrideSprite = spriteDecaysTextBG;
            else
                textBackground.overrideSprite = null;
        }

        public void UpdateVisuals(IManifestable manifestable)
        {
            _entityId = manifestable.EntityId;
            _quantity = manifestable.Quantity;

            text.text = manifestable.Label;
            stackBadge.gameObject.SetActive(_quantity > 1);
            stackCountText.text = _quantity.ToString();
            var timeshadow = manifestable.GetTimeshadow();
            UpdateTimerVisuals(timeshadow.Lifetime,timeshadow.LifetimeRemaining,timeshadow.LastInterval,timeshadow.Resaturate);

        }

        private bool Decays()
        {
            return _originalDuration > 0f;
        }


        private bool ApproachingFinalDissolution()
        {
            return _durationRemaining < _originalDuration / 2;
        }

        IEnumerator ShowingDecayTimer()
        {
            _showingDecayTimerFlag = true;

            while (decayAlpha < 1.0f)
            {

                decayAlpha += 0.1f;

            if (decayCountText && decayBackgroundImage)
            {
                Color col = decayCountText.color;
                col.a = decayAlpha;
                decayCountText.color = col;
                col = cachedDecayBackgroundColor; // then we can multiply with the non-1 alpha - CP
                col.a *= decayAlpha;
                decayBackgroundImage.color = col;
            }
            yield return new WaitForSeconds(0.03f);

            }

            _showingDecayTimerFlag = false;
        }

        IEnumerator HidingDecayTimer()
        {
            _hidingDecayTimerFlag = true;
            while (decayAlpha > 0f)
            {

                decayAlpha -= 0.1f;


                if (decayCountText && decayBackgroundImage)
                {
                    Color col = decayCountText.color;
                    col.a = decayAlpha;
                    decayCountText.color = col;
                    col = cachedDecayBackgroundColor; // then we can multiply with the non-1 alpha - CP
                    col.a *= decayAlpha;
                    decayBackgroundImage.color = col;
                }

                yield return new WaitForSeconds(0.03f);

            }

            _hidingDecayTimerFlag = false;
        }

        private void UpdateTimerVisuals(float originalDurationOfCurrentElement, float currentDurationRemaining, float interval, bool resaturate)
        {

            if (originalDurationOfCurrentElement <= 0) //this card doesn't decay.
            {
                decayView.gameObject.SetActive(false);
                _originalDuration = 0f;
                _durationRemaining = 0f;
                return;
            }

            _originalDuration = originalDurationOfCurrentElement;
            _durationRemaining = currentDurationRemaining;
            
            if (ApproachingFinalDissolution() || _forceDisplayTimeRemaining)
            {
                if(_hidingDecayTimerFlag && _hidingDecayTimer!=null)
                {
                        StopCoroutine(_hidingDecayTimer);
                        _hidingDecayTimerFlag = false;
                }

                if (decayAlpha<1f && !_showingDecayTimerFlag)
                    _showingDecayTimer = StartCoroutine(ShowingDecayTimer());
            }
            else
            {
                if (_showingDecayTimerFlag && _showingDecayTimer!=null)
                {
                     StopCoroutine(ShowingDecayTimer());
                     _showingDecayTimerFlag = false;
                }
                if (decayAlpha >0f && !_hidingDecayTimerFlag)
                    _hidingDecayTimer = StartCoroutine(HidingDecayTimer());
            }

            //This determines whether the decay view is active, and what the current text value should be.
            //The coroutines determine the current alpha values of the text and the background image
            if (decayAlpha>0f)
            {
                decayView.gameObject.SetActive(true);
                string cardDecayTimeString =
                    Watchman.Get<ILocStringProvider>().GetTimeStringForCurrentLanguage(_durationRemaining);

                decayCountText.text = cardDecayTimeString;
                decayCountText.richText = true;
            }
            else
            decayView.gameObject.SetActive(false);


            float percentageDecayed = 1 - _durationRemaining / _originalDuration;
            percentageDecayed = Mathf.Clamp01(percentageDecayed);
            if (resaturate)
            {
                float reversePercentage = 1f - percentageDecayed;
                artwork.color = new Color(1f - reversePercentage, 1f - reversePercentage, 1f - reversePercentage, 1f);
            }
            else
            {
                artwork.color = new Color(1f - percentageDecayed, 1f - percentageDecayed, 1f - percentageDecayed, 1f);
            }

        }


        private bool IsGlowing()
        {
            if (glowImage == null)
                return false;
            return glowImage.gameObject.activeSelf;
        }

        private void SetGlowColor(Color color)
        {
            if(glowImage.currentColor!=color)

                glowImage.SetColor(color);
        }

        private void SetGlowColor(UIStyle.GlowPurpose purposeType)
        {
            SetGlowColor(UIStyle.GetGlowColor(purposeType,UIStyle.GlowTheme.Classic));
        }

        private void ShowHoverGlow(bool show, bool playSFX = true, Color? hoverColor = null)
        {
            
            if (show)
            {
                if (playSFX)
                    SoundManager.PlaySfx("TokenHover");

                glowImage.SetColor(hoverColor == null ? UIStyle.GetGlowColor(UIStyle.GlowPurpose.OnHover, UIStyle.GlowTheme.Classic) : hoverColor.Value);
                glowImage.Show();

            }
            else
            {
                //if (playSFX)
                //    SoundManager.PlaySfx("TokenHoverOff");
                glowImage.Hide();
            }
        }


        public bool NoPush
        {
            get { return false; }
        }

        public void Unshroud(bool instant)
        {
            if (!instant)
                SoundManager.PlaySfx("CardTurnOver");

            _flipHelper?.Flip(FlipHelper.TargetOrientation.FaceUp,instant);
            
        }

        public void Shroud(bool instant)
        {
            _flipHelper.Flip(FlipHelper.TargetOrientation.FaceDown, instant);
        }



        public void Retire(RetirementVFX retirementVfx,Action callbackOnRetired)
        {
            
            if (retirementVfx == RetirementVFX.CardHide)
            {
                StartCoroutine( FadeCard(this.gameObject.GetComponentInParent<CanvasGroup>(),0.5f));
            }
            else
            {
                // Check if we have an effect
                CardEffectRemove effect;

                if (retirementVfx == RetirementVFX.None || !gameObject.activeInHierarchy)
                    effect = null;
                else
                    effect = InstantiateEffect(retirementVfx.ToString());

                if (effect != null)
                {
                    effect.SetCallbackOnComplete(callbackOnRetired);
                    effect.StartAnim(this.transform);
                }
                else
                {
                    Destroy(gameObject);
                     callbackOnRetired();
                }
            }

            
        }



        private CardEffectRemove InstantiateEffect(string effectName)
        {
            var prefab = Resources.Load("FX/RemoveCard/" + effectName);

            if (prefab == null)
                return null;

            var obj = Instantiate(prefab,this.transform) as GameObject;

            if (obj == null)
                return null;

            return obj.GetComponent<CardEffectRemove>();
        }

        private IEnumerator FadeCard(CanvasGroup canvasGroup, float fadeDuration)
        {
            float time = 0f;

            while (time < fadeDuration)
            {
                time += Time.deltaTime;
                canvasGroup.alpha = 1f - time / fadeDuration;
                yield return null;
            }

            Destroy(gameObject);
        }

        private void SetBackface(string backId)
        {
            Sprite sprite;

            if (string.IsNullOrEmpty(backId))
                sprite = null;
            else
                sprite = ResourcesManager.GetSpriteForCardBack(backId);

            backArtwork.overrideSprite = sprite;
        }

        protected void OnDisable()
        {
            // this resets any animation frames so we don't get stuck when deactivating mid-anim
            ResetIconAnimation();
        }

        public void ResetIconAnimation()
        {
            artwork.overrideSprite = null;
            // we're turning? Just set us to the target
            _flipHelper.FinishFlip();
        }

        public bool CanAnimateIcon()
        {
            return frames.Any();
        }

        public void BeginIconAnimation()
        {
            if (animCoroutine != null)
                StopCoroutine(animCoroutine);
      

            float duration = 0.2f;
            int frameCount = frames.Count;
            int frameIndex = 0;

            animCoroutine = StartCoroutine(DoAnim(duration, frameCount, frameIndex));
        }

        /// <param name="duration">Determines how long the animation runs. Time is spent equally on all frames</param>
        /// <param name="frameCount">How many frames to show. Default is 1</param>
        /// <param name="frameIndex">At which frame to start. Default is 0</param>
        private IEnumerator DoAnim(float duration, int frameCount, int frameIndex)
        {


            float time = 0f;
            int lastSpriteIndex = -1;

            while (time < duration)
            {
                time += Time.deltaTime;
                int spriteIndex;
                if (frameCount == 1)
                    spriteIndex = 0;
                else
                    spriteIndex = Mathf.FloorToInt(time / duration * frameCount);


                if (spriteIndex != lastSpriteIndex)
                {
                    lastSpriteIndex = spriteIndex;
                    if (spriteIndex < frames.Count)
                    {
                        artwork.overrideSprite = frames[spriteIndex];
                    }
                    else
                        artwork.overrideSprite = null;
                }
                yield return null;
            }

            // remove anim
            artwork.overrideSprite = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {

            var tabletopManager = Watchman.Get<Meniscate>();
            if (tabletopManager != null
            ) //eg we might have a face down card on the credits page - in the longer term, of course, this should get interfaced
            {
                if (_flipHelper.CurrentOrientation!=FlipHelper.TargetOrientation.FaceDown)
                    tabletopManager.SetHighlightedElement(_entityId,_quantity);
                else
                    tabletopManager.SetHighlightedElement(null);
            }

            //ExecuteEvents.Execute<IPointerEnterHandler>(transform.parent.gameObject, eventData,
            //    (parentToken, y) => parentToken.OnPointerEnter(eventData));
        }


        public void OnPointerExit(PointerEventData eventData)
        {
            var ttm = Watchman.Get<Meniscate>();
            if (ttm != null)
            {
                Watchman.Get<Meniscate>().SetHighlightedElement(null);
            }

            ExecuteEvents.Execute<IPointerExitHandler>(transform.parent.gameObject, eventData,
                (parentToken, y) => parentToken.OnPointerExit(eventData));
        }



    }
    }
