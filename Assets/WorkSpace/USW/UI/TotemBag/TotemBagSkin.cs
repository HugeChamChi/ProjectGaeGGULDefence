using TMPro;
using UnityEngine;

/// <summary>KHJ_Artwork sprites and fonts used by <see cref="TotemBagPopup"/>. Panels and cards are generated rounded boxes, so nothing is stretched.</summary>
[CreateAssetMenu(menuName = "GGD/UI/Totem Bag Skin", fileName = "TotemBagSkin")]
public sealed class TotemBagSkin : ScriptableObject
{
    [Header("Panel")]
    public Sprite TitleBanner;
    public Sprite CloseButton;

    [Header("Cells")]
    [Tooltip("Normal, Rare, Epic, Legend")]
    public Sprite[] UnitFrames = new Sprite[4];
    public Sprite TotemFrame;
    public Sprite LevelBadge;

    [Header("Info")]

    [Header("Fonts")]
    public TMP_FontAsset TitleFont;
    public TMP_FontAsset BodyFont;
    [Tooltip("Info card titles. Dark text on cream; the title font's baked outline smears there.")]
    public TMP_FontAsset HeadingFont;
    public TMP_FontAsset NumberFont;

    [Header("Display")]
    [Tooltip("Shown after the current level, e.g. 7/15. Matches the last authored level effect.")]
    [Min(1)] public int DisplayMaxLevel = 15;
}
