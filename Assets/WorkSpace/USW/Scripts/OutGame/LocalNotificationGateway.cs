using System;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

/// <summary>Android local-notification boundary; Editor never schedules device notifications.</summary>
public class LocalNotificationGateway
{
    private const string ChannelId = "ggd_local_reminders_v1";
    /// <summary>Whether the backend clock is trustworthy in this session.</summary>
    public virtual bool ClockReady => ServerClock.IsSynced;
    /// <summary>Current server UTC estimate.</summary>
    public virtual DateTime ServerUtc => ServerClock.UtcNow;
    /// <summary>OS UTC used only to translate server-time remaining duration into an Android alarm.</summary>
    public virtual DateTime DeviceUtc => DateTime.UtcNow;
    /// <summary>Current account identity; never embedded in notifications.</summary>
    public virtual string Owner => BackEnd.Backend.IsLogin ? BackEnd.Backend.UserInDate : null;
    /// <summary>Same authored recovery interval used by BackendGameData; missing data disables stamina reminders.</summary>
    public virtual int RecoveryIntervalSeconds => RM.Load<StaminaConfig>("Data/StaminaConfig")?.recoveryIntervalSeconds ?? 0;
    /// <summary>Real OS permission; does not request another permission dialog.</summary>
    public virtual bool PermissionGranted => DeviceSettingsService.NotificationPermissionGranted;
    /// <summary>Registers the app's reminder channel without requesting permission.</summary>
    public virtual void Initialize()
    {
#if UNITY_ANDROID
        if (Application.isEditor) return;
        AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
        { Id = ChannelId, Name = "게임 알림", Importance = Importance.Default, Description = "스태미나 회복과 출석 보상 알림" });
#endif
    }
    /// <summary>Cancels scheduled and displayed local notifications on OFF, logout, or replacement.</summary>
    public virtual void CancelAll()
    {
#if UNITY_ANDROID
        if (Application.isEditor) return;
        AndroidNotificationCenter.CancelAllNotifications();
#endif
    }
    /// <summary>Schedules a stable ID using the server-derived remaining duration; attendance repeats daily.</summary>
    public virtual void Schedule(int id, string title, string message, DateTime deviceUtc, bool daily)
    {
#if UNITY_ANDROID
        if (Application.isEditor) return;
        var notification = new AndroidNotification { Title = title, Text = message,
            FireTime = deviceUtc.ToLocalTime(), ShouldAutoCancel = true };
        if (daily) notification.RepeatInterval = TimeSpan.FromDays(1);
        AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, id);
#endif
    }
}
