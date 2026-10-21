using System;
using BackEnd;

/// <summary>Backend boundary for explicitly confirmed account withdrawal.</summary>
public class AccountDeletionGateway
{
    /// <summary>Account that owns the active session.</summary>
    public virtual string Owner => Backend.UserInDate;
    /// <summary>Whether a withdrawal can be requested for this session.</summary>
    public virtual bool LoggedIn => Backend.IsLogin;
    /// <summary>Editor sessions use shared test accounts and must never withdraw them.</summary>
    public virtual bool CanWithdraw => !UnityEngine.Application.isEditor;
    /// <summary>Requests immediate withdrawal; only a successful server response confirms deletion.</summary>
    public virtual void Withdraw(Action<bool> completed) => Backend.BMember.WithdrawAccount(response => completed(response.IsSuccess()));
    /// <summary>Clears credentials and in-memory data after the server confirms withdrawal.</summary>
    public virtual void ClearConfirmedSession()
    {
        Backend.BMember.DeleteGuestInfo();
        GoogleAccountAuth.ForgetGoogleLogin();
        Player.ClearAll();
    }
}
