﻿using System.Collections;
using SecretHistories.Abstract;
using SecretHistories.Commands;
using SecretHistories.Entities;
using SecretHistories.Manifestations;
using SecretHistories.Services;
using SecretHistories.Spheres;
using SecretHistories.UI;
using UnityEngine;

namespace SecretHistories.Ghosts
{
    public abstract class AbstractGhost: MonoBehaviour,IGhost
    {
        
        protected RectTransform rectTransform;
        private Sphere _projectedInSphere;
        [SerializeField] protected CanvasGroupFader canvasGroupFader;

        private Coroutine _travelCoroutine;


        public void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        public bool Visible => canvasGroupFader.IsVisible();

        public virtual void ShowAt(Sphere projectInSphere, Vector3 showAtAnchoredPosition3D)
        {

            if (projectInSphere==_projectedInSphere && rectTransform.anchoredPosition3D!=showAtAnchoredPosition3D) //do a smooth transition if moving in the same projected sphere and position not already identical
                AnimateGhostMovement(rectTransform.anchoredPosition3D,showAtAnchoredPosition3D);
            else
            {
                rectTransform.SetParent(projectInSphere.GetRectTransform());
            rectTransform.anchoredPosition3D = showAtAnchoredPosition3D;
            canvasGroupFader.Show();
            _projectedInSphere = projectInSphere;
            }
        }

        private void AnimateGhostMovement(Vector3 startPosition,Vector3 endPosition)
        {
            if(_travelCoroutine!=null)
                StopCoroutine(_travelCoroutine);

            _travelCoroutine = StartCoroutine(GhostMovingTo(startPosition, endPosition));
        }

        private IEnumerator GhostMovingTo(Vector3 startPosition, Vector3 endPosition)
        {
            float _travelDuration = 0.2f;
            float _elapsed = 0f;
            while (rectTransform.anchoredPosition3D != endPosition)
            {
                _elapsed += Time.deltaTime;
                float completion = _elapsed / _travelDuration;
                var lerpValue= Easing.Quartic.Out(completion);
                rectTransform.anchoredPosition3D = new Vector3(Mathf.Lerp(startPosition.x, endPosition.x, lerpValue), Mathf.Lerp(startPosition.y, endPosition.y, lerpValue), endPosition.z);
            yield return null;
            }
        }

        public virtual void HideIn(Token forToken)
        {
            canvasGroupFader.HideImmediately(); //ghost behaviour is determined by whether it's visible or not. So when we hide it, we mean hide immediately.
            if(rectTransform!=null)
                rectTransform.SetParent(forToken.TokenRectTransform); //so it doesn't clutter up the hierarchy
            _projectedInSphere = null;

        }

        public bool PromiseBlocksCandidateRect(Sphere sphere, Rect candidateRect)
        {
            if (!Visible)
                return false; //invisible ghosts never block nuthin
            if (sphere != _projectedInSphere)
                return false;
            if (!GetRect().Overlaps(candidateRect))
                return false;

            return true;

        }

        public Rect GetRect()
        {
            var rt = gameObject.GetComponent<RectTransform>();
            if(rectTransform==null)
                return new Rect(0,0,0,0);

            return rt.rect;
        }


        public bool TryFulfilPromise(Token token,Context context)
        {
            if (!Visible)
                return false; //if the ghost isn't active, there's no promise to fulfill.

        //otherwise, we did show the ghost, so we'd better be ready to make good on it.
            TokenTravelItinerary travellingToGhost =
                new TokenTravelItinerary(token.TokenRectTransform.anchoredPosition3D, rectTransform.anchoredPosition3D)
                    .WithDuration(Watchman.Get<Compendium>().GetSingleEntity<Dictum>().DefaultQuickTravelDuration);

            travellingToGhost.Depart(token, context);

            HideIn(token); //now clean up the ghost

            //and say that we've fulfilled the promise
            return true;
        }



        public virtual void Retire()
        {
                Destroy(gameObject);
        }

    }
}