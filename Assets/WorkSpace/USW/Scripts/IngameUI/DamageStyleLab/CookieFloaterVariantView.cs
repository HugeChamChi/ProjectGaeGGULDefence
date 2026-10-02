using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 쿠키런형 후보 하나를 보스 한 마리 위에 그린다 (FxLab_CookieFloater 전용).
/// 후보 값은 매 프레임 설정 에셋에서 읽어 플레이 중 튜닝이 바로 보인다.
/// 뜨는 범위·중앙 영역 표시선과, 숫자가 범위 밖으로 얼마나 나가는지(이탈 거리)를 함께 기록한다.
/// </summary>
public sealed class CookieFloaterVariantView
{
    private const int PoolSize = 32;
    private const int OuterPickAttempts = 12;

    private sealed class Popup
    {
        public RectTransform Rect;
        public TextMeshProUGUI Text;
        public CanvasGroup Group;
        public bool Active;
        public float BornAt;
        public float BaseScale;
        public float BaseAlpha;
        public Vector2 Start;
        public Vector2 Direction;
    }

    private static readonly BossDamageKind[] Kinds = { BossDamageKind.Normal, BossDamageKind.Critical, BossDamageKind.Burn };

    private readonly CookieFloaterLabSettings _s;
    private readonly RectTransform _root;
    private readonly Popup[] _popups = new Popup[PoolSize];
    private readonly decimal[] _pending = new decimal[3];
    private readonly Image[] _scatterGuide;
    private readonly Image[] _centerGuide;
    private SpriteRenderer _anchor;
    private System.Random _rng;
    private int _seed;
    private float _nextFlushAt;
    private bool _flushArmed;

    /// <summary>현재 후보 번호 (Settings.Variants 기준).</summary>
    public int VariantIndex { get; private set; }
    /// <summary>현재 후보. 범위를 벗어나면 null.</summary>
    public CookieFloaterLabSettings.Variant Variant =>
        _s.Variants != null && VariantIndex >= 0 && VariantIndex < _s.Variants.Length ? _s.Variants[VariantIndex] : null;
    /// <summary>Clear 이후 동시에 떠 있던 숫자 최대 수.</summary>
    public int PeakActive { get; private set; }
    /// <summary>Clear 이후 숫자 중심이 뜨는 범위 위로 나간 최대 거리 (캔버스 단위).</summary>
    public float MaxOverTop { get; private set; }
    /// <summary>Clear 이후 숫자 중심이 뜨는 범위 옆으로 나간 최대 거리 (캔버스 단위).</summary>
    public float MaxOverSide { get; private set; }

    public CookieFloaterVariantView(CookieFloaterLabSettings settings, RectTransform container, string name, int seed)
    {
        _s = settings;
        _seed = seed;
        _rng = new System.Random(seed);
        _root = DamageStyleLabUtil.CreateRoot(container, name);
        var guides = DamageStyleLabUtil.CreateRoot(_root, "Guides");
        _scatterGuide = CreateFrame(guides, "ScatterGuide");
        _centerGuide = CreateFrame(guides, "CenterGuide");
        for (int i = 0; i < _popups.Length; i++)
        {
            var text = DamageStyleLabUtil.CreateText(_root, "Num" + i, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center, out var group);
            text.fontStyle = FontStyles.Italic;
            if (_s.Font != null) text.font = _s.Font;
            if (_s.FontMaterial != null) text.fontSharedMaterial = _s.FontMaterial;
            _popups[i] = new Popup { Rect = (RectTransform)text.transform, Text = text, Group = group };
        }
    }

    /// <summary>숫자를 띄울 보스 스프라이트.</summary>
    public void SetAnchor(SpriteRenderer boss) => _anchor = boss;

    /// <summary>후보를 바꾸고 화면을 비운다.</summary>
    public void SetVariant(int index)
    {
        VariantIndex = index;
        Clear();
    }

    /// <summary>범위 표시선 켜기/끄기.</summary>
    public bool ShowGuides { get; set; } = true;

    /// <summary>피해 한 건. FlushInterval 동안 종류별로 합쳐 하나로 띄운다.</summary>
    public void Add(decimal amount, BossDamageKind kind, float now)
    {
        if (amount <= 0 || Variant == null) return;
        if (!_flushArmed)
        {
            _flushArmed = true;
            _nextFlushAt = now + Mathf.Max(0f, Variant.FlushInterval);
        }
        _pending[Mathf.Clamp((int)kind, 0, _pending.Length - 1)] += amount;
    }

    /// <summary>떠 있는 숫자·쌓인 피해·기록을 지우고 무작위 순서를 처음으로 되돌린다.</summary>
    public void Clear()
    {
        for (int i = 0; i < _pending.Length; i++) _pending[i] = 0;
        _flushArmed = false;
        foreach (var p in _popups) { p.Active = false; p.Rect.gameObject.SetActive(false); }
        _rng = new System.Random(_seed);
        PeakActive = 0;
        MaxOverTop = 0f;
        MaxOverSide = 0f;
    }

    /// <summary>시계 기준 갱신.</summary>
    public void Tick(float now)
    {
        var v = Variant;
        if (v == null) return;
        Rect area = ScatterArea(v);
        UpdateGuides(v, area);
        if (_flushArmed && now >= _nextFlushAt) Flush(v, area, now);
        Animate(v, area, now);
    }

    // ── 생성 ──────────────────────────────────────────────────

    private void Flush(CookieFloaterLabSettings.Variant v, Rect area, float now)
    {
        _flushArmed = false;
        foreach (var kind in Kinds)
        {
            int k = (int)kind;
            if (_pending[k] <= 0) continue;
            Spawn(v, area, kind, _pending[k], now);
            _pending[k] = 0;
        }
    }

    private void Spawn(CookieFloaterLabSettings.Variant v, Rect area, BossDamageKind kind, decimal amount, float now)
    {
        Popup target = null;
        int limit = Mathf.Clamp(v.MaxPopups, 1, _popups.Length);
        for (int i = 0; i < limit; i++)
        {
            var p = _popups[i];
            if (!p.Active) { target = p; break; }
            if (target == null || p.BornAt < target.BornAt) target = p;
        }
        if (target == null) return;

        bool strong = kind != BossDamageKind.Normal;
        bool center = v.UseCenterZone && !(strong && v.StrongAlwaysOuter) && Next() < v.CenterShare;
        Rect zone = CenterZone(v, area);
        Vector2 start = center ? RandomIn(zone) : v.UseCenterZone ? RandomOutside(area, zone) : RandomIn(area);

        float kindScale = kind == BossDamageKind.Critical ? v.CriticalScale : kind == BossDamageKind.Burn ? v.BurnScale : 1f;
        float zoneScale = !v.UseCenterZone ? 1f : center ? v.CenterScale : v.OuterScale;

        target.Text.fontSize = v.FontSize;
        target.Text.text = DamageStyleLabUtil.Short(amount);
        target.BaseScale = kindScale * zoneScale;
        target.Direction = Direction(v, start.x - area.center.x);
        if (_s.FullScreenWidth)
        {
            float halfWidth = target.Text.GetPreferredValues(target.Text.text).x * 0.5f * target.BaseScale * (1f + Mathf.Max(0f, v.PopOvershoot));
            start = KeepInside(start, target.Direction, v.Rise, halfWidth, area);
        }

        target.Active = true;
        target.BornAt = now;
        target.Start = start;
        target.BaseAlpha = center ? v.CenterAlpha : v.Alpha;
        target.Text.colorGradient = (kind == BossDamageKind.Critical ? _s.CriticalColor
            : kind == BossDamageKind.Burn ? _s.BurnColor : _s.NormalColor).ToVertexGradient();
        target.Rect.anchoredPosition = start;
        target.Rect.localScale = Vector3.zero;
        target.Group.alpha = target.BaseAlpha;
        target.Rect.gameObject.SetActive(true);
        target.Rect.SetAsLastSibling();

        // 끝 위치 기준 이탈 거리 (숫자 중심)
        Vector2 end = start + target.Direction * v.Rise;
        MaxOverTop = Mathf.Max(MaxOverTop, end.y - area.yMax);
        MaxOverSide = Mathf.Max(MaxOverSide, area.xMin - end.x, end.x - area.xMax);
    }

    private Vector2 Direction(CookieFloaterLabSettings.Variant v, float offsetFromCenter)
    {
        if (v.Motion == CookieFloaterLabSettings.Motion.Up) return Vector2.up;
        float side = offsetFromCenter > 0f ? 1f : offsetFromCenter < 0f ? -1f : (Next() < 0.5f ? -1f : 1f);
        float rad = v.DiagonalAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad) * side, Mathf.Cos(rad));
    }

    // ── 움직임 ────────────────────────────────────────────────

    private void Animate(CookieFloaterLabSettings.Variant v, Rect area, float now)
    {
        float life = Mathf.Max(0.05f, v.Lifetime);
        int active = 0;
        foreach (var p in _popups)
        {
            if (!p.Active) continue;
            float age = now - p.BornAt;
            float t = age / life;
            if (t >= 1f) { p.Active = false; p.Rect.gameObject.SetActive(false); continue; }
            active++;
            float eased = 1f - Mathf.Pow(1f - t, Mathf.Max(1f, v.RiseEasePower));
            p.Rect.anchoredPosition = p.Start + p.Direction * (v.Rise * eased);
            float s = p.BaseScale * DamageStyleLabUtil.Pop(age, v.PopSeconds, v.PopOvershoot);
            p.Rect.localScale = new Vector3(s, s, 1f);
            float fade = t < v.FadeStart ? 1f : 1f - (t - v.FadeStart) / Mathf.Max(0.0001f, 1f - v.FadeStart);
            p.Group.alpha = p.BaseAlpha * fade;
        }
        PeakActive = Mathf.Max(PeakActive, active);
    }

    // ── 범위 ──────────────────────────────────────────────────

    // 보스 스프라이트 화면 영역 × Scatter, 이 view 로컬 좌표
    private Rect ScatterArea(CookieFloaterLabSettings.Variant v)
    {
        Rect boss = DamageStyleLabUtil.BossScreenRect(_anchor != null ? _anchor.transform : null, _anchor);
        Vector2 min = DamageStyleLabUtil.ScreenToLocal(_root, boss.min);
        Vector2 max = DamageStyleLabUtil.ScreenToLocal(_root, boss.max);
        var local = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        var size = Vector2.Scale(local.size, v.Scatter);
        var area = new Rect(local.center - size * 0.5f, size);
        if (!_s.FullScreenWidth) return area;
        Rect screen = _root.rect;
        float pad = Mathf.Max(0f, _s.ScreenEdgePadding);
        return Rect.MinMaxRect(screen.xMin + pad, area.yMin, screen.xMax - pad, area.yMax);
    }

    // 숫자 전체(글자 폭·등장 때 커지는 만큼·대각선 이동)가 가로 범위 안에 남도록 시작 x를 당긴다.
    private static Vector2 KeepInside(Vector2 start, Vector2 direction, float rise, float halfWidth, Rect area)
    {
        float min = area.xMin + halfWidth + Mathf.Max(0f, -direction.x * rise);
        float max = area.xMax - halfWidth - Mathf.Max(0f, direction.x * rise);
        start.x = min <= max ? Mathf.Clamp(start.x, min, max) : area.center.x;
        return start;
    }

    private static Rect CenterZone(CookieFloaterLabSettings.Variant v, Rect area)
    {
        var size = Vector2.Scale(area.size, v.CenterZone);
        return new Rect(area.center - size * 0.5f, size);
    }

    private Vector2 RandomIn(Rect r) => new Vector2(Mathf.Lerp(r.xMin, r.xMax, Next()), Mathf.Lerp(r.yMin, r.yMax, Next()));

    // 바깥 테두리(area − zone)에서 고른다. 계속 중앙에 걸리면 가로 가장자리로 민다.
    private Vector2 RandomOutside(Rect area, Rect zone)
    {
        for (int i = 0; i < OuterPickAttempts; i++)
        {
            var p = RandomIn(area);
            if (!zone.Contains(p)) return p;
        }
        var q = RandomIn(area);
        q.x = q.x < area.center.x ? Mathf.Lerp(area.xMin, zone.xMin, Next()) : Mathf.Lerp(zone.xMax, area.xMax, Next());
        return q;
    }

    private float Next() => (float)_rng.NextDouble();

    // ── 표시선 ────────────────────────────────────────────────

    private void UpdateGuides(CookieFloaterLabSettings.Variant v, Rect area)
    {
        SetFrame(_scatterGuide, area, _s.ScatterGuideColor, ShowGuides);
        SetFrame(_centerGuide, CenterZone(v, area), _s.CenterGuideColor, ShowGuides && v.UseCenterZone);
    }

    private static Image[] CreateFrame(RectTransform parent, string name)
    {
        var frame = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject($"{name}_{i}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            frame[i] = go.GetComponent<Image>();
            frame[i].raycastTarget = false;
        }
        return frame;
    }

    private void SetFrame(Image[] frame, Rect r, Color color, bool visible)
    {
        float t = Mathf.Max(1f, _s.GuideThickness);
        // 위, 아래, 왼쪽, 오른쪽
        Place(frame[0], new Vector2(r.center.x, r.yMax), new Vector2(r.width + t, t), color, visible);
        Place(frame[1], new Vector2(r.center.x, r.yMin), new Vector2(r.width + t, t), color, visible);
        Place(frame[2], new Vector2(r.xMin, r.center.y), new Vector2(t, r.height + t), color, visible);
        Place(frame[3], new Vector2(r.xMax, r.center.y), new Vector2(t, r.height + t), color, visible);
    }

    private static void Place(Image image, Vector2 pos, Vector2 size, Color color, bool visible)
    {
        if (image.gameObject.activeSelf != visible) image.gameObject.SetActive(visible);
        if (!visible) return;
        image.rectTransform.anchoredPosition = pos;
        image.rectTransform.sizeDelta = size;
        image.color = color;
    }
}
