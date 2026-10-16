using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Authentication, browser and mail boundary for web coupons; isolated tests override this boundary.</summary>
public class WebCouponGateway
{
    /// <summary>Current authenticated backend owner.</summary>
    public virtual string Owner => Backend.UserInDate;
    /// <summary>Public coupon UID, never a login token.</summary>
    public virtual string Uid => Backend.UID;
    /// <summary>Actual authenticated nickname or the matching PlayerData mirror.</summary>
    public virtual string Nickname => !string.IsNullOrEmpty(Backend.UserNickName) ? Backend.UserNickName : Player.PlayerData.IsReady ? Player.PlayerData.Data.PlayerName : "";
    /// <summary>Whether a user is logged in.</summary>
    public virtual bool LoggedIn => Backend.IsLogin;
    /// <summary>Monotonic clock for accidental double-tap suppression.</summary>
    public virtual float Now => Time.realtimeSinceStartup;
    /// <summary>Shared mail loading/claim lock.</summary>
    public virtual bool MailBusy => Player.Mail.IsBusy;
    /// <summary>Last mail result; does not imply coupon registration success.</summary>
    public virtual string MailStatus => Player.Mail.Status;
    /// <summary>Uses the platform browser; opening a page never grants currency.</summary>
    public virtual void OpenUrl(string url) => Application.OpenURL(url);
    /// <summary>Loads the current account's pending coupon mail, without claiming it.</summary>
    public virtual UniTask ReloadMailAsync() => Player.Mail.LoadAsync(PostType.Coupon);
    /// <summary>Preserves already-captured direct-coupon responses during migration; never redeems a code.</summary>
    public virtual async UniTask<string> RecoverLegacyAsync()
    {
        var result = await Table.Coupon.RecoverPendingAsync();
        return result.Message;
    }
}
