using System;
using BackEnd;

/// <summary>Backend boundary; tests can supply isolated accounts, transport and balance storage.</summary>
public class MailGateway
{
    /// <summary>Current authenticated account.</summary>
    public virtual string Owner => Backend.UserInDate;
    /// <summary>Existing account balance and durable receipt service.</summary>
    public virtual PlayerDataController Balances => Player.PlayerData;
    /// <summary>Approved reward mapping asset.</summary>
    public virtual MailRewardConfig RewardConfig => UnityEngine.Resources.Load<MailRewardConfig>("MailRewardConfig");
    /// <summary>Loads one SDK page without consuming rewards.</summary>
    public virtual void GetList(PostType type, Action<MailResponse> callback)
        => Backend.UPost.GetPostList(type, 100, result => callback(Convert(result)));
    /// <summary>Claims one previously loaded identity.</summary>
    public virtual void Claim(PostType type, string id, Action<MailResponse> callback)
        => Backend.UPost.ReceivePostItem(type, id, result => callback(Convert(result)));
    private static MailResponse Convert(BackendReturnObject result)
        => new() { StatusCode = result.StatusCode, Json = result.IsSuccess() ? result.GetReturnValuetoJSON().ToJson() : "{}" };
}
