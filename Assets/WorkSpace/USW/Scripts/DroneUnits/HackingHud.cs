using TMPro;
using UnityEngine;

/// <summary>실제 보스 해킹 잔고를 독립 링으로 표시한다. FxLab의 임의 충전 루프는 사용하지 않는다.</summary>
public sealed class HackingHud : MonoBehaviour
{
    private DroneHackingRuntime _runtime;
    private HackGaugeOrbit _gauge;
    private RectTransform _parent, _bar;
    private TMP_Text _reserved;
    private readonly Vector3[] _corners = new Vector3[4];
    private BossBase _target;
    private int _version;
    private int _capacity;
    private long _totalConsumed;
    /// <summary>실제 HP바와 동일 캔버스 좌표를 사용한다.</summary>
    public void Initialize(DroneHackingRuntime runtime, UI_BossHpBar bar)
    {
        if (runtime == null || bar == null || _gauge != null) return;
        var canvas = bar.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        _runtime = runtime;
        _bar = (RectTransform)bar.transform;
        var host = new GameObject("HackingHud", typeof(RectTransform));
        _parent = host.GetComponent<RectTransform>();
        _parent.SetParent(canvas.transform, false);
        _parent.anchorMin = Vector2.zero; _parent.anchorMax = Vector2.one;
        _parent.offsetMin = _parent.offsetMax = Vector2.zero;
        var label = bar.GetComponentInChildren<TMP_Text>(true);
        var font = label != null ? label.font : TMP_Settings.defaultFontAsset;
        _gauge = new HackGaugeOrbit(_parent, font, null, HackGaugeOrbit.Look.Segmented);
        _capacity = Mathf.Max(1, runtime.Ledger.Capacity);
        _gauge.Reset(0, _capacity);
        var pending = new GameObject("ReservedStacks", typeof(RectTransform), typeof(TextMeshProUGUI));
        pending.transform.SetParent(_parent, false);
        _reserved = pending.GetComponent<TextMeshProUGUI>();
        _reserved.font = font; _reserved.fontSize = 18; _reserved.alignment = TextAlignmentOptions.Center;
        _reserved.raycastTarget = false;
        _reserved.rectTransform.sizeDelta = new Vector2(220, 30);
    }
    private void LateUpdate()
    {
        if (_gauge == null || _runtime == null || _bar == null) return;
        var target = _runtime.Target;
        int capacity = Mathf.Max(1, _runtime.Ledger.Capacity);
        if (target != _target || capacity != _capacity)
        {
            _target = target;
            _capacity = capacity;
            _gauge.Reset(0, capacity);
            _version = _runtime.ConsumptionVersion;
            _totalConsumed = _runtime.TotalConsumed;
        }
        _bar.GetWorldCorners(_corners);
        Vector3 min = _parent.InverseTransformPoint(_corners[0]);
        Vector3 max = _parent.InverseTransformPoint(_corners[2]);
        _gauge.HudBar = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        _gauge.Set(_runtime.Ledger.Available + _runtime.Ledger.Reserved);
        if (_version != _runtime.ConsumptionVersion)
        {
            _version = _runtime.ConsumptionVersion;
            _gauge.Consume((int)System.Math.Min(int.MaxValue, _runtime.TotalConsumed - _totalConsumed));
            _totalConsumed = _runtime.TotalConsumed;
        }
        _gauge.Tick(Time.time, Vector2.zero);
        _reserved.text = _runtime.Ledger.Reserved > 0 ? $"확보 중 {_runtime.Ledger.Reserved}" : string.Empty;
        _reserved.rectTransform.anchoredPosition = new Vector2(max.x - 104, min.y - 330);
    }
    private void OnDestroy() { if (_parent != null) Destroy(_parent.gameObject); }
}
