using GaeGGUL.Animation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared editor helpers for lobby popup prefabs (attendance, account): one palette, font and shell
/// so every utility popup looks consistent with the lobby menu sheet.
/// </summary>
public static class LobbyPopupAuthoring
{
    public const string IconRoot = "Assets/Imports/Layer Lab/GUI Pro-FantasyHero/ResourcesData/Sptites/Components/";
    public const string RpgIconRoot = "Assets/Imports/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/Sprites/Component/";
    public const int SortingOrder = 120;

    public static readonly Color Sheet = new(.12f, .10f, .21f, 1);
    public static readonly Color Header = new(.20f, .16f, .33f, 1);
    public static readonly Color Card = new(.20f, .16f, .33f, 1);
    public static readonly Color CardFeatured = new(.27f, .19f, .42f, 1);
    public static readonly Color Accent = new(1f, .78f, .30f, 1);
    public static readonly Color StatusColor = new(1f, .86f, .48f, 1);
    public static readonly Color Muted = new(.73f, .70f, .86f, 1);
    public static readonly Color Check = new(.55f, 1f, .78f, 1);
    public static readonly Color Line = new(.32f, .27f, .48f, 1);

    private static Sprite _round;
    private static TMP_FontAsset _font;

    /// <summary>Root, dim, panel, header and close button of one popup.</summary>
    public struct Shell
    {
        public GameObject Root;
        public RectTransform Panel;
        public TextMeshProUGUI Title;
        public Button Close;
        public Button Dim;
        public Anim_UI_Scale Anim;
    }

    /// <summary>Loads the shared font and sprite; false when the project font is missing.</summary>
    public static bool Prepare()
    {
        _round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        _font = null;
        foreach (var guid in AssetDatabase.FindAssets("NotoSansKR-Bold SDF t:TMP_FontAsset"))
        { _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid)); if (_font != null) break; }
        return _font != null;
    }

    /// <summary>Creates a full-screen popup with a centered panel of the given size.</summary>
    public static Shell CreateShell(string name, string title, Vector2 panelSize)
    {
        var shell = new Shell();
        shell.Root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        Stretch((RectTransform)shell.Root.transform);
        var canvas = shell.Root.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = SortingOrder;

        var dim = Box(shell.Root.transform, "Dim", new Color(0, 0, 0, .62f), sliced: false); Stretch(dim);
        shell.Dim = dim.gameObject.AddComponent<Button>(); shell.Dim.transition = Selectable.Transition.None;

        shell.Panel = Box(shell.Root.transform, "Panel", Sheet);
        Place(shell.Panel, new Vector2(.5f, .5f), Vector2.zero, panelSize);

        var header = Box(shell.Panel, "Header", Header);
        header.anchorMin = new Vector2(0, 1); header.anchorMax = Vector2.one; header.pivot = new Vector2(.5f, 1);
        header.anchoredPosition = Vector2.zero; header.sizeDelta = new Vector2(0, 96);
        shell.Title = Text(header, "Title", title, 44, Color.white, TextAlignmentOptions.Center); Stretch(shell.Title.rectTransform);

        var close = Box(header, "CloseButton", Card);
        Place(close, new Vector2(1, .5f), new Vector2(-52, 0), new Vector2(72, 72));
        shell.Close = close.gameObject.AddComponent<Button>();
        Stretch(Text(close, "Label", "X", 36, Muted, TextAlignmentOptions.Center).rectTransform);

        shell.Anim = shell.Root.AddComponent<Anim_UI_Scale>();
        var so = new SerializedObject(shell.Anim);
        so.FindProperty("animationTarget").objectReferenceValue = shell.Panel;
        so.FindProperty("triggerType").enumValueIndex = (int)AnimTriggerType.None;
        so.FindProperty("startMultiplier").vector2Value = new Vector2(.86f, .86f);
        so.FindProperty("endMultiplier").vector2Value = new Vector2(.92f, .92f);
        so.ApplyModifiedPropertiesWithoutUndo();
        return shell;
    }

    /// <summary>Assigns UI_Base close/background/animation fields.</summary>
    public static void BindBase(SerializedObject popup, Shell shell)
    {
        popup.FindProperty("btn_Close").objectReferenceValue = shell.Close;
        popup.FindProperty("btn_BackgroundClose").objectReferenceValue = shell.Dim;
        popup.FindProperty("targetAnim").objectReferenceValue = shell.Anim;
    }

    /// <summary>Centered section label with a thin line on each side.</summary>
    public static void Divider(RectTransform parent, string label, float x, float y, float width)
    {
        var text = Text(parent, "Divider_" + label, label, 26, Muted, TextAlignmentOptions.Center);
        TopLeft(text.rectTransform, x + width * .3f, y, width * .4f, 40);
        var left = Box(parent, "LineL", Line, sliced: false); TopLeft(left, x, y + 19, width * .28f, 2);
        var right = Box(parent, "LineR", Line, sliced: false); TopLeft(right, x + width * .72f, y + 19, width * .28f, 2);
    }

    public static Sprite Icon(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    public static RectTransform Box(Transform parent, string name, Color color, bool sliced = true)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var image = rect.GetComponent<Image>(); image.color = color;
        if (sliced) { image.sprite = _round; image.type = Image.Type.Sliced; }
        return rect;
    }

    public static Image Picture(Transform parent, string name, Sprite sprite, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false); image.sprite = sprite; image.color = color; image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions align)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false); text.font = _font; text.text = value; text.fontSize = size; text.color = color;
        text.alignment = align; text.raycastTarget = false; text.richText = false; text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    /// <summary>Wrapping text that shrinks to fit its box.</summary>
    public static TextMeshProUGUI Paragraph(Transform parent, string name, float size, Color color, float x, float y, float width, float height)
    {
        var text = Text(parent, name, "", size, color, TextAlignmentOptions.MidlineLeft);
        TopLeft(text.rectTransform, x, y, width, height);
        text.textWrappingMode = TextWrappingModes.Normal; text.enableAutoSizing = true; text.fontSizeMin = size * .75f; text.fontSizeMax = size;
        return text;
    }

    public static void Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; }

    public static void TopLeft(RectTransform rect, float x, float y, float width, float height)
    { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); }
}
