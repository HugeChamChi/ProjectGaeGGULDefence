using System;

[System.Serializable]
public class PlayerData
{
    /// <summary>Stable chart reward keys, saved atomically with the granted balances.</summary>
    public System.Collections.Generic.List<string> RoundRewardClaims = new();
    public UpgradeProgressData UpgradeProgress = new();
    /// <summary>Last absolute snapshot identifier, for retry acknowledgement and local journal reconciliation.</summary>
    public string SaveId = string.Empty;
    public System.Collections.Generic.List<UnitLevelEntry> UnitLevels = new();
    [Serializable] public sealed class UnitLevelEntry { public string UnitKey; public int Level = 1; }
    public RewardInventory RewardInventory = new();
    public string WeeklyShopPurchasePeriod = string.Empty;
    public System.Collections.Generic.List<int> WeeklyShopPurchases = new();
    public string ShopPurchaseDay = string.Empty;
    public System.Collections.Generic.List<int> ShopPurchases = new();
    public System.Collections.Generic.List<string> CompletedTutorials = new();
    // -1 means the legacy row is still the source for this selection.
    public int SelectedChiefId = -1;
    public int SelectedIconId = -1;
    public int SelectedFrameId = -1;
    /// <summary>Paid entry reserved until a production battle actually starts.</summary>
    public string PendingEntryId = string.Empty;
    /// <summary>Cost already deducted for the pending entry.</summary>
    public int PendingEntryCost;
    /// <summary>Account coupon history; saved in the same snapshot as its currency grants.</summary>
    public System.Collections.Generic.List<CouponReceipt> CouponHistory = new();
    /// <summary>Paid diamond grants, saved atomically with the balance for redelivery deduplication.</summary>
    public System.Collections.Generic.List<GaeGGUL.IAP.DiamondPurchaseReceipt> DiamondPurchases = new();
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

    /// <summary>Monotonic battle reservation number; never reset when abandoning a run.</summary>
    public long AccountExperienceSequence;
    /// <summary>Current reserved battle ID, journaled before boss callbacks.</summary>
    public string AccountExperienceRunId = string.Empty;
    /// <summary>Experience, level changes and this receipt are one absolute PlayerData snapshot.</summary>
    public AccountExperienceReceipt AccountExperienceReceipt;

    public string LastResetDate;

    /// <summary>Days claimed in the current 7-day attendance cycle (0-7).</summary>
    public System.Collections.Generic.List<GGD.Events.SavedMission> EventMissions = new();
    public System.Collections.Generic.List<string> EventMissionReceipts = new();
    public int AttendanceCount;
    public System.Collections.Generic.List<GGD.Events.AttendanceEventProgress> EventAttendance = new();
    public int AttendancePendingDays;
    public string AttendanceLastVisitDay;
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
