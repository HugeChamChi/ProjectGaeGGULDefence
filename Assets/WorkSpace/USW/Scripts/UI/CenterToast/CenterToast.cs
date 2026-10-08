using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 가운데 짧은 알림 ("식량이 부족합니다." 같은 시스템 문구) — 어디서든 재사용 (사용자 요청 2026-10-02, 참고 design/중앙팝업1.gif).
/// <code>_centerToast.Show("식량이 부족합니다.", CenterToastKind.Warning);</code>
/// 연타 처리는 연출 방식(CenterToastSettings.Style)이 정한다: 쌓기 / 같은 문구 합치기 / 하나만 교체.
/// timeScale을 무시하고 돌아 일시정지 중에도 뜬다. 부품은 풀로 재사용한다.
/// 인게임에 붙일 때는 씬 LifetimeScope에 RegisterComponentInHierarchy로 등록하고 [Inject]로 받아 쓴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CenterToast : MonoBehaviour
{
    private const float MaxStep = 1f / 20f;
    private const float SlotSharpness = 22f;     // 줄이 밀려 올라가는 속도
    private const float SpawnSlot = -0.6f;       // 새 줄은 살짝 아래에서 올라오며 등장
    private const float IconScale = 0.75f;
    private const float BadgeScale = 0.6f;

    [SerializeField] private CenterToastSettings _settings;
    [Tooltip("게임 글꼴 (D 펀치 글자)")]
    [SerializeField] private TMP_FontAsset _font;
    [Tooltip("모던 고딕 글꼴 (A·B·C). 비우면 게임 글꼴")]
    [SerializeField] private TMP_FontAsset _modernFont;
    [Tooltip("알림을 놓을 곳 (가운데 기준). 비우면 이 오브젝트")]
    [SerializeField] private RectTransform _container;

    private readonly List<CenterToastEntry> _entries = new List<CenterToastEntry>();   // 0 = 가장 새 것
    private readonly Stack<CenterToastEntry> _pool = new Stack<CenterToastEntry>();
    private CenterToastSprites _sprites;
    private ICenterToastMotion _motion;
    private CenterToastStyle _style;

    /// <summary>재생 속도 배율 (실험실 슬로모션용, 기본 1).</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>false면 Time.deltaTime 사용 (고정 프레임 캡처용). 기본은 timeScale 무시.</summary>
    public bool UseUnscaledTime { get; set; } = true;

    /// <summary>지금 연출 방식. 바꾸면 떠 있는 알림을 지운다.</summary>
    public CenterToastStyle Style
    {
        get => _style;
        set
        {
            if (_motion != null && value == _style) return;
            Clear();
            _style = value;
            _motion = CreateMotion(value);
        }
    }

    private void Awake()
    {
        if (_container == null) _container = (RectTransform)transform;
        _sprites = new CenterToastSprites();
        if (_settings != null) Style = _settings.Style;
    }

    private void OnDestroy() => _sprites?.Dispose();

    /// <summary>알림을 띄운다. 같은 문구 연타 처리는 연출 방식에 따른다.</summary>
    public void Show(string message, CenterToastKind kind = CenterToastKind.Warning)
    {
        if (string.IsNullOrEmpty(message) || _settings == null || _font == null) return;
        // 인스펙터에서 방식을 바꿨으면 따라간다
        if (_motion == null || _settings.Style != _style) Style = _settings.Style;

        if (_motion.MergeRepeats)
        {
            foreach (var e in _entries)
            {
                if (e.Exiting || e.Message != message || e.Kind != kind) continue;
                e.Count++;
                e.BumpAge = 0f;
                e.ExitAt = e.Age + _settings.Lifetime;
                return;
            }
        }
        if (_motion.SingleSlot)
            foreach (var e in _entries) Kick(e);
        else if (_settings.PushedLifetime > 0f)
            foreach (var e in _entries)   // 밀려 올라간 줄은 그때부터 짧게만 남는다 (다시 밀려도 늘어나지 않음)
                if (!e.Exiting) e.ExitAt = Mathf.Min(e.ExitAt, e.Age + _settings.PushedLifetime);

        var entry = Rent();
        entry.Message = message;
        entry.Kind = kind;
        entry.Count = 1;
        entry.Age = 0f;
        entry.ExitAt = _settings.Lifetime;
        entry.BumpAge = float.PositiveInfinity;
        entry.Slot = SpawnSlot;
        entry.Kicked = false;
        entry.HideParts();
        ApplyFont(entry, _motion.UseModernFont && _modernFont != null ? _modernFont : _font);
        entry.Text.text = message;
        entry.TextWidth = entry.Text.GetPreferredValues(message).x;
        entry.Root.SetAsLastSibling();
        _motion.Prepare(entry, _settings, _sprites);
        _entries.Insert(0, entry);

        int alive = 0;
        foreach (var e in _entries)
            if (!e.Exiting && ++alive > _settings.MaxVisible) Kick(e);
    }

    /// <summary>떠 있는 알림을 모두 즉시 지운다.</summary>
    public void Clear()
    {
        foreach (var e in _entries) Return(e);
        _entries.Clear();
    }

    private void LateUpdate()
    {
        if (_motion == null || _entries.Count == 0) return;
        if (_settings.Style != _style) { Style = _settings.Style; return; }
        float dt = Mathf.Min(UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, MaxStep) * TimeScale;
        float follow = 1f - Mathf.Exp(-SlotSharpness * dt);
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            e.Age += dt;
            e.BumpAge += dt;
            if (e.Age >= e.ExitAt + _motion.ExitSeconds)
            {
                Return(e);
                _entries.RemoveAt(i);
                continue;
            }
            e.Slot = Mathf.Lerp(e.Slot, i, follow);
        }
        for (int i = 0; i < _entries.Count; i++)
        {
            _motion.Render(_entries[i], i, _settings);
            ApplyScale(_entries[i]);
        }
    }

    // 방식이 배치한 결과를 AnchorY 기준으로 통째로 키운다 (간격·이동 거리도 같은 비율).
    private void ApplyScale(CenterToastEntry e)
    {
        float s = _settings.Scale;
        if (Mathf.Approximately(s, 1f)) return;
        var p = e.Root.anchoredPosition;
        e.Root.anchoredPosition = new Vector2(p.x * s, _settings.AnchorY + (p.y - _settings.AnchorY) * s);
        e.Root.localScale *= s;
    }

    private static void Kick(CenterToastEntry e)
    {
        if (!e.Exiting) e.ExitAt = e.Age;
        e.Kicked = true;
    }

    private static ICenterToastMotion CreateMotion(CenterToastStyle style) => style switch
    {
        CenterToastStyle.LineSplit => new CenterToastLineSplitMotion(),
        CenterToastStyle.GlassSlide => new CenterToastGlassSlideMotion(),
        CenterToastStyle.PunchText => new CenterToastPunchMotion(),
        CenterToastStyle.StackLines => new CenterToastStackLinesMotion(),
        CenterToastStyle.SlideLines => new CenterToastSlideLinesMotion(),
        CenterToastStyle.SlideRise => new CenterToastSlideRiseMotion(),
        CenterToastStyle.SlideCards => new CenterToastSlideCardsMotion(),
        CenterToastStyle.SlideBand => new CenterToastSlideBandMotion(),
        _ => new CenterToastStackMotion(),
    };

    // 글꼴이 바뀌면 TMP가 기본 재질로 돌아가 외곽선이 풀린다 — 외곽선은 각 방식의 Prepare가 다시 정한다.
    private static void ApplyFont(CenterToastEntry e, TMP_FontAsset font)
    {
        if (e.Text.font == font) return;
        e.Text.font = font;
        e.BadgeText.font = font;
        e.IconText.font = font;
    }

    // ── 풀 ────────────────────────────────────────────────────

    private CenterToastEntry Rent()
    {
        var e = _pool.Count > 0 ? _pool.Pop() : CreateEntry();
        e.Root.gameObject.SetActive(true);
        return e;
    }

    private void Return(CenterToastEntry e)
    {
        e.Root.gameObject.SetActive(false);
        _pool.Push(e);
    }

    private CenterToastEntry CreateEntry()
    {
        var root = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        root.SetParent(_container, false);
        var group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        var e = new CenterToastEntry
        {
            Root = root,
            Group = group,
            Glow = NewImage("Glow", root),
            Rim = NewImage("Rim", root),
            Background = NewImage("Background", root),
            LineA = NewImage("LineA", root),
            LineB = NewImage("LineB", root),
            Icon = NewImage("Icon", root),
        };
        e.IconText = NewText("IconText", e.Icon.rectTransform, _settings.FontSize * IconScale);
        var clip = new GameObject("Clip", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        clip.SetParent(root, false);
        e.Clip = clip;
        e.Sweep = NewImage("Sweep", clip);
        e.Text = NewText("Text", clip, _settings.FontSize);
        e.Badge = NewImage("Badge", root);
        e.BadgeText = NewText("BadgeText", e.Badge.rectTransform, _settings.FontSize * BadgeScale);
        root.gameObject.SetActive(false);
        return e;
    }

    private static Image NewImage(string name, RectTransform parent)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI NewText(string name, RectTransform parent, float size)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        t.rectTransform.sizeDelta = new Vector2(size * 20f, size * 1.4f);
        t.font = _font;
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }
}
