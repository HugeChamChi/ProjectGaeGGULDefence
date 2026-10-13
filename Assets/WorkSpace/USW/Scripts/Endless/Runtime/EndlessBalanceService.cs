using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>Optional remote NORMAL tuning. Each refresh bypasses the legacy Chart cache.</summary>
public sealed class EndlessBalanceService
{
    /// <summary>Exact backend chart name expected by this client.</summary>
    public const string ChartName = "EndlessBalance";
    private const int TimeoutSeconds = 5;
    /// <summary>Last refresh result; null means use authored SO settings.</summary>
    public EndlessBalanceSnapshot Current { get; private set; }
    /// <summary>Prevents a run from capturing settings while a refresh is in flight.</summary>
    public bool IsRefreshing { get; private set; }

    /// <summary>Refreshes before scene readiness; cancellation propagates and every other failure falls back to SO.</summary>
    public async UniTask RefreshAsync(CancellationToken cancellationToken)
    {
        Current = null;
        cancellationToken.ThrowIfCancellationRequested();
        if (!Backend.IsLogin) return;
        IsRefreshing = true;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(TimeoutSeconds), DelayType.Realtime);
        try
        {
            var listCompletion = new UniTaskCompletionSource<BackendReturnObject>();
            Backend.Chart.GetChartListV2(response => listCompletion.TrySetResult(response));
            var list = await listCompletion.Task.AttachExternalCancellation(timeout.Token);
            if (!list.IsSuccess()) throw new InvalidOperationException("Chart list: " + list.GetStatusCode());
            string fileId = null;
            var rows = list.FlattenRows();
            for (int i = 0; i < rows.Count; i++)
                if (rows[i]["chartName"].ToString() == ChartName)
                { fileId = rows[i]["selectedChartFileId"].ToString(); break; }
            if (string.IsNullOrEmpty(fileId) || fileId == "0")
                throw new InvalidOperationException("EndlessBalance chart is missing or not applied.");
            var dataCompletion = new UniTaskCompletionSource<BackendReturnObject>();
            Backend.Chart.GetChartContents(fileId, response => dataCompletion.TrySetResult(response));
            var data = await dataCompletion.Task.AttachExternalCancellation(timeout.Token);
            if (!data.IsSuccess() || !EndlessBalanceSnapshot.TryParse(data.FlattenRows(), out var snapshot))
                throw new InvalidOperationException("Invalid EndlessBalance chart data.");
            cancellationToken.ThrowIfCancellationRequested();
            Current = snapshot;
            Debug.Log("[EndlessBalance] Remote chart loaded; applies to the next NORMAL run.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error)
        { Debug.LogWarning("[EndlessBalance] Using authored SO settings: " + error.Message); }
        finally { IsRefreshing = false; }
    }
}
