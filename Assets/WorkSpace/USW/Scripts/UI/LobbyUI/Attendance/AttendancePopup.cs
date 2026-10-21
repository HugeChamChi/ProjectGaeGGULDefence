using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// 7-day attendance popup: days 1-6 in a 2x3 grid and a featured day 7 on the right.
/// Tapping today's card claims it; progress and reward grants are saved in the account PlayerData snapshot.
/// </summary>
public sealed class AttendancePopup : UI_Base
{
    private const float CountdownRefreshSeconds = 1f;

    [SerializeField] private AttendanceConfig _config;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private TMP_Text _footer;
    [SerializeField] private AttendanceDaySlot[] _slots = new AttendanceDaySlot[AttendanceConfig.CycleDays];
    [SerializeField] private Sprite _goldIcon;
    [SerializeField] private Sprite _diamondIcon;
    [SerializeField] private Sprite _staminaIcon;

    private AttendanceService _service;
    private bool _timeReady;
    private bool _claiming;
    private string _renderedDay;
    private float _nextCountdown;
    private string _notice;

    /// <summary>Raised after a successful grant so the lobby can refresh badges.</summary>
    public event Action OnClaimed;
    /// <summary>Attendance flow; created from the authored config.</summary>
    public AttendanceService Service => _service ??= new AttendanceService(_config);

    protected override void Awake()
    {
        base.Awake();
        for (int i = 0; i < _slots.Length; i++)
        {
            int day = i + 1;
            if (_slots[i] != null && _slots[i].Button != null) _slots[i].Button.onClick.AddListener(() => OnSlotPressed(day));
        }
        if (_title != null && _config != null) _title.text = _config.Title;
        if (_footer != null && _config != null) _footer.text = _config.Footer;
    }

    /// <inheritdoc />
    public override void Open()
    {
        base.Open();
        _notice = null; _timeReady = Service.UseSynchronizedClock(); Render();
    }

    /// <inheritdoc />
    public override async UniTask CloseAsync()
    {
        await base.CloseAsync();
        foreach (var slot in _slots) slot?.StopEffects();
    }

    private void OnSlotPressed(int day)
    {
        if (_claiming) return;
        var slot = _slots[day - 1];
        var status = CurrentStatus();
        if (!_timeReady || status.ClaimableDay != day)
        {
            slot.PlayRefused();
            _notice = !_timeReady ? null
                : slot.Current == AttendanceDaySlot.State.Claimed ? "이미 받은 보상입니다."
                : status.ClaimedToday ? "오늘 출석 보상은 이미 받았어요."
                : $"{day}일차 보상은 아직 받을 수 없어요.";
            Render();
            return;
        }
        ClaimAsync().Forget();
    }

    private async UniTaskVoid ClaimAsync()
    {
        _claiming = true; SetStatus("보상을 받는 중…");
        try
        {
            var (result, day) = await Service.ClaimAsync(this.GetCancellationTokenOnDestroy());
            if (this == null) return;
            _timeReady = Service.LastServerUtc != null;
            _notice = result switch
            {
                AttendanceService.ClaimResult.Claimed => $"{day}일차 보상 {RewardText(day)} 지급 완료!",
                AttendanceService.ClaimResult.ClaimedSavePending => $"{day}일차 보상 {RewardText(day)} 지급, 서버 저장을 다시 시도하는 중",
                AttendanceService.ClaimResult.AlreadyClaimed => "오늘 출석 보상은 이미 받았어요.",
                AttendanceService.ClaimResult.TimeUnavailable => "서버 시간을 확인하지 못했어요. 잠시 후 다시 시도해 주세요.",
                AttendanceService.ClaimResult.Invalid => "출석 보상 설정을 확인할 수 없어요.",
                _ => "계정 정보를 불러오는 중이에요. 잠시 후 다시 시도해 주세요."
            };
            Render();
            if (day > 0) { _slots[day - 1].PlayClaimed(); OnClaimed?.Invoke(); }
        }
        catch (OperationCanceledException) { }
        finally { _claiming = false; }
    }

    private AttendanceRules.Status CurrentStatus() => _timeReady ? Service.Evaluate() : default;

    private void Render()
    {
        var status = CurrentStatus();
        _renderedDay = _timeReady && _config != null ? _config.DayKey(Service.EstimatedServerUtc) : null;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;
            var reward = _config != null && _config.Rewards != null && i < _config.Rewards.Length ? _config.Rewards[i] : null;
            var state = i < status.Claimed ? AttendanceDaySlot.State.Claimed
                : i + 1 == status.ClaimableDay ? AttendanceDaySlot.State.Claimable
                : AttendanceDaySlot.State.Locked;
            _slots[i].Bind(i + 1, reward != null ? IconFor(reward.Currency) : null, reward?.Amount ?? 0, state);
        }
        SetStatus(StatusText(status));
    }

    private string StatusText(AttendanceRules.Status status)
    {
        if (_config == null || !_config.IsValid) return "출석 보상 설정을 확인할 수 없어요.";
        if (!_timeReady || Player.PlayerData?.Data == null) return _notice ?? "";
        string prefix = string.IsNullOrEmpty(_notice) ? "" : _notice + "\n";
        if (status.CanClaim) return prefix + $"오늘의 보상({status.ClaimableDay}일차)을 눌러 받으세요.";
        if (status.ClaimedToday) return prefix + "오늘 출석 완료  다음 출석까지 " + Countdown();
        return prefix + "서버 날짜를 확인할 수 없어 출석을 잠시 막았어요.";
    }

    private string Countdown()
    {
        var left = _config.NextReset(Service.EstimatedServerUtc) - Service.EstimatedServerUtc;
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        return $"{(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}";
    }

    private void Update()
    {
        if (_claiming || (_canvas != null && !_canvas.enabled) || Time.unscaledTime < _nextCountdown) return;
        _nextCountdown = Time.unscaledTime + CountdownRefreshSeconds;
        bool wasReady = _timeReady;
        _timeReady = Service.UseSynchronizedClock();
        if (!_timeReady) { if (wasReady) Render(); return; }
        if (!wasReady) { Render(); return; }
        // Crossing the reset boundary while open re-evaluates the new day; otherwise only the countdown text changes.
        if (_config.DayKey(Service.EstimatedServerUtc) != _renderedDay) { _notice = null; Render(); }
        else if (CurrentStatus().ClaimedToday) SetStatus(StatusText(CurrentStatus()));
    }

    private string RewardText(int day)
    {
        var reward = _config.Rewards[day - 1];
        string name = reward.Currency switch { MailRewardConfig.Currency.Gold => "골드", MailRewardConfig.Currency.Diamond => "다이아", _ => "스태미나" };
        return $"{name} {reward.Amount:N0}";
    }

    private Sprite IconFor(MailRewardConfig.Currency currency) => currency switch
    {
        MailRewardConfig.Currency.Gold => _goldIcon,
        MailRewardConfig.Currency.Diamond => _diamondIcon,
        _ => _staminaIcon
    };

    private void SetStatus(string text) { if (_status != null) _status.text = text; }
}
