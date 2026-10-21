using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;

/// <summary>Reads the applied BACKND chart by name, bypassing the legacy chart cache.</summary>
public sealed class BackendEndlessBalanceGateway : IEndlessBalanceGateway
{
    /// <inheritdoc />
    public bool IsLoggedIn => Backend.IsLogin;
    /// <inheritdoc />
    public string Account => Backend.UID;

    /// <inheritdoc />
    public async UniTask<JsonData> LoadAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var listCompletion = new UniTaskCompletionSource<BackendReturnObject>();
        Backend.Chart.GetChartListV2(response => listCompletion.TrySetResult(response));
        var list = await listCompletion.Task.AttachExternalCancellation(token);
        if (!list.IsSuccess()) throw new InvalidOperationException("Chart list: " + list.GetStatusCode());
        string fileId = null;
        var rows = list.FlattenRows();
        for (int i = 0; i < rows.Count; i++)
            if (rows[i]["chartName"].ToString() == EndlessBalanceService.ChartName)
            { fileId = rows[i]["selectedChartFileId"].ToString(); break; }
        if (string.IsNullOrEmpty(fileId) || fileId == "0")
            throw new InvalidOperationException("EndlessBalance chart is missing or not applied.");
        token.ThrowIfCancellationRequested();
        var dataCompletion = new UniTaskCompletionSource<BackendReturnObject>();
        Backend.Chart.GetChartContents(fileId, response => dataCompletion.TrySetResult(response));
        var data = await dataCompletion.Task.AttachExternalCancellation(token);
        if (!data.IsSuccess()) throw new InvalidOperationException("Chart contents: " + data.GetStatusCode());
        return data.FlattenRows();
    }
}
