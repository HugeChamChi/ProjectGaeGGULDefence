using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Required remote NORMAL balance. A failed refresh cannot start a run using local or stale values.</summary>
public sealed class EndlessBalanceService
{
    /// <summary>Exact backend chart name expected by this client.</summary>
    public const string ChartName = "EndlessBalance";
    private const int TimeoutSeconds = 5;
    /// <summary>Last successful refresh; null means NORMAL entry is blocked.</summary>
    public EndlessBalanceSnapshot Current { get; private set; }
    /// <summary>Prevents a run from capturing settings while a refresh is in flight.</summary>
    public bool IsRefreshing { get; private set; }
    private bool _preparedEntry;
    private string _preparedAccount;
    private readonly IEndlessBalanceGateway _gateway;

    /// <summary>Uses an injected authenticated gateway; tests can provide isolated transport failures.</summary>
    public EndlessBalanceService(IEndlessBalanceGateway gateway) => _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

    /// <summary>Loads required settings before stamina reservation, then carries this exact snapshot into the scene.</summary>
    public async UniTask PrepareEntryAsync(CancellationToken cancellationToken, EndlessModeData mode = null)
    {
        await RefreshAsync(cancellationToken);
        if (mode != null)
        {
            using var validation = new EndlessRunService(new EndlessRandom());
            if (!validation.TryStart(mode, out string error, Current))
            {
                Current = null;
                throw new InvalidOperationException(error);
            }
        }
        _preparedAccount = _gateway.Account;
        _preparedEntry = true;
    }

    /// <summary>Consumes a successful lobby preparation, or fetches settings for direct scene entry.</summary>
    public async UniTask PrepareSceneAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool prepared = _preparedEntry && Current != null && _gateway.IsLoggedIn && _preparedAccount == _gateway.Account;
        _preparedEntry = false;
        _preparedAccount = null;
        if (!prepared) await RefreshAsync(cancellationToken);
    }

    /// <summary>Refreshes the applied chart. Missing login, timeout, malformed data and network failure block entry.</summary>
    public async UniTask RefreshAsync(CancellationToken cancellationToken)
    {
        if (IsRefreshing) throw new InvalidOperationException("Battle settings are already loading.");
        Current = null;
        _preparedEntry = false;
        _preparedAccount = null;
        cancellationToken.ThrowIfCancellationRequested();
        if (!_gateway.IsLoggedIn) throw new InvalidOperationException("로그인 후 전투 설정을 확인해 주세요.");
        string account = _gateway.Account;
        IsRefreshing = true;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(TimeoutSeconds), DelayType.Realtime);
        try
        {
            var data = await _gateway.LoadAsync(timeout.Token).AttachExternalCancellation(timeout.Token);
            if (!EndlessBalanceSnapshot.TryParse(data, out var snapshot))
                throw new InvalidOperationException("Invalid EndlessBalance chart data.");
            cancellationToken.ThrowIfCancellationRequested();
            if (!_gateway.IsLoggedIn || account != _gateway.Account) throw new InvalidOperationException("Account changed while loading battle settings.");
            Current = snapshot;
            Debug.Log("[EndlessBalance] Remote chart loaded; applies to the next NORMAL run.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            Current = null;
            Debug.LogWarning("[EndlessBalance] Entry blocked: " + error.Message);
            throw new InvalidOperationException("전투 설정을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.", error);
        }
        finally { IsRefreshing = false; }
    }
}
