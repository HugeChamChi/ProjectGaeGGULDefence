using UnityEngine;

/// <summary>Authored local reminder text and Korean notification time, separate from the attendance reset.</summary>
[CreateAssetMenu(menuName = "Game/Local Notification Config", fileName = "LocalNotificationConfig")]
public sealed class LocalNotificationConfig : ScriptableObject
{
    /// <summary>Title shared by both reminders.</summary>
    public string Title = "개꿀 디펜스";
    /// <summary>User-selected stamina-full message.</summary>
    [TextArea] public string StaminaMessage = "스테미나 가득참";
    /// <summary>User-selected unclaimed-attendance message.</summary>
    [TextArea] public string AttendanceMessage = "일일출석보상 미수령";
    /// <summary>User-selected KST hour; independent of the device timezone.</summary>
    [Range(0, 23)] public int AttendanceHourKst;
    /// <summary>Minute within the selected KST hour.</summary>
    [Range(0, 59)] public int AttendanceMinute;
}
