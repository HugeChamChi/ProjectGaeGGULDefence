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

    // 스테미나 시스템 추가
    public PlayerData()
    {
        Stamina = MaxStamina;
        LastStaminaRecoveryTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

}