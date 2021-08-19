﻿using UnityEngine;
using System.Collections;
using SecretHistories.Entities;
using SecretHistories.Enums;
using SecretHistories.UI;
using SecretHistories.Fucine;
using SecretHistories.Services;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace SecretHistories.Constants {
    public class EndGameAnimController : MonoBehaviour {
#pragma warning disable 649
        [SerializeField] Vector2 targetPosOffset = new Vector2(0f, -150f);

        [Header("Controllers")]
        [SerializeField] private TabletopManager _tabletopManager;
        [SerializeField] private SpeedControlUI _speedControlUi;
        [SerializeField] private UIController _uiController;
        
        [Header("Visuals")]
        [SerializeField] private Canvas tableCanvas;
        [SerializeField] private ScrollRect tableScroll;
        [SerializeField] private Canvas menuCanvas;

#pragma warning restore 649

        bool isEnding = false;
        public void TriggerEnd(RectTransform focusOnTransform, Ending ending) {
            if (isEnding)
                return;

            isEnding = true;
            StartCoroutine(DoEndGameAnim(focusOnTransform, ending));
        }

        IEnumerator DoEndGameAnim(RectTransform focusOnTransform, Ending ending) {
            const float zoomDuration = 5f;
            const float fadeDuration = 2f;

            // disable all input
            GraphicRaycaster rayCaster;
            rayCaster = tableCanvas.GetComponent<GraphicRaycaster>();
            rayCaster.enabled = false; // Disable clicks on tabletop

            rayCaster = menuCanvas.GetComponent<GraphicRaycaster>();
            rayCaster.enabled = false; // Disable clicks on Screen

			//cameraZoom.cameraZoomEnabled = false;
            _uiController.enabled = false; // Disable shortcuts

            // pause game
          Watchman.Get<LocalNexus>().SpeedControlEvent.Invoke(new SpeedControlEventArgs { ControlPriorityLevel =3 , GameSpeed = GameSpeed.Paused, WithSFX =false });


           tableScroll.StopMovement(); // make sure the scroll rect stops
			tableScroll.movementType = ScrollRect.MovementType.Unrestricted; // this allows us to leave the boundaries on the anim in case our token is at the table edges
            _tabletopManager.CloseAllSituationWindowsExcept(null); // no window has an id of NULL, so all close

            // TODO: play death effect / music

            // Start hiding all tokens
            RetireAllStacks(RetirementVFX.CardBurn);

            // (Spawn specific effect based on token, depending on end-game-type)
            InstantiateEffect(ending, focusOnTransform);

            
            float time = 0f;
            Vector2 startPos = tableScroll.content.anchoredPosition;
            Vector2 targetPos = -1f * focusOnTransform.anchoredPosition + targetPosOffset;
            // ^ WARNING: targetPosOffset fixes the difference between the scrollable and tokenParent rect sizes 

            Debug.Log("Target Zoom Pos " + targetPos);


           // cameraZoom.StartFixedZoom(0f, zoomDuration);

            var menuBarCanvasGrp = menuCanvas.GetComponent<CanvasGroup>();


            while (time < zoomDuration && !_uiController.IsPressingAbortHotkey()) {
                menuBarCanvasGrp.alpha = 1f - time; // remove lower button bar.
                tableScroll.content.anchoredPosition = Vector2.Lerp(startPos, targetPos, Easing.Circular.Out((time / zoomDuration)));
                yield return null;
                time += Time.deltaTime;
            }

            // automatically jumps here on Abort - NOTE: At the moment this auto-focuses the token, but that's okay, it's important info
            tableScroll.content.anchoredPosition = targetPos;


            menuBarCanvasGrp.alpha = 0f;
            

            // TODO: Put the fade into the while loop so that on aborting the zoom still continues
            Watchman.Get<TabletopFadeOverlay>().FadeToBlack(fadeDuration);
            yield return new WaitForSeconds(fadeDuration);

            Watchman.Get<StageHand>().EndingScreen();

        }

        GameObject InstantiateEffect(Ending ending, Transform token) {

            string effectName;

            if (string.IsNullOrEmpty(ending.Anim))
                effectName = "DramaticLight";
            else
                effectName = ending.Anim;


            var prefab = Resources.Load("FX/EndGame/" + effectName);

            if (prefab == null)
                return null;

            var go = Instantiate(prefab, token) as GameObject;
            go.transform.position = token.position;
            go.transform.localScale = Vector3.one;
            go.SetActive(true);

            var effect = go.GetComponent<CardEffect>();

            //AK temporarily commented out to fix build
            if (effect != null)
                effect.StartAnim(token);

            return go;
        }

        void RetireAllStacks(RetirementVFX anim) {
            var stacks = _tabletopManager._tabletop.GetElementTokens();

            foreach (var item in stacks)
                item.Retire(anim);
        }


    }
}
