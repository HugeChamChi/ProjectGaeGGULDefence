using System;

[System.Serializable]
public class PlayerData
{
    /// <summary>Last absolute snapshot identifier, for retry acknowledgement and local journal reconciliation.</summary>
    public string SaveId = string.Empty;
    /// <summary>Paid entry reserved until a production battle actually starts.</summary>
    public string PendingEntryId = string.Empty;
    /// <summary>Cost already deducted for the pending entry.</summary>
    public int PendingEntryCost;
    /// <summary>Account coupon history; saved in the same snapshot as its currency grants.</summary>
    public System.Collections.Generic.List<CouponReceipt> CouponHistory = new();
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

    /// <summary>Days claimed in the current 7-day attendance cycle (0-7).</summary>
    public int AttendanceCount;
    /// <summary>Server-day key of the last attendance claim; one claim per server day.</summary>
    public string AttendanceLastDay = string.Empty;

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
