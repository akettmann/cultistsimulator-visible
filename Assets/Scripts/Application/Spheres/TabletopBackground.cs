﻿using SecretHistories.Entities;
using SecretHistories.Constants;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SecretHistories.UI
{
    public class TabletopBackground : MonoBehaviour, IPointerClickHandler {

        public event System.Action<PointerEventData> onClicked;

#pragma warning disable 649
        [SerializeField] private Image Cover;
            [SerializeField] Image Surface;
        [SerializeField] Image Edge;
#pragma warning restore 649


        public void Awake()
        {
            var w=new Watchman();
            w.Register(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            onClicked?.Invoke(eventData);
        }

        public void ShowTabletopFor(Legacy characterActiveLegacy)
        {

            if (!string.IsNullOrEmpty(characterActiveLegacy.TableCoverImage))
            {
                var coverImage = ResourcesManager.GetSpriteForUI(characterActiveLegacy.TableCoverImage);
                Cover.sprite = coverImage;
            }

            if (!string.IsNullOrEmpty(characterActiveLegacy.TableSurfaceImage))
            {
                var surfaceImage = ResourcesManager.GetSpriteForUI(characterActiveLegacy.TableSurfaceImage);
                Surface.sprite = surfaceImage;
            }


            if (!string.IsNullOrEmpty(characterActiveLegacy.TableEdgeImage))
            {
                var edgeImage = ResourcesManager.GetSpriteForUI(characterActiveLegacy.TableEdgeImage);
                Edge.sprite = edgeImage;
            }

        }

    }
}
