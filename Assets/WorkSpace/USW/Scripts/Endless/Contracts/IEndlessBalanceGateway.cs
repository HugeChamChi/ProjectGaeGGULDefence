using System.Threading;
using Cysharp.Threading.Tasks;
using LitJson;

/// <summary>Authenticated chart transport; exposes no local balance defaults.</summary>
public interface IEndlessBalanceGateway
{
    /// <summary>Whether an authenticated backend session exists.</summary>
    bool IsLoggedIn { get; }
    /// <summary>Current session identity used to reject late responses after account changes.</summary>
    string Account { get; }
    /// <summary>Loads the currently applied chart and observes the caller's timeout/lifetime.</summary>
    UniTask<JsonData> LoadAsync(CancellationToken token);
}
