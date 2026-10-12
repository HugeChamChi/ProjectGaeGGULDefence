using System;

/// <summary>
/// Player 데이터에 저장된 피해 숫자와 진동 설정을 UI 및 출력기에 제공한다.
/// Player 데이터가 아직 없을 때(튜토리얼 중 백그라운드 로그인 전, 에디터 직접 실행) 바꾼 값은
/// 이번 실행 동안 유지하고, 데이터가 로드되면 기존 저장 경로로 넘긴다 — 토글이 눌러도 되돌아가던 문제 수정(10-13).
/// </summary>
public sealed class GamePresentationSettings : IDisposable
{
    private readonly PlayerDataController _playerData;
    private bool? _pendingDamageNumbers;
    private bool? _pendingVibration;

    /// <summary>로드, 계정 해제 또는 설정 변경 시 알린다.</summary>
    public event Action OnChanged;
    /// <summary>저장 데이터가 없는 기존 계정은 피해 숫자를 표시한다.</summary>
    public bool ShowDamageNumbers => _playerData.Data?.ShowDamageNumbers ?? _pendingDamageNumbers ?? true;
    /// <summary>저장 데이터가 없는 기존 계정은 진동을 허용한다.</summary>
    public bool VibrationEnabled => _playerData.Data?.VibrationEnabled ?? _pendingVibration ?? true;

    /// <summary>기존 Player 데이터 컨트롤러를 사용한다.</summary>
    public GamePresentationSettings(PlayerDataController playerData)
    {
        _playerData = playerData;
        _playerData.OnPresentationSettingsChanged += Publish;
    }

    /// <summary>피해 숫자 설정을 기존 변경 감지 및 저장 경로로 전달한다. 데이터 로드 전이면 로드될 때까지 보관한다.</summary>
    public void SetDamageNumbers(bool enabled)
    {
        if (_playerData.Data == null) { _pendingDamageNumbers = enabled; Publish(); return; }
        _playerData.SetDamageNumbers(enabled);
    }

    /// <summary>진동 설정을 기존 변경 감지 및 저장 경로로 전달한다. 데이터 로드 전이면 로드될 때까지 보관한다.</summary>
    public void SetVibration(bool enabled)
    {
        if (_playerData.Data == null) { _pendingVibration = enabled; Publish(); return; }
        _playerData.SetVibration(enabled);
    }

    private void Publish()
    {
        if (_playerData.Data != null) FlushPending();
        OnChanged?.Invoke();
    }

    // 로드 전에 바꾼 값을 저장 경로로 넘긴다. 넘기는 중 다시 들어오는 Publish는 보관값이 비어 있어 아무것도 안 한다.
    private void FlushPending()
    {
        if (_pendingDamageNumbers is bool damage) { _pendingDamageNumbers = null; _playerData.SetDamageNumbers(damage); }
        if (_pendingVibration is bool vibration) { _pendingVibration = null; _playerData.SetVibration(vibration); }
    }

    /// <summary>앱 서비스가 종료되면 구독을 해제한다.</summary>
    public void Dispose() => _playerData.OnPresentationSettingsChanged -= Publish;
}
