using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static LobbyPopupAuthoring;

/// <summary>
/// One-shot authoring tool for the lobby account popup (structure from design/계정예시.png, lobby palette).
/// The saved prefab is the source of truth afterwards; the tool refuses to overwrite it.
/// </summary>
public static class AccountPopupBuilder
{
    private const string PrefabPath = "Assets/WorkSpace/USW/Prefabs/UI/AccountPopup.prefab";
    private const string UtilityAssetsPath = "Assets/WorkSpace/USW/Resources/LobbyUtilityAssets.asset";
    private const float Left = 40, Width = 820;

    [MenuItem("Tools/Lobby/Build Account Popup")]
    public static void BuildMenu() => Debug.Log(Build());

    /// <summary>Creates the prefab and links it in LobbyUtilityAssets; returns a summary line.</summary>
    public static string Build()
    {
        if (File.Exists(PrefabPath)) return "AccountPopup.prefab already exists; edit the prefab instead of rebuilding.";
        if (!Prepare()) return "NotoSansKR-Bold SDF not found.";

        var shell = CreateShell("AccountPopup", "계정", new Vector2(900, 740));
        var panel = shell.Panel;

        var codeLabel = Text(panel, "MemberCodeLabel", "회원 코드", 26, Muted, TextAlignmentOptions.MidlineLeft);
        TopLeft(codeLabel.rectTransform, Left + 8, 120, Width, 36);
        var codeRow = Box(panel, "MemberCodeRow", Card); TopLeft(codeRow, Left, 160, Width, 88);
        var code = Text(codeRow, "Code", "", 32, Color.white, TextAlignmentOptions.MidlineLeft); Stretch(code.rectTransform, 28, 0, 110, 0);
        code.enableAutoSizing = true; code.fontSizeMin = 22; code.fontSizeMax = 32;
        var copy = Box(codeRow, "CopyButton", CardFeatured); Place(copy, new Vector2(1, .5f), new Vector2(-48, 0), new Vector2(72, 72));
        var copyButton = copy.gameObject.AddComponent<Button>();
        Place(Picture(copy, "Icon", Icon(RpgIconRoot + "Icon_PictoIcons/256/function_icon_copy.png"), Muted).rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(42, 42));

        Divider(panel, "계정 연동", Left, 280, Width);
        var google = Box(panel, "GoogleButton", Card); TopLeft(google, Left, 336, Width, 112);
        var googleButton = google.gameObject.AddComponent<Button>();
        var logo = Box(google, "LogoCircle", Color.white); Place(logo, new Vector2(0, .5f), new Vector2(62, 0), new Vector2(76, 76));
        Place(Picture(logo, "Logo", Icon(RpgIconRoot + "IconMisc/Icon_Sns_Google.png"), Color.white).rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(50, 50));
        var googleLabel = Text(google, "Label", "Google 연동", 32, Color.white, TextAlignmentOptions.BottomLeft);
        TopLeft(googleLabel.rectTransform, 124, 14, 600, 46);
        var googleStatus = Text(google, "Status", "", 22, Muted, TextAlignmentOptions.TopLeft);
        TopLeft(googleStatus.rectTransform, 124, 62, 620, 36); googleStatus.enableAutoSizing = true; googleStatus.fontSizeMin = 16; googleStatus.fontSizeMax = 22;
        var linked = Picture(google, "LinkedMark", Icon(IconRoot + "Icon_PictoIcons/256/PictoIcon_Check.Png"), Check);
        Place(linked.rectTransform, new Vector2(1, .5f), new Vector2(-52, 0), new Vector2(52, 52)); linked.gameObject.SetActive(false);

        Divider(panel, "알림", Left, 480, Width);
        var push = Box(panel, "PushRow", Card); TopLeft(push, Left, 536, Width, 112);
        Place(Picture(push, "Bell", Icon(IconRoot + "Icon_PictoIcons/256/PictoIcon_Bell.Png"), Muted).rectTransform, new Vector2(0, .5f), new Vector2(62, 0), new Vector2(56, 56));
        var pushTitle = Text(push, "Title", "푸시 알림", 32, Color.white, TextAlignmentOptions.BottomLeft);
        TopLeft(pushTitle.rectTransform, 124, 14, 480, 46);
        var pushStatus = Text(push, "Status", "", 22, Muted, TextAlignmentOptions.TopLeft);
        TopLeft(pushStatus.rectTransform, 124, 62, 500, 36); pushStatus.enableAutoSizing = true; pushStatus.fontSizeMin = 14; pushStatus.fontSizeMax = 22;
        var toggle = Box(push, "Toggle", Card); Place(toggle, new Vector2(1, .5f), new Vector2(-96, 0), new Vector2(140, 64));
        var toggleButton = toggle.gameObject.AddComponent<Button>();
        var toggleLabel = Text(toggle, "Label", "OFF", 28, Muted, TextAlignmentOptions.Center); Stretch(toggleLabel.rectTransform);

        var sync = Box(panel, "SyncRow", new Color(.30f, .18f, .26f, 1)); TopLeft(sync, Left, 740, Width, 100);
        var syncStatus = Text(sync, "Status", "", 24, StatusColor, TextAlignmentOptions.MidlineLeft); Stretch(syncStatus.rectTransform, 24, 0, 380, 0);
        syncStatus.textWrappingMode = TextWrappingModes.Normal; syncStatus.enableAutoSizing = true; syncStatus.fontSizeMin = 16; syncStatus.fontSizeMax = 24;
        var retry = SmallButton(sync, "RetrySave", "다시 저장", new Vector2(-276, 0));
        var useServer = SmallButton(sync, "UseServer", "서버 기록 사용", new Vector2(-96, 0));
        sync.gameObject.SetActive(false);

        var message = Paragraph(panel, "Message", 26, StatusColor, Left, 668, Width, 56);

        var popup = shell.Root.AddComponent<AccountPopup>();
        var so = new SerializedObject(popup);
        BindBase(so, shell);
        so.FindProperty("_panel").objectReferenceValue = panel;
        so.FindProperty("_memberCode").objectReferenceValue = code;
        so.FindProperty("_copy").objectReferenceValue = copyButton;
        so.FindProperty("_google").objectReferenceValue = googleButton;
        so.FindProperty("_googleLabel").objectReferenceValue = googleLabel;
        so.FindProperty("_googleStatus").objectReferenceValue = googleStatus;
        so.FindProperty("_googleLinkedMark").objectReferenceValue = linked.gameObject;
        so.FindProperty("_pushToggle").objectReferenceValue = toggleButton;
        so.FindProperty("_pushToggleBackground").objectReferenceValue = toggle.GetComponent<Image>();
        so.FindProperty("_pushToggleLabel").objectReferenceValue = toggleLabel;
        so.FindProperty("_pushStatus").objectReferenceValue = pushStatus;
        so.FindProperty("_syncRow").objectReferenceValue = sync.gameObject;
        so.FindProperty("_syncStatus").objectReferenceValue = syncStatus;
        so.FindProperty("_retrySave").objectReferenceValue = retry;
        so.FindProperty("_useServer").objectReferenceValue = useServer;
        so.FindProperty("_message").objectReferenceValue = message;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        var prefab = PrefabUtility.SaveAsPrefabAsset(shell.Root, PrefabPath);
        Object.DestroyImmediate(shell.Root);

        var utility = AssetDatabase.LoadAssetAtPath<LobbyUtilityAssets>(UtilityAssetsPath);
        if (utility != null) { utility.AccountPrefab = prefab.GetComponent<AccountPopup>(); EditorUtility.SetDirty(utility); }
        AssetDatabase.SaveAssets();
        return "Built " + PrefabPath + (utility != null ? " and linked LobbyUtilityAssets." : " (LobbyUtilityAssets not found).");
    }

    private static Button SmallButton(RectTransform parent, string name, string label, Vector2 position)
    {
        var rect = Box(parent, name, CardFeatured); Place(rect, new Vector2(1, .5f), position, new Vector2(168, 64));
        var text = Text(rect, "Label", label, 24, Color.white, TextAlignmentOptions.Center); Stretch(text.rectTransform, 8, 0, 8, 0);
        text.enableAutoSizing = true; text.fontSizeMin = 16; text.fontSizeMax = 24;
        return rect.gameObject.AddComponent<Button>();
    }
}
