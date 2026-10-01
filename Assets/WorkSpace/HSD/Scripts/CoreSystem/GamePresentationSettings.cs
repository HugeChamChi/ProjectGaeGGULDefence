using System;

/// <summary>Player 데이터에 저장된 피해 숫자와 진동 설정을 UI 및 출력기에 제공한다.</summary>
public sealed class GamePresentationSettings : IDisposable
{
    private readonly PlayerDataController _playerData;

    /// <summary>로드, 계정 해제 또는 설정 변경 시 알린다.</summary>
    public event Action OnChanged;
    /// <summary>저장 데이터가 없는 기존 계정은 피해 숫자를 표시한다.</summary>
    public bool ShowDamageNumbers => _playerData.Data?.ShowDamageNumbers ?? true;
    /// <summary>저장 데이터가 없는 기존 계정은 진동을 허용한다.</summary>
    public bool VibrationEnabled => _playerData.Data?.VibrationEnabled ?? true;

    /// <summary>기존 Player 데이터 컨트롤러를 사용한다.</summary>
    public GamePresentationSettings(PlayerDataController playerData)
    {
        _playerData = playerData;
        _playerData.OnPresentationSettingsChanged += Publish;
    }

    /// <summary>피해 숫자 설정을 기존 변경 감지 및 저장 경로로 전달한다.</summary>
    public void SetDamageNumbers(bool enabled) => _playerData.SetDamageNumbers(enabled);
    /// <summary>진동 설정을 기존 변경 감지 및 저장 경로로 전달한다.</summary>
    public void SetVibration(bool enabled) => _playerData.SetVibration(enabled);
    private void Publish() => OnChanged?.Invoke();
    /// <summary>앱 서비스가 종료되면 구독을 해제한다.</summary>
    public void Dispose() => _playerData.OnPresentationSettingsChanged -= Publish;
}
