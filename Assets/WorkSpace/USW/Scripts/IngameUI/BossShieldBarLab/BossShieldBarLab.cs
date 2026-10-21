using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 HP바 보호막 시안 실험실(FxLab_BossShieldBar) 드라이버 (사용자 요청 2026-10-22).
/// 같은 가짜 보스 HP·보호막을 A안(현재 줄 위, 최대 한 줄)과 B안(바 오른쪽 끝부터 최대HP 비율) 두 바에 동시에 보낸다.
/// 피해는 보호막이 먼저 흡수하고 남은 양만 HP를 깎는다 (BossBase 보호막 규칙). 실험실 전용.
/// </summary>
public sealed class BossShieldBarLab : MonoBehaviour
{
    [SerializeField] private UI_BossHpBar[] _bars;
    [SerializeField] private UI_BossHpShield[] _shields;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private Slider _shieldSlider;
    [SerializeField] private TMP_Text _autoLabel;

    [Header("Fake Boss (도마뱀 기준)")]
    [SerializeField] private float _maxHp = 25000f;
    [SerializeField] private float _hpPerLine = 500f;
    [Tooltip("시작 HP 비율. 현재 줄이 절반쯤 남게 둔다")]
    [SerializeField, Range(0f, 1f)] private float _startHpRatio = 0.5296f;
    [Tooltip("슬라이더 최대 보호막 (최대HP 비율)")]
    [SerializeField, Range(0.01f, 1f)] private float _sliderMaxRatio = 0.3f;
    [SerializeField] private float _smallHitLines = 0.3f;
    [SerializeField] private float _bigHitLines = 2f;
    [Tooltip("깨지기 직전 프리셋 (줄 단위)")]
    [SerializeField] private float _nearBreakLines = 0.15f;
    [SerializeField] private float _autoHitInterval = 0.35f;
    [SerializeField] private float _autoHitLines = 0.6f;

    private decimal _hp;
    private decimal _shield;
    private bool _auto;
    private float _autoTimer;

    private decimal MaxHp => (decimal)_maxHp;
    private decimal PerLine => (decimal)_hpPerLine;

    private void Start()
    {
        if (_shieldSlider != null)
        {
            _shieldSlider.minValue = 0f;
            _shieldSlider.maxValue = _sliderMaxRatio;
            _shieldSlider.onValueChanged.AddListener(OnSlider);
        }
        ResetBoss();
    }

    private void OnDestroy()
    {
        if (_shieldSlider != null) _shieldSlider.onValueChanged.RemoveListener(OnSlider);
    }

    private void Update()
    {
        if (!_auto) return;
        _autoTimer += Time.unscaledDeltaTime;
        if (_autoTimer < _autoHitInterval) return;
        _autoTimer = 0f;
        Hit(_autoHitLines);
    }

    /// <summary>HP를 시작값으로 되돌리고 보호막을 즉시 비운다.</summary>
    public void ResetBoss()
    {
        _hp = Math.Round(MaxHp * (decimal)_startHpRatio);
        _shield = 0m;
        foreach (var bar in _bars) bar?.BeginBoss(_hp, MaxHp, 0, PerLine);
        foreach (var shield in _shields)
        {
            if (shield == null) continue;
            shield.SetShieldExact(0m, MaxHp, PerLine);
            shield.ClearImmediate();
        }
        Apply();
    }

    /// <summary>최대HP 비율만큼 보호막을 부여한다 (기존 보호막을 덮어쓴다).</summary>
    public void GrantRatio(float ratio) => SetShield(Math.Round(MaxHp * (decimal)ratio));

    /// <summary>10% 부여 (도마뱀 가시방벽 값).</summary>
    public void Grant10() => GrantRatio(0.1f);

    /// <summary>30% 부여 (슬라이더 최대).</summary>
    public void Grant30() => GrantRatio(_sliderMaxRatio);

    /// <summary>깨지기 직전 양으로 맞춘다.</summary>
    public void NearBreak() => SetShield(Math.Round(PerLine * (decimal)_nearBreakLines));

    /// <summary>보호막을 0으로 (깨짐 연출).</summary>
    public void ClearShield() => SetShield(0m);

    /// <summary>작은 피격.</summary>
    public void SmallHit() => Hit(_smallHitLines);

    /// <summary>큰 피격.</summary>
    public void BigHit() => Hit(_bigHitLines);

    /// <summary>자동 피격을 켜고 끈다.</summary>
    public void ToggleAuto()
    {
        _auto = !_auto;
        _autoTimer = 0f;
        if (_autoLabel != null) _autoLabel.text = _auto ? "자동 피격 끄기" : "자동 피격";
    }

    /// <summary>두 바 모두 같은 길이 기준으로 바꾼다 (캡처 비교용).</summary>
    public void SetSpan(int span)
    {
        foreach (var shield in _shields) if (shield != null) shield.Span = (UI_BossHpShield.ShieldSpan)span;
    }

    private void OnSlider(float ratio) => SetShield(Math.Round(MaxHp * (decimal)ratio), false);

    private void SetShield(decimal amount, bool syncSlider = true)
    {
        _shield = Math.Max(0m, amount);
        Apply(syncSlider);
    }

    private void Hit(float lines)
    {
        decimal damage = PerLine * (decimal)lines;
        decimal absorbed = Math.Min(_shield, damage);
        _shield -= absorbed;
        _hp -= damage - absorbed;
        if (_hp <= 0m) _hp = Math.Round(MaxHp * (decimal)_startHpRatio);
        Apply();
    }

    private void Apply(bool syncSlider = true)
    {
        foreach (var bar in _bars) bar?.SetHpExact(_hp, MaxHp);
        foreach (var shield in _shields) shield?.SetShieldExact(_shield, MaxHp, PerLine);
        if (syncSlider && _shieldSlider != null)
            _shieldSlider.SetValueWithoutNotify(Mathf.Clamp((float)(_shield / MaxHp), 0f, _sliderMaxRatio));
        if (_status != null)
        {
            var c = CultureInfo.InvariantCulture;
            _status.text = string.Format(c, "HP {0:N0} / {1:N0}  ·  보호막 {2:N0} ({3:0.0}%) = {4:0.00}줄",
                _hp, MaxHp, _shield, _shield / MaxHp * 100m, _shield / PerLine);
        }
    }
}
