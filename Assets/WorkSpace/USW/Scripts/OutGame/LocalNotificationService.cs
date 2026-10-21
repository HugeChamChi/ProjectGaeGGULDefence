using System;
using UnityEngine;
using VContainer.Unity;

/// <summary>App-owned reminders driven by existing data, recovery ticks, consent and focus events.</summary>
public sealed class LocalNotificationService : IStartable, IDisposable
{
    /// <summary>Stable Android ID for the one-shot stamina reminder.</summary>
    public const int StaminaId = 41001;
    /// <summary>Stable Android ID for the daily attendance reminder.</summary>
    public const int AttendanceId = 41002;
    private const double ClockDriftToleranceSeconds = 5;
    private readonly PlayerDataController _data;
    private readonly DeviceSettingsService _settings;
    private readonly LocalNotificationGateway _gateway;
    private readonly LocalNotificationConfig _config;
    private readonly AttendanceConfig _attendance;
    private string _owner;
    private DateTime? _stamina, _nextAttendance;
    private double _clockOffset;
    private bool _started, _hasPlan, _quitting;

    /// <summary>Receives root-scoped dependencies and the same authored attendance rules as gameplay.</summary>
    public LocalNotificationService(PlayerDataController data, DeviceSettingsService settings,
        LocalNotificationGateway gateway, LocalNotificationConfig config, AttendanceConfig attendance)
    { _data = data; _settings = settings; _gateway = gateway; _config = config; _attendance = attendance; }

    /// <inheritdoc />
    public void Start()
    {
        if (_started) return;
        _started = true;
        _data.OnUpdateUI += OnData;
        _data.OnStaminaRecoveryTimer += OnTimer;
        _settings.OnPushConsentChanged += OnConsent;
        Application.focusChanged += OnFocus;
        Application.quitting += OnQuitting;
        try { _gateway.Initialize(); _gateway.CancelAll(); }
        catch (Exception error) { Debug.LogWarning("[LocalNotification] Initialize: " + error.GetType().Name); }
        Refresh();
    }

    private void OnData(PlayerData _) => Refresh();
    private void OnTimer(int _) => Refresh();
    private void OnConsent(bool _) => Refresh(true);
    private void OnFocus(bool _) => Refresh(true);
    private void OnQuitting() => _quitting = true;

    /// <summary>Reconciles reminders; OFF/unknown owner/time cancel existing alarms, ON rebuilds them.</summary>
    public void Refresh(bool force = false)
    {
        if (_quitting) return;
        try
        {
            string owner = _gateway.Owner;
            bool ready = _settings.PushConsent && _data.IsReady && !string.IsNullOrEmpty(owner)
                && _gateway.ClockReady && _gateway.PermissionGranted && _config != null;
            if (!ready)
            {
                if (_hasPlan || force) _gateway.CancelAll();
                _hasPlan = false; _owner = null; _stamina = _nextAttendance = null;
                return;
            }
            DateTime now = _gateway.ServerUtc, device = _gateway.DeviceUtc;
            DateTime? stamina = LocalNotificationPlanner.StaminaFull(_data.Data, _gateway.RecoveryIntervalSeconds, now);
            DateTime? attendance = LocalNotificationPlanner.NextAttendance(_data.Data, _attendance, _config, now);
            double offset = (device - now).TotalSeconds;
            if (!force && _hasPlan && owner == _owner && stamina == _stamina && attendance == _nextAttendance
                && Math.Abs(offset - _clockOffset) < ClockDriftToleranceSeconds) return;
            _gateway.CancelAll();
            _hasPlan = false;
            if (stamina.HasValue) _gateway.Schedule(StaminaId, _config.Title, _config.StaminaMessage, device + (stamina.Value - now), false);
            if (attendance.HasValue) _gateway.Schedule(AttendanceId, _config.Title, _config.AttendanceMessage, device + (attendance.Value - now), true);
            _owner = owner; _stamina = stamina; _nextAttendance = attendance; _clockOffset = offset; _hasPlan = true;
        }
        catch (Exception error)
        {
            // Notification failures must never interrupt gameplay, saving, or account cleanup.
            _hasPlan = false;
            try { _gateway.CancelAll(); } catch (Exception) { }
            Debug.LogWarning("[LocalNotification] Schedule: " + error.GetType().Name);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_started) return;
        _started = false;
        _data.OnUpdateUI -= OnData;
        _data.OnStaminaRecoveryTimer -= OnTimer;
        _settings.OnPushConsentChanged -= OnConsent;
        Application.focusChanged -= OnFocus;
        Application.quitting -= OnQuitting;
        // OS alarms must survive normal application shutdown. Account clear cancels via OnData.
    }
}
