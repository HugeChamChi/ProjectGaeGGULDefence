using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// FxLab_HackBloom 해킹 스택 UI 시안 공용 기반 — 목표값을 향해 숫자가 굴러가고(기폭 때 빠르게 줄어듦),
/// 최대치 대비 비율로 색 단계(하늘→파랑→보라→MAX 맥동)를 정한다. 시안별 모양은 <see cref="Render"/>에서 그린다.
/// 최대 스택이 50~100이어도 읽히도록 모든 시안은 칸 수가 아닌 비율로 표현한다.
/// 위치는 두 가지: 보스 옆(보스 크기에 따라 흔들림) / HUD(인게임 보스 HP바 기준 고정 — 추천). 시안마다 HUD 자리가 다르다.
/// </summary>
public abstract class HackStackGauge
{
    /// <summary>획득 때 톡 튀는 시간.</summary>
    protected const float BumpTime = .15f;
    /// <summary>개화 때 부풀며 사라지는 시간.</summary>
    protected const float BurstTime = .32f;
    /// <summary>시안 공용 어두운 바탕색.</summary>
    protected static readonly Color Back = new Color(.05f, .09f, .16f, .85f);
    /// <summary>비어 있는 칸·트랙 색.</summary>
    protected static readonly Color Track = new Color(.14f, .2f, .3f, .95f);
    private static readonly Color[] Tiers =
    {
        new Color(.38f, .86f, 1f), new Color(.3f, .62f, 1f), new Color(.6f, .48f, 1f),
    };

    /// <summary>시안 루트 (부모 캔버스 중앙 기준 좌표).</summary>
    protected readonly RectTransform Root;
    /// <summary>표시가 붙는 캔버스 영역.</summary>
    protected readonly RectTransform Parent;
    private readonly CanvasGroup _group;
    private readonly TMP_FontAsset _font;
    private readonly Material _fontMaterial;
    private float _shown, _bumpAt = -9f, _igniteAt = -1f, _burstAt = -1f, _lastStep;
    private bool _hud, _placed;
    private Tweener _valueTween;
    private const float GainDuration = .28f;
    private const float SpendDuration = .38f;

    /// <summary>목표 스택 수.</summary>
    protected int Target { get; private set; }
    /// <summary>최대 스택 수.</summary>
    protected int Max { get; private set; } = 100;

    /// <summary>시안 이름 (버튼).</summary>
    public abstract string Name { get; }

    /// <summary>보스 HP바 영역 (부모 캔버스 좌표). HUD 배치 기준.</summary>
    public Rect HudBar { get; set; }

    /// <summary>true = HP바 기준 HUD 고정, false = 보스 옆.</summary>
    public bool UseHud { get; set; } = true;

    /// <summary>보이기/숨기기 (시안 전환).</summary>
    public virtual bool Active
    {
        set
        {
            if (!value) { _valueTween?.Kill(); _valueTween = null; _shown = Target; }
            Root.gameObject.SetActive(value);
        }
    }

    /// <summary>루트를 만든다. 크기·피벗은 시안이 정한다.</summary>
    protected HackStackGauge(RectTransform parent, TMP_FontAsset font, Material fontMaterial, Vector2 size, Vector2 pivot)
    {
        Parent = parent;
        _font = font; _fontMaterial = fontMaterial;
        Root = new GameObject("HackGauge_" + GetType().Name, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        Root.SetParent(parent, false);
        Root.anchorMin = Root.anchorMax = new Vector2(.5f, .5f);
        Root.pivot = pivot;
        Root.sizeDelta = size;
        _group = Root.GetComponent<CanvasGroup>();
        _group.alpha = 0f; _group.blocksRaycasts = false; _group.interactable = false;
    }

    /// <summary>새 반복 시작: 값 즉시 설정(굴림 없음).</summary>
    public virtual void Reset(int value, int max)
    {
        _valueTween?.Kill(); _valueTween = null;
        Max = Mathf.Max(1, max);
        Target = Mathf.Clamp(value, 0, Max);
        _shown = Target; _lastStep = Target;
        _igniteAt = _burstAt = -1f; _bumpAt = -9f;
        _group.alpha = ShowEmpty || Target > 0 ? 1f : 0f;
    }

    /// <summary>스택 값을 바꾼다. 늘어나면 톡 튀고, 줄어들면 숫자가 굴러 내려간다.</summary>
    public virtual void Set(int value)
    {
        value = Mathf.Clamp(value, 0, Max);
        if (value == Target) return;
        if (value > Target) _bumpAt = Time.time;
        float duration = value > Target ? GainDuration : SpendDuration;
        Target = value;
        _valueTween?.Kill();
        _valueTween = DOTween.To(() => _shown, shown => _shown = shown, Target, duration)
            .SetEase(Ease.OutCubic).SetLink(Root.gameObject);
    }

    /// <summary>기폭 예고 — 하얗게 달아오른다.</summary>
    public void Ignite() => _igniteAt = Time.time;

    /// <summary>실제로 예약된 소모량으로 기폭 피드백을 시작한다.</summary>
    public virtual void Consume(int amount) { if (amount > 0) Ignite(); }

    /// <summary>고정 HUD는 빈 상태에서도 용량을 보여줄 수 있다.</summary>
    protected virtual bool ShowEmpty => false;

    /// <summary>획득 시 전체 위젯의 확대량.</summary>
    protected virtual float PulseScale => .2f;

    /// <summary>개화 — 0으로 굴러 내려가며 크게 부풀고 사라진다.</summary>
    public void Burst() { _burstAt = Time.time; Set(0); }

    /// <summary>매 프레임 갱신. bossLocal은 보스 옆 기준점(캔버스 좌표).</summary>
    public virtual void Tick(float now, Vector2 bossLocal)
    {
        // 기폭 때는 빠르게, 쌓일 때는 바로 따라간다.
        int shown = Mathf.RoundToInt(_shown);
        if (shown != Mathf.RoundToInt(_lastStep) && shown < _lastStep) _bumpAt = Mathf.Max(_bumpAt, now - BumpTime * .5f);
        _lastStep = shown;

        float bump = 1f - Mathf.Clamp01((now - _bumpAt) / BumpTime);
        // 기폭 예고 번쩍임은 잠깐만: 0.1초에 달아올랐다가 0.35초부터 식는다 (일부만 소모해도 흰색으로 남지 않게).
        float ignite = _igniteAt < 0f ? 0f : Mathf.Clamp01((now - _igniteAt) / .1f) * (1f - Mathf.Clamp01((now - _igniteAt - .35f) / .2f));
        float scale = 1f + PulseScale * bump * bump;
        float alpha = ShowEmpty || shown > 0 || Target > 0 ? 1f : 0f;
        if (_burstAt >= 0f)
        {
            float q = Mathf.Clamp01((now - _burstAt) / BurstTime);
            scale *= 1f + .35f * q;
            alpha = 1f - q * q;
            if (q >= 1f && shown == 0) { _burstAt = _igniteAt = -1f; alpha = 0f; }
        }
        else if (Target == 0 && shown == 0) _igniteAt = -1f;
        _group.alpha = _burstAt >= 0f ? alpha : Mathf.MoveTowards(_group.alpha, alpha, Time.deltaTime * 8f);
        if (!_placed || _hud != UseHud) { _placed = true; _hud = UseHud; OnPlacement(UseHud); }
        Root.anchoredPosition = UseHud ? HudAnchor(HudBar) : Anchor(bossLocal);
        scale *= UseHud ? HudScale : 1f;
        Root.localScale = new Vector3(scale, scale, 1f);
        Render(now, shown, Mathf.Clamp01(_shown / Max), bump, ignite);
    }

    /// <summary>보스 옆 위치.</summary>
    protected virtual Vector2 Anchor(Vector2 bossLocal) => bossLocal;

    /// <summary>HUD 크기 배율 (HP바 아래 좁은 자리에 맞춰 줄인다).</summary>
    protected virtual float HudScale => .72f;

    /// <summary>HUD 위치 — 기본은 HP바 바로 아래 오른쪽 끝 정렬 (왼쪽은 기존 디버프 칸 자리). 피벗 (0, 0.5) 기준.</summary>
    protected virtual Vector2 HudAnchor(Rect bar)
        => new Vector2(bar.xMax - 16f - Root.sizeDelta.x * HudScale, bar.yMin - 6f - Root.sizeDelta.y * HudScale * .5f);

    /// <summary>배치가 바뀔 때 시안이 모양을 바꿀 기회 (예: 링은 HUD에서 해골 아이콘을 감싼다).</summary>
    protected virtual void OnPlacement(bool hud) { }

    /// <summary>시안별 모양 그리기.</summary>
    protected abstract void Render(float now, int shown, float ratio, float bump, float ignite);

    /// <summary>비율 단계 색: 하늘 → 파랑 → 보라, 최대치면 흰색과 맥동. 기폭 예고 중이면 흰색으로.</summary>
    protected static Color TierColor(float ratio, float now, float ignite)
    {
        Color c = ratio >= 1f ? Color.Lerp(Tiers[2], Color.white, .5f + .5f * Mathf.Sin(now * 12f))
            : Tiers[Mathf.Clamp((int)(ratio * 3f), 0, 2)];
        return Color.Lerp(c, Color.white, ignite);
    }

    /// <summary>"47/100" — 분모는 작게.</summary>
    protected string Fraction(int shown) => shown + "<size=55%>/" + Max + "</size>";

    // ── 만들기 도우미 ─────────────────────────────────────────

    /// <summary>이미지 하나. anchor/pivot 같은 점 기준으로 놓는다.</summary>
    protected Image Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, Sprite sprite = null)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rect = img.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size; rect.anchoredPosition = position;
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
        return img;
    }

    /// <summary>글자 하나.</summary>
    protected TextMeshProUGUI Text(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions align)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var rect = t.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size; rect.anchoredPosition = position;
        if (_font != null) t.font = _font;
        if (_fontMaterial != null) t.fontSharedMaterial = _fontMaterial;
        t.fontSize = fontSize; t.fontStyle = FontStyles.Bold | FontStyles.Italic;
        t.alignment = align; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        t.richText = true;
        return t;
    }

    /// <summary>왼쪽에서 채워지는 막대 (폭 비율로 채움).</summary>
    protected static void Fill(Image fill, float width, float ratio)
    {
        var size = fill.rectTransform.sizeDelta;
        size.x = width * Mathf.Clamp01(ratio);
        fill.rectTransform.sizeDelta = size;
        fill.enabled = size.x > .5f;
    }
}
