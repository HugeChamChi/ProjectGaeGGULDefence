using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;

/// <summary>BACKND nickname changes, with owner checks even for late callbacks.</summary>
public class ProfileNicknameGateway
{
    private UniTaskCompletionSource<int> _request;
    private string _requestOwner;
    private int _revision;
    /// <summary>Authenticated account identity, never its display name.</summary>
    public virtual string Owner => Backend.UserInDate;
    /// <summary>Whether current account data can be changed.</summary>
    public virtual bool IsReady => Backend.IsLogin && Player.PlayerData.IsReady;
    /// <summary>A request remains in flight until the SDK reports its actual outcome.</summary>
    public virtual bool IsChanging => _request != null && _requestOwner == Owner;
    /// <summary>Current account display name.</summary>
    public virtual string Nickname => Player.PlayerData.Data?.PlayerName ?? "";

    /// <summary>Returns the SDK status. Caller cancellation never discards a confirmed late success.</summary>
    public virtual async UniTask<int> ChangeAsync(string name, CancellationToken token)
    {
        if (!IsReady || IsChanging) return 0;
        string owner = Owner;
        var data = Player.PlayerData.Data;
        _revision++;
        var completion = new UniTaskCompletionSource<int>();
        _request = completion; _requestOwner = owner;
        try
        {
            Backend.BMember.UpdateNickname(name, response =>
            {
                if (response.IsSuccess() && owner == Owner && ReferenceEquals(data, Player.PlayerData.Data)) ApplyName(name);
                if (ReferenceEquals(_request, completion)) _request = null;
                completion.TrySetResult(response.StatusCode);
            });
        }
        catch { if (ReferenceEquals(_request, completion)) _request = null; throw; }
        return await completion.Task.AttachExternalCancellation(token);
    }

    /// <summary>Reconciles a server-confirmed name after reopening or restarting the app.</summary>
    public virtual async UniTask RefreshAsync(CancellationToken token)
    {
        if (!IsReady || IsChanging) return;
        string owner = Owner;
        int revision = _revision;
        var data = Player.PlayerData.Data;
        var completion = new UniTaskCompletionSource<BackendReturnObject>();
        Backend.BMember.GetUserInfoV2(response => completion.TrySetResult(response));
        var result = await completion.Task.AttachExternalCancellation(token);
        if (owner != Owner || revision != _revision || !ReferenceEquals(data, Player.PlayerData.Data) || !result.IsSuccess()) return;
        var row = result.GetReturnValuetoJSON()["row"];
        if (row.ContainsKey("nickname") && row["nickname"] != null) ApplyName(row["nickname"].ToString());
    }

    /// <summary>Persists the confirmed display name using the existing account journal.</summary>
    public virtual UniTask<bool> SaveAsync() => Player.PlayerData.TrySaveAsync();

    private static void ApplyName(string name)
    {
        Player.PlayerData.SetNickname(name);
        Player.RequestDebouncedSave();
    }
}
