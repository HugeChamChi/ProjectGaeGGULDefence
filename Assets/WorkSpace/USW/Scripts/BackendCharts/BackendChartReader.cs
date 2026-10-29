using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;

namespace GGD.BackendCharts
{
    // SDK transport only. Feature loaders own timeout, lifetime and configuration policy.
    public static class BackendChartReader
    {
        public static async UniTask<JsonData> ReadAsync(string chartName, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!Backend.IsLogin) throw new InvalidOperationException("Chart requires login: " + chartName);
            var listing = new UniTaskCompletionSource<BackendReturnObject>();
            Backend.Chart.GetChartListV2(response => listing.TrySetResult(response));
            var list = await listing.Task.AttachExternalCancellation(token);
            if (!list.IsSuccess()) throw new InvalidOperationException("Chart list: " + list.GetStatusCode());
            string file = null;
            foreach (JsonData row in list.FlattenRows())
            {
                if (row["chartName"].ToString() != chartName) continue;
                if (file != null) throw new InvalidOperationException("Duplicate chart name: " + chartName);
                file = row["selectedChartFileId"].ToString();
            }
            if (string.IsNullOrEmpty(file) || file == "0") throw new InvalidOperationException("Missing applied chart: " + chartName);
            token.ThrowIfCancellationRequested();
            var contents = new UniTaskCompletionSource<BackendReturnObject>();
            Backend.Chart.GetChartContents(file, response => contents.TrySetResult(response));
            var result = await contents.Task.AttachExternalCancellation(token);
            if (!result.IsSuccess()) throw new InvalidOperationException("Chart contents: " + result.GetStatusCode());
            return result.FlattenRows();
        }
    }
}
