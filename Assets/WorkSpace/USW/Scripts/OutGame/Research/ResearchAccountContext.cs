using System;
using BackEnd;

/// <summary>Tracks authenticated account generations and notifies research views after reload.</summary>
public sealed class ResearchAccountContext : IDisposable
{
    public string AccountKey { get; private set; }
    public long Generation { get; private set; }
    public event Action OnAccountChanged;
    public ResearchAccountContext()
    {
        AccountKey = CurrentKey();
        Player.OnAccountCleared += Cleared;
        Player.OnAccountReady += Ready;
    }
    private static string CurrentKey() => Backend.IsLogin && !string.IsNullOrEmpty(Backend.UserInDate)
        ? Backend.UserInDate : ResearchSaveAddress.LocalAccount;
    private void Cleared() { Generation++; AccountKey = ResearchSaveAddress.LocalAccount; }
    private void Ready() { AccountKey = CurrentKey(); Generation++; OnAccountChanged?.Invoke(); }
    public void SetAccountKey(string key)
    {
        string next = string.IsNullOrWhiteSpace(key) ? ResearchSaveAddress.LocalAccount : key;
        if (next == AccountKey) return;
        AccountKey = next; Generation++; OnAccountChanged?.Invoke();
    }
    public void Dispose() { Player.OnAccountCleared -= Cleared; Player.OnAccountReady -= Ready; }
}
