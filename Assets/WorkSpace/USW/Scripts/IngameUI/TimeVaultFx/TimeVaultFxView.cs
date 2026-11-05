using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시간 금고 유예 연출(시스템 오버라이드)의 UI 요소를 만들고 들고 있는다. TimeVaultFx가 처음 쓸 때 한 번 만든다.
/// 좌표는 1080x1920 기준 캔버스 단위. 남은 시간 숫자는 인게임 타이머가 보여 주므로 만들지 않는다.
/// 배지·차단 띠의 위치와 크기는 TimeVaultFx가 실제 HUD에 맞춘다.
/// </summary>
public sealed class TimeVaultFxView
{
    private const float TerminalY = 360f, TitleY = 60f, TitleSize = 200f;
    private const float CopyOffset = 12f;
    private const float WideText = 1000f, SidePadding = 40f, MinFontScale = 0.4f;

    public readonly Image Overlay;
    public readonly Material OverlayMaterial;
    public readonly CanvasGroup TerminalGroup;
    public readonly TextMeshProUGUI[] TerminalLines;
    public readonly CanvasGroup TitleGroup;
    public readonly TextMeshProUGUI[] Title = new TextMeshProUGUI[3];
    public readonly RectTransform Deny;
    public readonly CanvasGroup DenyGroup;
    public readonly RectTransform Badge;
    public readonly CanvasGroup BadgeGroup;

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    /// <summary>root 아래에 모든 요소를 만든다.</summary>
    public TimeVaultFxView(RectTransform root, TimeVaultFxSettings s)
    {
        var font = s.DisplayFont;
        var red = s.OverrideColor;

        // 오버레이 (맨 아래)
        Overlay = Stretch("Overlay", root).gameObject.AddComponent<Image>();
        Overlay.raycastTarget = false;
        OverlayMaterial = s.OverlayMaterial != null ? new Material(s.OverlayMaterial) : null;
        Overlay.material = OverlayMaterial;

        // 발동: 터미널 4줄 → OVERRIDE
        var terminal = NewImage("Terminal", root, new Vector2(0f, TerminalY), new Vector2(960f, 320f), s.PanelColor);
        AddOutline(terminal.gameObject, WithAlpha(red, 0.45f), 3f);
        TerminalGroup = terminal.gameObject.AddComponent<CanvasGroup>();
        int lineCount = s.TerminalLines != null ? s.TerminalLines.Length : 0;
        TerminalLines = new TextMeshProUGUI[lineCount];
        for (int i = 0; i < lineCount; i++)
        {
            var line = NewText("Line" + i, terminal.rectTransform, new Vector2(0f, 105f - i * 70f), new Vector2(900f, 64f), font, 44f, new Color(1f, 0.32f, 0.38f), s.TerminalLines[i]);
            line.alignment = TextAlignmentOptions.Left;
            TerminalLines[i] = line;
        }

        var titleRoot = Stretch("Title", root);
        TitleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
        var box = new Vector2(1060f, TitleSize * 1.4f);
        var pos = new Vector2(0f, TitleY);
        // [0] = 본문, [1] = 왼쪽 청록 복제, [2] = 오른쪽 흰 복제. 복제를 먼저 그려 본문이 위에 오게 한다.
        Title[1] = NewText("CopyL", titleRoot, pos + new Vector2(-CopyOffset, 0f), box, font, TitleSize, WithAlpha(s.GlitchCyan, 0.8f), s.OverrideTitle);
        Title[2] = NewText("CopyR", titleRoot, pos + new Vector2(CopyOffset, 0f), box, font, TitleSize, WithAlpha(Color.white, 0.8f), s.OverrideTitle);
        Title[0] = NewText("Main", titleRoot, pos, box, font, TitleSize, red, s.OverrideTitle);

        // 유지: 잠긴 하단 버튼 위 ACCESS DENIED 띠
        var deny = NewImage("AccessDenied", root, Vector2.zero, new Vector2(1000f, 160f), s.DenyColor);
        AddOutline(deny.gameObject, red, 4f);
        Deny = deny.rectTransform;
        DenyGroup = deny.gameObject.AddComponent<CanvasGroup>();
        var denyText = Stretch("Label", Deny).gameObject.AddComponent<TextMeshProUGUI>();
        SetupText(denyText, font, 72f, red, s.AccessDenied);
        denyText.characterSpacing = 8f;
        AddShadow(denyText.gameObject);

        // 유지: 타이머 아래 배지
        var badge = NewImage("Badge", root, Vector2.zero, new Vector2(330f, 58f), s.PanelColor);
        AddOutline(badge.gameObject, red, 3f);
        Badge = badge.rectTransform;
        BadgeGroup = badge.gameObject.AddComponent<CanvasGroup>();
        var badgeText = NewText("Label", Badge, Vector2.zero, Badge.sizeDelta, font, 34f, red, s.OverrideBadge);
        badgeText.characterSpacing = 10f;
    }

    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    private static RectTransform Stretch(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Center; rect.anchorMax = Center; rect.pivot = Center;
        rect.anchoredPosition = pos; rect.sizeDelta = size;
        return rect;
    }

    private static Image NewImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var image = NewRect(name, parent, pos, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, Vector2 pos, Vector2 size, TMP_FontAsset font, float fontSize, Color color, string text)
    {
        var rect = NewRect(name, parent, pos, size);
        if (size.x >= WideText)
        {
            // 화면 폭 기준 글자: 9:16보다 좁은 화면에서도 잘리지 않게 좌우를 늘리고 자동 크기를 켠다.
            rect.anchorMin = new Vector2(0f, Center.y); rect.anchorMax = new Vector2(1f, Center.y);
            rect.sizeDelta = new Vector2(-2f * SidePadding, size.y);
        }
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        SetupText(tmp, font, fontSize, color, text);
        return tmp;
    }

    private static void SetupText(TextMeshProUGUI tmp, TMP_FontAsset font, float fontSize, Color color, string text)
    {
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = fontSize;
        tmp.fontSizeMin = fontSize * MinFontScale;
        tmp.raycastTarget = false;
    }

    // 밝은 인게임 배경 위에서도 글자가 읽히게 하는 아래 그림자.
    private static void AddShadow(GameObject go)
    {
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(4f, -5f);
    }

    private static void AddOutline(GameObject go, Color color, float width)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(width, -width);
    }
}
