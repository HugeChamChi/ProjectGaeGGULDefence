using TMPro;
using UnityEngine;

/// <summary>
/// 쿠키런형: 보스 위에 작고 흐린 숫자를 흩뿌린다. 피해를 CookieFlushInterval 동안 종류별로 합쳐 한 개씩 띄운다.
/// </summary>
public sealed class CookieDamageView : IDamageStyleView
{
    private sealed class Popup
    {
        public RectTransform Rect;
        public TextMeshProUGUI Text;
        public CanvasGroup Group;
        public bool Active;
        public float BornAt;
        public float BaseScale;
        public Vector2 Start;
    }

    private static readonly BossDamageKind[] Kinds = { BossDamageKind.Normal, BossDamageKind.Critical, BossDamageKind.Burn };

    private readonly DamageStyleLabSettings _s;
    private readonly RectTransform _root;
    private readonly Popup[] _popups;
    private readonly decimal[] _pending = new decimal[3];
    private Transform _anchor;
    private SpriteRenderer _anchorRenderer;
    private float _nextFlushAt;

    public CookieDamageView(DamageStyleLabSettings settings, RectTransform container)
    {
        _s = settings;
        _root = DamageStyleLabUtil.CreateRoot(container, "CookieStyle");
        _popups = new Popup[Mathf.Max(1, _s.CookieMaxPopups)];
        for (int i = 0; i < _popups.Length; i++)
        {
            var text = DamageStyleLabUtil.CreateText(_root, "Small" + i, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center, out var group);
            text.fontSize = _s.CookieFontSize;
            text.fontStyle = FontStyles.Italic;
            _popups[i] = new Popup { Rect = (RectTransform)text.transform, Text = text, Group = group };
        }
    }

    public void SetAnchor(Transform boss)
    {
        _anchor = boss;
        _anchorRenderer = boss != null ? boss.GetComponentInChildren<SpriteRenderer>() : null;
    }

    public void SetFont(TMP_FontAsset font, Material material)
    {
        foreach (var p in _popups)
        {
            if (font != null) p.Text.font = font;
            if (material != null) p.Text.fontSharedMaterial = material;
        }
    }

    public void Add(decimal amount, BossDamageKind kind)
    {
        if (amount <= 0) return;
        int k = Mathf.Clamp((int)kind, 0, _pending.Length - 1);
        bool idle = _pending[0] == 0 && _pending[1] == 0 && _pending[2] == 0;
        if (idle && Time.unscaledTime >= _nextFlushAt) _nextFlushAt = Time.unscaledTime + _s.CookieFlushInterval;
        _pending[k] += amount;
    }

    public void Tick(float now, float deltaTime)
    {
        if (now >= _nextFlushAt) Flush(now);
        Animate(now);
    }

    public void Clear()
    {
        for (int i = 0; i < _pending.Length; i++) _pending[i] = 0;
        foreach (var p in _popups) { p.Active = false; p.Rect.gameObject.SetActive(false); }
    }

    private void Flush(float now)
    {
        _nextFlushAt = now + _s.CookieFlushInterval;
        foreach (var kind in Kinds)
        {
            int k = (int)kind;
            if (_pending[k] <= 0) continue;
            Spawn(kind, _pending[k], now);
            _pending[k] = 0;
        }
    }

    private void Spawn(BossDamageKind kind, decimal amount, float now)
    {
        Popup target = null;
        foreach (var p in _popups)
        {
            if (!p.Active) { target = p; break; }
            if (target == null || p.BornAt < target.BornAt) target = p;
        }
        if (target == null) return;

        Rect boss = DamageStyleLabUtil.BossScreenRect(_anchor, _anchorRenderer);
        var screen = new Vector2(
            boss.center.x + Random.Range(-0.5f, 0.5f) * boss.width * _s.CookieScatter.x,
            boss.center.y + Random.Range(-0.5f, 0.5f) * boss.height * _s.CookieScatter.y);

        target.Active = true;
        target.BornAt = now;
        target.Start = DamageStyleLabUtil.ScreenToLocal(_root, screen);
        target.BaseScale = kind == BossDamageKind.Critical ? _s.CookieCriticalScale
            : kind == BossDamageKind.Burn ? _s.CookieBurnScale : 1f;
        target.Text.fontSize = _s.CookieFontSize;
        target.Text.text = DamageStyleLabUtil.Short(amount);
        target.Text.colorGradient = (kind == BossDamageKind.Critical ? _s.CriticalColor
            : kind == BossDamageKind.Burn ? _s.BurnColor : _s.NormalColor).ToVertexGradient();
        target.Rect.anchoredPosition = target.Start;
        target.Rect.localScale = Vector3.zero;
        target.Group.alpha = _s.CookieAlpha;
        target.Rect.gameObject.SetActive(true);
        target.Rect.SetAsLastSibling();
    }

    private void Animate(float now)
    {
        float life = Mathf.Max(0.05f, _s.CookieLifetime);
        foreach (var p in _popups)
        {
            if (!p.Active) continue;
            float age = now - p.BornAt;
            float t = age / life;
            if (t >= 1f) { p.Active = false; p.Rect.gameObject.SetActive(false); continue; }
            float eased = 1f - (1f - t) * (1f - t);
            p.Rect.anchoredPosition = p.Start + new Vector2(0f, _s.CookieRise * eased);
            p.Text.fontSize = _s.CookieFontSize;
            float s = p.BaseScale * DamageStyleLabUtil.Pop(age, _s.CookiePopSeconds, _s.CookiePopOvershoot);
            p.Rect.localScale = new Vector3(s, s, 1f);
            p.Group.alpha = _s.CookieAlpha * (t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f);
        }
    }
}
