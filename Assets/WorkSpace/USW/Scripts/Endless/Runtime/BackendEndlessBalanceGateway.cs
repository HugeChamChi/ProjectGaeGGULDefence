using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using GGD.BackendCharts;

/// <summary>Checks the applied BACKND chart and uses the SDK's persistent, versioned cache.</summary>
public sealed class BackendEndlessBalanceGateway : IEndlessBalanceGateway
{
    /// <inheritdoc />
    public bool IsLoggedIn => Backend.IsLogin;
    /// <inheritdoc />
    public string Account => Backend.UID;

    /// <inheritdoc />
    public UniTask<JsonData> LoadAsync(CancellationToken token)
        => BackendChartReader.ReadAsync(EndlessBalanceService.ChartName, token);
}
