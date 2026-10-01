using System;

[System.Serializable]
public class PlayerData
{
    public string PlayerUid;
    public string PlayerName;
    public int Gold;
    public int Diamond;

    public int Stamina;
    public int MaxStamina = 30;
    public long LastStaminaRecoveryTime;

    public int PlayerLevel;
    public int PlayerExp;
    public int MaxExp = 500;

    public string LastResetDate;

    /// <summary>피해 숫자 표시 여부. 이전 저장 데이터의 기본값은 켜짐.</summary>
    public bool ShowDamageNumbers = true;
    /// <summary>진동 허용 여부. 이전 저장 데이터의 기본값은 켜짐.</summary>
    public bool VibrationEnabled = true;

    // 스테미나 시스템 추가
    public PlayerData()
    {
        Stamina = MaxStamina;
        LastStaminaRecoveryTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

}
