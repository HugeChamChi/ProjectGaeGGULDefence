using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 9122 해킹 자폭 B 글리치 흡수(2026-10-21 사용자 확정)의 본게임 표시.
/// 적중점: 짧은 보라 가로 글리치. HACK: 자폭 한 기당 신호 한 개가 링으로 들어가고, 도착 때 숫자를 확인한다.
/// 스택은 적중 순간 실제 장부에 이미 반영되며 여기서는 다시 더하지 않는다. HackingHud가 소유·갱신한다.
/// </summary>
public sealed class BombHackFeedback : MonoBehaviour
{
    private const string GaugeName = "HackOrbit_Segmented";

    private sealed class Impact
    {
        public bool Active;
        public float At;
        public Vector3 World;
    }

    private sealed class Receipt
    {
        public bool Active, Acknowledged;
        public float Born;
        public int Before, After, Gain, Lane;
    }

    private BombHackFeedbackSettings _settings;
    private DroneHackingRuntime _runtime;
    private RectTransform _root, _gauge;
    private Canvas _canvas;
    private Camera _worldCamera;
    private BombHackCanvasFx _fx;
    private StatBuffSpriteAtlas _atlas;
    private WorldGlitchBlocks _glitch;
    private GameObject _glitchRoot;
    private Sprite _pip, _dot, _sparkle;
    private readonly List<Impact> _impacts = new();
    private readonly List<Receipt> _receipts = new();
    private float _nextInputAt;
    private int _cycles, _pendingGain;
    private bool _drewLastFrame;

    /// <summary>적중 순간 반영됐지만 신호가 아직 HACK에 도착하지 않은 스택. HUD 표시에서만 뺀다.</summary>
    public int PendingGain => _pendingGain;

    /// <summary>HUD 캔버스 좌표계와 실제 해킹 자원에 연결한다.</summary>
    public void Initialize(DroneHackingRuntime runtime, RectTransform root, Canvas canvas, BombHackFeedbackSettings settings)
    {
        if (runtime == null || root == null || canvas == null) return;
        _settings = settings != null ? settings : ScriptableObject.CreateInstance<BombHackFeedbackSettings>();
        _runtime = runtime; _root = root; _canvas = canvas;
        _gauge = root.Find(GaugeName) as RectTransform;
        _atlas = new StatBuffSpriteAtlas();
        _pip = _atlas.Resolve(StatBuffShapes.Pip);
        _dot = _atlas.Resolve(StatBuffShapes.Dot);
        _sparkle = _atlas.Resolve(StatBuffShapes.Get(StatBuffShape.Sparkle).Fill);
        var fxObject = new GameObject("BombHackFx", typeof(RectTransform));
        var rect = fxObject.GetComponent<RectTransform>();
        rect.SetParent(root, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();
        _fx = fxObject.AddComponent<BombHackCanvasFx>();
        _fx.Initialize(_pip.texture);
        // 월드 크기로 늘리는 조각이므로 캔버스 배율을 받지 않는 씬 루트에 둔다.
        _glitchRoot = new GameObject("BombHackGlitchPool", typeof(SortingGroup));
        var group = _glitchRoot.GetComponent<SortingGroup>();
        group.sortingLayerName = "FX"; group.sortingOrder = 55;
        _glitch = new WorldGlitchBlocks(_glitchRoot.transform, 1);
        _runtime.OnBombStacksAccepted += OnBombHit;
    }

    /// <summary>보스 교체·전투 종료 때 대기 중인 표시를 모두 버린다.</summary>
    public void Clear()
    {
        foreach (var impact in _impacts) impact.Active = false;
        foreach (var receipt in _receipts) receipt.Active = receipt.Acknowledged = false;
        _pendingGain = 0; _nextInputAt = 0f; _cycles = 0;
        _glitch?.Clear();
        if (_drewLastFrame) { _fx.BeginFrame(); _fx.EndFrame(); _drewLastFrame = false; }
    }

    private void OnBombHit(Vector3 world, int accepted, int total)
    {
        float now = Time.time;
        bool dense = ActiveImpacts() >= _settings.DenseImpactThreshold;
        Impact slot = null;
        foreach (var impact in _impacts) if (!impact.Active) { slot = impact; break; }
        if (slot == null) _impacts.Add(slot = new Impact());
        slot.Active = true; slot.At = now; slot.World = world;
        int blocks = dense ? _settings.DenseGlitchBlocks : _settings.GlitchBlocks;
        for (int i = 0; i < blocks; i++)
        {
            Vector3 p = world + new Vector3((i - 2f) * 0.09f, Mathf.Sin(i * 2f) * 0.16f, 0f);
            _glitch.Spawn(p, new Vector2(0.23f + i * 0.04f, 0.045f), _settings.RuptureColor, _settings.RuptureSeconds, 0.12f);
        }
        // 용량을 넘어 실제로 수용되지 않은 양에는 수신을 만들지 않는다.
        if (accepted <= 0) return;
        Receipt free = null;
        foreach (var receipt in _receipts) if (!receipt.Active) { free = receipt; break; }
        // 수용한 적중마다 수신을 보존한다. 풀은 동시에 대기한 최대 수까지만 자란다(장부 용량 이하).
        if (free == null) _receipts.Add(free = new Receipt());
        free.Born = Mathf.Max(now + _settings.GlitchLeadSeconds, _nextInputAt);
        _nextInputAt = free.Born + _settings.ReceiptSpacingSeconds;
        free.Gain = accepted; free.After = total; free.Before = total - accepted;
        free.Lane = _cycles % 3 - 1; free.Active = true; free.Acknowledged = false;
        _cycles++; _pendingGain += accepted;
    }

    /// <summary>HackingHud가 숫자를 갱신하기 직전에 한 번 호출한다.</summary>
    public void Tick(int capacity)
    {
        if (_fx == null) return;
        float now = Time.time;
        _glitch.Tick(Time.deltaTime);
        _fx.BeginFrame();
        bool drew = false;
        bool dense = ActiveImpacts() >= _settings.DenseImpactThreshold;
        foreach (var impact in _impacts)
        {
            if (!impact.Active) continue;
            float age = now - impact.At;
            if (age >= Mathf.Max(_settings.RuptureSeconds, _settings.SparkleSeconds)) { impact.Active = false; continue; }
            DrawImpact(impact, age, dense ? _settings.DenseDetailScale : 1f);
            drew = true;
        }
        Vector2 center = _gauge != null ? _gauge.anchoredPosition : Vector2.zero;
        foreach (var receipt in _receipts)
        {
            if (!receipt.Active) continue;
            float age = now - receipt.Born;
            if (age < 0f) continue;
            if (!receipt.Acknowledged && age >= _settings.GlitchTransferSeconds)
            {
                receipt.Acknowledged = true;
                _pendingGain = Mathf.Max(0, _pendingGain - receipt.Gain);
            }
            DrawIntake(receipt, age, center, capacity);
            drew = true;
            if (age >= _settings.GlitchTransferSeconds + _settings.GlitchSettleSeconds) receipt.Active = false;
        }
        if (drew || _drewLastFrame) _fx.EndFrame();
        _drewLastFrame = drew;
    }

    private int ActiveImpacts()
    {
        int count = 0;
        foreach (var impact in _impacts) if (impact.Active) count++;
        return count;
    }

    private void DrawImpact(Impact impact, float age, float detail)
    {
        if (!TryCanvasPoint(impact.World, out Vector2 origin)) return;
        float q = Mathf.Clamp01(age / _settings.RuptureSeconds);
        if (q < 1f)
        {
            _fx.Sprite(_dot, origin, Vector2.one * _settings.ImpactSize * (0.7f + q), Alpha(_settings.ImpactInkColor, (1f - q) * 0.75f));
            for (int j = 0; j < 3; j++)
            {
                Vector2 row = origin + new Vector2(0f, (j - 1) * _settings.SliceSpacing);
                _fx.Line(_pip, row + Vector2.left * _settings.SliceHalfLength, row + Vector2.right * _settings.SliceHalfLength,
                    _settings.SliceWidth, Alpha(_settings.RuptureColor, 1f - q));
            }
        }
        float fade = 1f - Mathf.Clamp01(age / _settings.SparkleSeconds);
        _fx.Sprite(_sparkle, origin, Vector2.one * _settings.SparkleSize * detail, Alpha(_settings.DataColor, fade));
    }

    private void DrawIntake(Receipt receipt, float age, Vector2 center, int capacity)
    {
        float t = Mathf.Clamp01(age / _settings.GlitchTransferSeconds);
        float settle = Mathf.Clamp01((age - _settings.GlitchTransferSeconds) / _settings.GlitchSettleSeconds);
        float fade = 1f - settle;
        for (int i = 0; i < 3; i++)
        {
            float compression = Mathf.Clamp01(t * 1.16f - i * 0.08f);
            Vector2 outside = center + new Vector2(-143f + i * 7f, -28f + (i - 1) * 9f + receipt.Lane * 18f);
            Vector2 destination = center + new Vector2(-89f, -24f + (i - 1) * 3f + receipt.Lane * 8f);
            float movement = compression * compression * (3f - 2f * compression);
            Vector2 p = Vector2.Lerp(outside, destination, movement);
            float width = Mathf.Lerp(_settings.GlitchWidth * (i == 0 ? 1f : 0.48f), 6f, compression);
            Color color = Color.Lerp(_settings.GlitchColor, _settings.DataColor, compression);
            float strength = i == 0 ? 1f : 0.6f;
            _fx.Sprite(_pip, p, new Vector2(width + 5f, 8f), Alpha(_settings.ReceiptInkColor, fade * strength));
            _fx.Sprite(_pip, p, new Vector2(width, Mathf.Lerp(4f, 6f, compression)), Alpha(color, fade * strength));
            _fx.Sprite(_pip, p + new Vector2(width * 0.22f, 0f), new Vector2(8f, 3f), Alpha(_settings.DataColor, fade * strength));
        }
        if (t < 0.9f || capacity <= 0) return;
        float from = receipt.Before / (float)capacity * 360f;
        float span = Mathf.Max(4f, receipt.Gain / (float)capacity * 360f);
        int count = Mathf.Clamp(Mathf.CeilToInt(span / 4f), 3, 24);
        Color arc = Alpha(_settings.DataColor, fade * 0.75f);
        for (int i = 0; i < count; i++)
        {
            Vector2 a = center + Direction(from + span * i / count) * _settings.RingRadius;
            Vector2 b = center + Direction(from + span * (i + 1) / count) * _settings.RingRadius;
            _fx.Line(_pip, a, b, 7f, Alpha(_settings.ReceiptInkColor, arc.a));
            _fx.Line(_pip, a, b, 3f, arc);
        }
    }

    private bool TryCanvasPoint(Vector3 world, out Vector2 local)
    {
        if (_worldCamera == null) _worldCamera = Camera.main;
        local = default;
        if (_worldCamera == null) return false;
        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, _worldCamera.WorldToScreenPoint(world), uiCamera, out local);
    }

    private static Vector2 Direction(float degrees) => new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));
    private static Color Alpha(Color color, float alpha) { color.a *= Mathf.Clamp01(alpha); return color; }

    private void OnDestroy()
    {
        if (_runtime != null) _runtime.OnBombStacksAccepted -= OnBombHit;
        _glitch?.Dispose();
        if (_glitchRoot != null) Destroy(_glitchRoot);
        _atlas?.Dispose();
    }
}
