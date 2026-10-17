using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Owned rounded UI skin shared by the lobby header and its utility menu.</summary>
public sealed class LobbyUtilityVisuals : IDisposable
{
    /// <summary>Soft violet surface matching the lobby.</summary>
    public static readonly Color Surface = new(.17f, .14f, .29f, 1);
    /// <summary>Muted label color.</summary>
    public static readonly Color Muted = new(.73f, .70f, .86f, 1);
    /// <summary>Shared lobby popup background.</summary>
    public static readonly Color Sheet = new(.12f, .10f, .21f, 1);
    /// <summary>Shared lobby section and card background.</summary>
    public static readonly Color Card = new(.20f, .16f, .33f, 1);
    /// <summary>Selected or featured lobby card.</summary>
    public static readonly Color CardFeatured = new(.27f, .19f, .42f, 1);
    /// <summary>Primary action and progression accent.</summary>
    public static readonly Color Accent = new(1f, .78f, .30f, 1);
    /// <summary>Warm secondary status text.</summary>
    public static readonly Color Status = new(1f, .86f, .48f, 1);
    /// <summary>Thin separators and panel borders.</summary>
    public static readonly Color Line = new(.32f, .27f, .48f, 1);
    /// <summary>Positive completion state.</summary>
    public static readonly Color Check = new(.55f, 1f, .78f, 1);
    private readonly Texture2D _texture;
    private readonly Sprite _rounded;
    private readonly TMP_FontAsset _font;

    /// <summary>Creates one small shared sliced sprite; it is released with the header.</summary>
    public LobbyUtilityVisuals(TMP_FontAsset font)
    {
        _font = font;
        const int size = 32;
        _texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "LobbyUtilityRound", hideFlags = HideFlags.DontSave };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var center = new Vector2(Mathf.Clamp(x + .5f, 9, 23), Mathf.Clamp(y + .5f, 9, 23));
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), center);
                _texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(9.5f - distance)));
            }
        _texture.Apply();
        _rounded = Sprite.Create(_texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, new Vector4(9, 9, 9, 9));
        _rounded.name = "LobbyUtilityRound"; _rounded.hideFlags = HideFlags.DontSave;
    }
    /// <summary>Creates a rounded panel.</summary>
    public RectTransform Box(Transform parent, string name, Color color)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); var graphic = rect.GetComponent<Image>(); graphic.sprite = _rounded; graphic.type = Image.Type.Sliced; graphic.color = color;
        return rect;
    }
    /// <summary>Creates plain text without rich-text interpretation.</summary>
    public TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(parent, false); text.font = _font; text.text = value; text.fontSize = size; text.color = color;
        text.raycastTarget = false; text.richText = false; text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = true; text.fontSizeMax = size; text.fontSizeMin = size * .8f;
        text.alignment = TextAlignmentOptions.MidlineLeft; return text;
    }
    /// <summary>Creates one touch target.</summary>
    public Button Button(Transform parent, string name, string label, Action action)
    {
        var rect = Box(parent, name, Surface);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
        if (action != null) button.onClick.AddListener(() => action());
        var text = Text(rect, "Label", label, 30, Color.white); Fill(text.rectTransform, 24, 0, 24, 0);
        return button;
    }
    /// <summary>Stretches inside its parent with margins.</summary>
    public static void Fill(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
    /// <summary>Places a top-left rectangle in canvas units.</summary>
    public static void Place(RectTransform rect, float x, float y, float width, float height)
    { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); rect.localScale = Vector3.one; }
    /// <inheritdoc />
    public void Dispose() { UnityEngine.Object.Destroy(_rounded); UnityEngine.Object.Destroy(_texture); }
}
