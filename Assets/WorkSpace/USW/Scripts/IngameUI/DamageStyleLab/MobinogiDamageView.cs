using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 모비노기형: 보스 오른쪽 옆에 크고 기울어진 숫자를 세로로 쌓는다.
/// 타격마다 즉시 한 줄씩 띄우고, 새 줄이 들어오면 기존 줄이 위로 밀려난다. 가득 차면 가장 오래된 줄을 재사용한다.
/// </summary>
public sealed class MobinogiDamageView : IDamageStyleView
{
    private sealed class Line
    {
        public RectTransform Rect;
        public TextMeshProUGUI Text;
        public CanvasGroup Group;
        public BossDamageKind Kind;
        public float BornAt;
        public float Y;
        public float BaseScale;
    }

    private readonly DamageStyleLabSettings _s;
    private readonly RectTransform _root;
    private readonly Line[] _pool;
    private readonly List<Line> _active; // 0 = 가장 새 줄, 마지막 = 먼저 교체할 가장 오래된 줄
    private readonly float[] _avgLogByKind = new float[KindCount]; // 종류별 최근 피해량 평균 (로그)
    private readonly bool[] _hasAvgByKind = new bool[KindCount];
    private const int KindCount = 3; // BossDamageKind: Normal, Critical, Burn
    private Transform _anchor;
    private SpriteRenderer _anchorRenderer;

    /// <summary>설정된 최대 줄 수만큼 미리 생성해 타격 순서대로 재사용한다.</summary>
    public MobinogiDamageView(DamageStyleLabSettings settings, RectTransform container)
    {
        _s = settings;
        _root = DamageStyleLabUtil.CreateRoot(container, "MobinogiStyle");
        _pool = new Line[Mathf.Max(1, _s.MobiMaxLines)];
        _active = new List<Line>(_pool.Length);
        for (int i = 0; i < _pool.Length; i++)
        {
            var text = DamageStyleLabUtil.CreateText(_root, "Line" + i, new Vector2(0f, 0.5f), TextAlignmentOptions.MidlineLeft, out var group);
            text.fontSize = _s.MobiFontSize;
            text.fontStyle = FontStyles.Italic;
            _pool[i] = new Line { Rect = (RectTransform)text.transform, Text = text, Group = group };
        }
    }

    /// <inheritdoc />
    public void SetAnchor(Transform boss)
    {
        _anchor = boss;
        _anchorRenderer = boss != null ? boss.GetComponentInChildren<SpriteRenderer>() : null;
    }

    /// <inheritdoc />
    public void SetFont(TMP_FontAsset font, Material material)
    {
        foreach (var line in _pool)
        {
            if (font != null) line.Text.font = font;
            if (material != null) line.Text.fontSharedMaterial = material;
        }
    }

    /// <summary>양수 피해 한 건을 합산이나 대기 없이 표시한다. 최대 개수를 넘으면 가장 오래된 줄을 교체한다.</summary>
    public void Add(decimal amount, BossDamageKind kind) => Add(amount, kind, Time.unscaledTime);

    /// <summary>Tick에 넘기는 시계와 같은 기준의 now로 표시한다 (실험실 슬로모션용).</summary>
    public void Add(decimal amount, BossDamageKind kind, float now)
    {
        if (amount <= 0) return;
        Spawn(kind, amount, now);
    }

    /// <inheritdoc />
    public void Tick(float now, float deltaTime)
    {
        float scale = DamageStyleLabUtil.LocalToScreenScale(_root);
        Vector2 baseScreen = BaseScreen(scale);
        float baseScreenY = baseScreen.y;
        Vector2 basePos = DamageStyleLabUtil.ScreenToLocal(_root, baseScreen);
        float hudTop = Screen.height * (1f - _s.TopHudRatio);
        float spacing = _s.MobiFontSize * _s.MobiLineSpacing;
        float follow = 1f - Mathf.Exp(-_s.MobiFollowSpeed * deltaTime);
        float life = Mathf.Max(0.05f, _s.MobiLifetime);

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var line = _active[i];
            float age = now - line.BornAt;
            float t = age / life;
            if (t >= 1f) { Hide(line); _active.RemoveAt(i); continue; }

            float targetY = basePos.y + i * spacing;
            line.Y = Mathf.Lerp(line.Y, targetY, follow);
            float shake = line.Kind == BossDamageKind.Critical && age < _s.MobiShakeSeconds
                ? Random.Range(-1f, 1f) * _s.MobiCriticalShake * (1f - age / _s.MobiShakeSeconds) : 0f;
            float fade = t <= _s.MobiFadeStart ? 0f : (t - _s.MobiFadeStart) / Mathf.Max(0.0001f, 1f - _s.MobiFadeStart);
            Vector2 entryScale = EntryScale(age, out Vector2 entryOffset);
            line.Rect.anchoredPosition = new Vector2(basePos.x + shake + entryOffset.x, line.Y + entryOffset.y + _s.MobiFadeRise * fade);

            line.Text.fontSize = _s.MobiFontSize;
            line.Rect.localScale = new Vector3(line.BaseScale * entryScale.x, line.BaseScale * entryScale.y, 1f);
            float alpha = 1f - fade;
            // 위로 쌓이다 상단 HUD에 닿는 줄은 흐리게 한다 (위치는 그대로).
            float lineTopPx = (line.Y - basePos.y) * scale + baseScreenY + _s.MobiFontSize * scale * 0.5f;
            alpha *= Mathf.Clamp01((hudTop - lineTopPx) / Mathf.Max(1f, _s.MobiFontSize * scale) + 1f);
            line.Group.alpha = alpha;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        foreach (var line in _active) Hide(line);
        _active.Clear();
    }

    private void Spawn(BossDamageKind kind, decimal amount, float now)
    {
        Line line = null;
        foreach (var candidate in _pool)
            if (!_active.Contains(candidate)) { line = candidate; break; }
        if (line == null)
        {
            // 모두 사용 중이면 가장 오래된 줄(맨 위)을 재사용한다.
            line = _active[_active.Count - 1];
            _active.RemoveAt(_active.Count - 1);
        }

        line.Kind = kind;
        line.BornAt = now;
        line.BaseScale = (kind == BossDamageKind.Critical ? _s.MobiCriticalScale : kind == BossDamageKind.Burn ? _s.MobiBurnScale : 1f)
                         * RelativeSize(kind, amount);
        line.Text.fontSize = _s.MobiFontSize;
        line.Text.text = DamageStyleLabUtil.Full(amount);
        line.Text.colorGradient = (kind == BossDamageKind.Critical ? _s.CriticalColor
            : kind == BossDamageKind.Burn ? _s.BurnColor : _s.NormalColor).ToVertexGradient();
        float scale = DamageStyleLabUtil.LocalToScreenScale(_root);
        Vector2 position = DamageStyleLabUtil.ScreenToLocal(_root, BaseScreen(scale));
        line.Y = position.y;
        line.Rect.anchoredPosition = position;
        line.Rect.localScale = Vector3.zero;
        line.Group.alpha = 1f;
        line.Rect.gameObject.SetActive(true);
        line.Rect.SetAsLastSibling();
        _active.Insert(0, line);
    }

    private static void Hide(Line line) => line.Rect.gameObject.SetActive(false);

    // 같은 종류 최근 평균(로그 이동평균) 대비 이번 피해가 크면 크게, 작으면 작게. 종류별 기본 배율(치명·화상)은 따로 곱한다.
    private float RelativeSize(BossDamageKind kind, decimal amount)
    {
        int index = (int)kind;
        if (_s.MobiRelativeSizePower <= 0f || index < 0 || index >= KindCount) return 1f;
        float log = Mathf.Log(Mathf.Max(1f, (float)amount));
        if (!_hasAvgByKind[index]) { _avgLogByKind[index] = log; _hasAvgByKind[index] = true; }
        float ratio = Mathf.Exp((log - _avgLogByKind[index]) * _s.MobiRelativeSizePower);
        _avgLogByKind[index] += (log - _avgLogByKind[index]) / Mathf.Max(1, _s.MobiRelativeSizeWindow);
        return Mathf.Clamp(ratio, _s.MobiRelativeSizeRange.x, _s.MobiRelativeSizeRange.y);
    }

    // 등장 연출: 가로·세로 크기 배율과 자리 기준 위치 차이.
    //   Pop  = 0 → 1+overshoot → 1 (기존)
    //   Slam = StartScale·Stretch 크기로 SlamFrom 위치에 찍힌 뒤 OutCubic으로 줄며 자리로, 1-Undershoot까지 살짝 작아졌다 1로 복귀
    private Vector2 EntryScale(float age, out Vector2 offset)
    {
        offset = Vector2.zero;
        if (_s.MobiEntry != DamageStyleLabSettings.MobiEntryStyle.Slam)
        {
            float pop = DamageStyleLabUtil.Pop(age, _s.MobiPopSeconds, _s.MobiPopOvershoot);
            return new Vector2(pop, pop);
        }

        float dur = Mathf.Max(0.0001f, _s.MobiSlamSeconds);
        float settleEnd = 1f - _s.MobiSlamUndershoot;
        if (age < dur)
        {
            float u = age / dur;
            float e = 1f - (1f - u) * (1f - u) * (1f - u);
            float k = Mathf.Lerp(_s.MobiSlamStartScale, settleEnd, e);
            offset = _s.MobiSlamFrom * (1f - e);
            return new Vector2(k * Mathf.Lerp(_s.MobiSlamStretch.x, 1f, e), k * Mathf.Lerp(_s.MobiSlamStretch.y, 1f, e));
        }
        float r = Mathf.Clamp01((age - dur) / dur);
        float back = Mathf.Lerp(settleEnd, 1f, 1f - (1f - r) * (1f - r));
        return new Vector2(back, back);
    }

    // 가장 아래(새) 줄 위치 = 보스 중심 + MobiOffset. 값만큼 그대로 움직이고, 화면 밖으로 완전히 나갈 때만 막는다.
    private Vector2 BaseScreen(float localToScreen)
    {
        Rect boss = DamageStyleLabUtil.BossScreenRect(_anchor, _anchorRenderer);
        Vector2 screen = boss.center + _s.MobiOffset * localToScreen;
        screen.x = Mathf.Clamp(screen.x, 0f, Screen.width);
        screen.y = Mathf.Clamp(screen.y, 0f, Screen.height);
        return screen;
    }
}
