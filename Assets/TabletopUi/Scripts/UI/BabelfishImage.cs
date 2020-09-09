﻿using Assets.Core.Entities;
using Assets.CS.TabletopUI;
using Assets.TabletopUi.Scripts.Services;
using Noon;
using OrbCreationExtensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Localization responder - assumes a singleton centralized manager class (LanguageManager{})
/// that maintains fields for the font assets for different language sets being parsed
/// (in this case: CJK, RU, and EN for everything else).
///
/// It is expected the LanguageManager defines an event used to indicate a language change has occurred.
///
/// This script switches a UI image according to the current language


public class BabelfishImage : MonoBehaviour
{
    [Tooltip("Custom images per language")]
    #pragma warning disable 649
    [SerializeField] private bool			            usesOverride;
#pragma warning restore 649

	private Image image;

    private void Start()
    {
		image = gameObject.GetComponent<Image>() as Image;

        string currentCultureId = Registry.Get<Config>().CultureId;

        var currentCulture = Registry.Get<ICompendium>().GetEntityById<Culture>(currentCultureId);

        DisplayImageForCulture(currentCulture);

    }

    private void DisplayImageForCulture(Culture culture)
    {
        if (usesOverride)
        {
            image.overrideSprite =
                ResourcesManager.GetSpriteLocalised("ui", image.sprite.name, culture.Id);
            // image.overrideSprite = sprites[i].sprite;
        }
        else
        {
            image.sprite =
                ResourcesManager.GetSpriteLocalised("ui", image.sprite.name, culture.Id);

            //image.sprite = sprites[i].sprite;
        }
        return;
    }

    private void OnEnable()
    {
        Registry.Get<Concursum>().CultureChangedEvent.AddListener(OnCultureChanged);
        
    }

    private void OnDisable()
    {
        Registry.Get<Concursum>().CultureChangedEvent.RemoveListener(OnCultureChanged);

    }

    public virtual void OnCultureChanged(CultureChangedArgs args)
    {
        DisplayImageForCulture(args.NewCulture);

        
    }

}
