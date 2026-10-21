using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static LobbyPopupAuthoring;

/// <summary>
/// One-shot authoring tool: writes the attendance config and popup prefab, then links it in LobbyUtilityAssets.
/// The saved prefab is the source of truth afterwards; the tool refuses to overwrite an existing prefab.
/// </summary>
public static class AttendancePopupBuilder
{
    private const string ConfigPath = "Assets/WorkSpace/USW/Resources/AttendanceConfig.asset";
    private const string PrefabFolder = "Assets/WorkSpace/USW/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/AttendancePopup.prefab";
    private const string UtilityAssetsPath = "Assets/WorkSpace/USW/Resources/LobbyUtilityAssets.asset";

    [MenuItem("Tools/Lobby/Build Attendance Popup")]
    public static void BuildMenu() => Debug.Log(Build());

    /// <summary>Creates missing assets; returns a summary line.</summary>
    public static string Build()
    {
        var config = AssetDatabase.LoadAssetAtPath<AttendanceConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<AttendanceConfig>();
            // Trial values pending user approval (see docs/technical/attendance.md).
            config.Rewards = new[]
            {
                R(MailRewardConfig.Currency.Stamina, 10), R(MailRewardConfig.Currency.Gold, 500), R(MailRewardConfig.Currency.Diamond, 10),
                R(MailRewardConfig.Currency.Stamina, 15), R(MailRewardConfig.Currency.Gold, 1000), R(MailRewardConfig.Currency.Diamond, 20),
                R(MailRewardConfig.Currency.Diamond, 50)
            };
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        if (File.Exists(PrefabPath)) return "AttendancePopup.prefab already exists; edit the prefab instead of rebuilding.";
        if (!Prepare()) return "NotoSansKR-Bold SDF not found.";

        var shell = CreateShell("AttendancePopup", "7일 출석 보상", new Vector2(960, 820));
        var panel = shell.Panel;
        var status = Paragraph(panel, "Status", 28, StatusColor, 32, 104, 896, 80);

        var slots = new AttendanceDaySlot[AttendanceConfig.CycleDays];
        for (int i = 0; i < 6; i++)
            slots[i] = Slot(panel, i + 1, 32 + (i % 3) * 212, 196 + (i / 3) * 256, new Vector2(196, 240), 112, 30, false);
        slots[6] = Slot(panel, 7, 672, 196, new Vector2(256, 496), 190, 42, true);

        var footer = Paragraph(panel, "Footer", 24, Muted, 32, 712, 896, 80);

        var popup = shell.Root.AddComponent<AttendancePopup>();
        var so = new SerializedObject(popup);
        BindBase(so, shell);
        so.FindProperty("_config").objectReferenceValue = config;
        so.FindProperty("_title").objectReferenceValue = shell.Title;
        so.FindProperty("_status").objectReferenceValue = status;
        so.FindProperty("_footer").objectReferenceValue = footer;
        so.FindProperty("_goldIcon").objectReferenceValue = Icon(IconRoot + "Icon_ItemIcons/256/ItemIcon_Coin_Gold.png");
        so.FindProperty("_diamondIcon").objectReferenceValue = Icon(IconRoot + "Icon_ItemIcons/256/ItemIcon_Gem_Diamond_Blue.png");
        so.FindProperty("_staminaIcon").objectReferenceValue = Icon(IconRoot + "Icon_ItemIcons/256/ItemIcon_Energy_Blue.png");
        var slotArray = so.FindProperty("_slots"); slotArray.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++) slotArray.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(PrefabFolder);
        var prefab = PrefabUtility.SaveAsPrefabAsset(shell.Root, PrefabPath);
        Object.DestroyImmediate(shell.Root);

        var utility = AssetDatabase.LoadAssetAtPath<LobbyUtilityAssets>(UtilityAssetsPath);
        if (utility != null) { utility.AttendancePrefab = prefab.GetComponent<AttendancePopup>(); EditorUtility.SetDirty(utility); }
        AssetDatabase.SaveAssets();
        return "Built " + PrefabPath + (utility != null ? " and linked LobbyUtilityAssets." : " (LobbyUtilityAssets not found).");
    }

    private static AttendanceDaySlot Slot(RectTransform parent, int day, float x, float y, Vector2 size, float icon, float amountSize, bool featured)
    {
        var root = new GameObject("Day" + day, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        root.SetParent(parent, false); TopLeft(root, x, y, size.x, size.y);
        var hit = root.GetComponent<Image>(); hit.color = new Color(1, 1, 1, 0);
        var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = hit; button.transition = Selectable.Transition.None;

        const float pill = 52, gap = 8;
        var glow = Box(root, "Glow", Accent); Stretch(glow, -6, -6, -6, pill + gap - 6); glow.GetComponent<Image>().raycastTarget = false;
        var card = Box(root, "Card", featured ? CardFeatured : Card); Stretch(card, 0, 0, 0, pill + gap);
        var iconImage = Picture(card, "Icon", null, Color.white);
        Place(iconImage.rectTransform, new Vector2(.5f, .5f), new Vector2(0, 6), new Vector2(icon, icon));
        var amount = Text(card, "Amount", "x0", amountSize, Color.white, TextAlignmentOptions.BottomRight);
        Stretch(amount.rectTransform, 10, 8, 14, 8);
        amount.outlineWidth = .2f; amount.outlineColor = new Color32(20, 12, 36, 255);

        var dayPill = Box(root, "DayPill", Card);
        dayPill.anchorMin = Vector2.zero; dayPill.anchorMax = new Vector2(1, 0); dayPill.pivot = new Vector2(.5f, 0);
        dayPill.anchoredPosition = Vector2.zero; dayPill.sizeDelta = new Vector2(featured ? -40 : -24, pill);
        var label = Text(dayPill, "Label", day + "일차", 26, Muted, TextAlignmentOptions.Center); Stretch(label.rectTransform);

        var claimed = Box(card, "Claimed", new Color(0, 0, 0, .58f)); Stretch(claimed);
        var check = Picture(claimed, "Check", Icon(IconRoot + "Icon_PictoIcons/256/PictoIcon_Check.Png"), Check);
        Place(check.rectTransform, new Vector2(.5f, .5f), Vector2.zero, Vector2.one * (featured ? 140 : 96));
        claimed.GetComponent<Image>().raycastTarget = false; claimed.gameObject.SetActive(false);
        glow.gameObject.SetActive(false);

        var slot = root.gameObject.AddComponent<AttendanceDaySlot>();
        var so = new SerializedObject(slot);
        so.FindProperty("_button").objectReferenceValue = button;
        so.FindProperty("_icon").objectReferenceValue = iconImage;
        so.FindProperty("_amount").objectReferenceValue = amount;
        so.FindProperty("_dayPill").objectReferenceValue = dayPill.GetComponent<Image>();
        so.FindProperty("_dayLabel").objectReferenceValue = label;
        so.FindProperty("_claimedMark").objectReferenceValue = claimed.gameObject;
        so.FindProperty("_glow").objectReferenceValue = glow.GetComponent<Image>();
        so.ApplyModifiedPropertiesWithoutUndo();
        return slot;
    }

    private static AttendanceConfig.Reward R(MailRewardConfig.Currency currency, int amount) => new() { Currency = currency, Amount = amount };
}
