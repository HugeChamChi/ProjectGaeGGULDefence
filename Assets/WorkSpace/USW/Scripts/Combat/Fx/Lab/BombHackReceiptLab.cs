using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>E 충돌과 네모를 뺀 기존 빛점에, 자폭 한 기당 한 번의 HACK 수신 움직임을 붙인다.</summary>
[DefaultExecutionOrder(150)]
public sealed class BombHackReceiptLab : MonoBehaviour
{
    [SerializeField] private BombHackLab _lab;
    [SerializeField] private BombHackReceiptSettings _settings;
    [SerializeField] private BombHackCanvasFx _fx;
    [SerializeField] private TMP_Text _title, _description, _status;
    [SerializeField] private Button[] _buttons;
    private StatBuffSpriteAtlas _atlas;
    private Sprite _pip;
    private bool _ready;
    private float _nextInputAt;
    private int _acceptedGain, _representedGain, _cycles, _activeCount, _visibleHighWater;
    private Receipt[] _receipts;
    private sealed class Receipt
    {
        public bool Active, Acknowledged;
        public float Born;
        public int Before, After, Gain, Lane;
        public Vector3 Origin;
    }

    /// <summary>수신 드라이버가 관찰한 실제 수용량. 적중 순간 증가한다.</summary>
    public int AcceptedGain => _acceptedGain;
    /// <summary>수신 움직임이 끝나 HUD에 확인한 양. 장부에 다시 더하지 않는다.</summary>
    public int RepresentedGain => _representedGain;
    /// <summary>적중 이벤트마다 별도로 만든 수신 수. 여러 기를 합치지 않는다.</summary>
    public int CycleCount => _cycles;
    /// <summary>수신/개별 대기 동작이 남아 있는지.</summary>
    public bool IsReceiving => _activeCount > 0;
    /// <summary>개별로 보관한 미확인 수용량. 합산 표시는 하지 않는다.</summary>
    public int PendingGain => _acceptedGain - _representedGain;
    /// <summary>동시에 보인 개별 수신 동작의 최대 수.</summary>
    public int VisibleHighWater => _visibleHighWater;

    private void OnEnable()
    {
        if (_lab == null) return;
        _lab.StackAccepted += OnAccepted; _lab.PreviewReset += ResetFeedback;
    }

    private void Start()
    {
        _atlas = new StatBuffSpriteAtlas();
        _pip = _atlas.Resolve(StatBuffShapes.Pip);
        // 이 씬은 소비 없이 원본 용량까지만 받는다. 모든 유효 수신을 보존할 수 있는 고정 풀이다.
        _receipts = new Receipt[Mathf.Max(8, _lab.PreviewCapacity)];
        for (int i = 0; i < _receipts.Length; i++) _receipts[i] = new Receipt();
        _fx.Initialize(_pip.texture); _fx.transform.SetAsLastSibling(); _ready = true; ResetFeedback();
    }

    private void ResetFeedback()
    {
        _acceptedGain = _representedGain = _cycles = _activeCount = _visibleHighWater = 0;
        _nextInputAt = 0f;
        if (_receipts != null) foreach (var r in _receipts) r.Active = r.Acknowledged = false;
        if (_ready) { _fx.BeginFrame(); _fx.EndFrame(); }
    }

    private void OnAccepted(Vector3 position, int gain)
    {
        if (gain <= 0) return;
        _acceptedGain += gain;
        Receipt free = null;
        foreach (var r in _receipts) if (!r.Active) { free = r; break; }
        if (free == null) throw new System.InvalidOperationException("Receipt pool cannot drop an accepted impact");
        free.Born = Mathf.Max(_lab.PreviewClock + _settings.GlitchLeadSeconds, _nextInputAt);
        _nextInputAt = free.Born + Mathf.Max(0.01f, _settings.ReceiptSpacingSeconds);
        free.Origin = position; free.Gain = gain;
        free.Before = _lab.PreviewStacks - gain; free.After = _lab.PreviewStacks;
        free.Lane = _cycles % 3 - 1; free.Active = true; free.Acknowledged = false;
        _cycles++; _activeCount++;
    }

    private void LateUpdate()
    {
        if (!_ready) return;
        _fx.BeginFrame();
        int visible = 0;
        foreach (var r in _receipts)
        {
            if (!r.Active) continue;
            float age = _lab.PreviewClock - r.Born;
            if (age < 0f) continue;
            visible++;
            float arrival = TransferSeconds;
            if (!r.Acknowledged && age >= arrival)
            {
                _lab.ShowReceivedStacks(Mathf.Max(_lab.HudTargetStacks, r.After));
                _representedGain += r.Gain; r.Acknowledged = true;
            }
            DrawGlitchIntake(r, age);
            if (age >= arrival + SettleSeconds) { r.Active = false; _activeCount--; }
        }
        _visibleHighWater = Mathf.Max(_visibleHighWater, visible);
        _fx.EndFrame(); RefreshText();
    }

    private float TransferSeconds => _settings.GlitchTransferSeconds;
    private float SettleSeconds => _settings.GlitchSettleSeconds;
    private float Transfer(float age) => Mathf.Clamp01(age / Mathf.Max(0.01f, TransferSeconds));
    private float Settle(float age) => Mathf.Clamp01((age - TransferSeconds) / Mathf.Max(0.01f, SettleSeconds));

    private void DrawGlitchIntake(Receipt r, float age)
    {
        Vector2 center = _lab.HudCenter;
        float t = Transfer(age), tail = Settle(age), fade = 1f - tail;
        for (int i = 0; i < 3; i++)
        {
            float compression = Mathf.Clamp01(t * 1.16f - i * 0.08f);
            Vector2 outside = center + new Vector2(-143f + i * 7f, -28f + (i - 1) * 9f + r.Lane * 18f);
            Vector2 destination = center + new Vector2(-89f, -24f + (i - 1) * 3f + r.Lane * 8f);
            float movement = compression * compression * (3f - 2f * compression);
            Vector2 p = Vector2.Lerp(outside, destination, movement);
            float width = Mathf.Lerp(_settings.GlitchWidth * (i == 0 ? 1f : 0.48f), 6f, compression);
            Color color = Color.Lerp(_settings.GlitchColor, _settings.DataColor, compression);
            float strength = i == 0 ? 1f : 0.6f;
            _fx.Sprite(_pip, p, new Vector2(width + 5f, 8f), Alpha(_settings.InkColor, fade * strength));
            _fx.Sprite(_pip, p, new Vector2(width, Mathf.Lerp(4f, 6f, compression)), Alpha(color, fade * strength));
            _fx.Sprite(_pip, p + new Vector2(width * 0.22f, 0f), new Vector2(8f, 3f), Alpha(_settings.DataColor, fade * strength));
        }
        DrawReceivedArc(r, age);
    }

    private void DrawReceivedArc(Receipt r, float age)
    {
        float t = Transfer(age);
        if (t < 0.9f) return;
        float fade = 1f - Settle(age);
        float from = r.Before / (float)_lab.PreviewCapacity * 360f;
        float span = Mathf.Max(4f, (r.After - r.Before) / (float)_lab.PreviewCapacity * 360f);
        int count = Mathf.Clamp(Mathf.CeilToInt(span / 4f), 3, 24);
        for (int i = 0; i < count; i++)
        {
            Vector2 a = _lab.HudCenter + Direction(from + span * i / count) * _settings.RingRadius;
            Vector2 b = _lab.HudCenter + Direction(from + span * (i + 1) / count) * _settings.RingRadius;
            Line(a, b, 3f, Alpha(_settings.DataColor, fade * 0.75f));
        }
    }

    private void Line(Vector2 a, Vector2 b, float width, Color color)
    {
        _fx.Line(_pip, a, b, width + 4f, Alpha(_settings.InkColor, color.a)); _fx.Line(_pip, a, b, width, color);
    }
    private static Vector2 Direction(float degrees) => new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));
    private static Color Alpha(Color color, float alpha) { color.a *= Mathf.Clamp01(alpha); return color; }

    private void RefreshText()
    {
        _title.text = "9122 해킹 자폭  /  B 글리치 흡수 확정";
        _description.text = "한 기마다 짧은 신호가 빠르게 흡수됩니다.\n여러 기도 합치지 않고 각각 들어갑니다.";
        _status.text = "네모 낙인 없음 · 한 기당 한 번 수신\n" +
            (_lab.IsDense ? "베탕12 · 전투 드론48" : "베탕1 · 전투 드론4") +
            " · " + (_lab.IsPaused ? "정지" : "재생") + "\n수신 동작만 비교 · 본게임 미연결";
        for (int i = 0; i < _buttons.Length; i++)
        {
            int action = i;
            bool active = action == 2 ? _lab.IsDense : action == 3 ? _lab.LoopEnabled :
                action == 4 ? _lab.PressureEnabled : action == 5 ? _lab.AmbientEnabled : action == 6 && _lab.IsPaused;
            _buttons[i].GetComponent<Image>().color = active ? new Color(0.25f, 0.18f, 0.4f, 0.97f) : new Color(0.09f, 0.13f, 0.19f, 0.96f);
        }
    }

    private void OnDisable()
    {
        if (_lab == null) return;
        _lab.StackAccepted -= OnAccepted; _lab.PreviewReset -= ResetFeedback;
    }
    private void OnDestroy() => _atlas?.Dispose();
}
