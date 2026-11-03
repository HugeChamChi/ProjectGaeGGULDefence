using System;
using System.Threading;
using BackEnd;
using BackEnd.Content;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

namespace GGD.BackendCharts
{
    // SDK owns persistent chart caching. Feature loaders own timeout, lifetime and configuration policy.
    public static class BackendChartReader
    {
        private static readonly SemaphoreSlim CacheGate = new SemaphoreSlim(1, 1);

        public static UniTask<JsonData> ReadAsync(string chartName, CancellationToken token)
            => ReadCoreAsync(chartName, token).AttachExternalCancellation(token);

        private static async UniTask<JsonData> ReadCoreAsync(string chartName, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!Backend.IsLogin) throw new InvalidOperationException("Chart requires login: " + chartName);
            await CacheGate.WaitAsync(token);
            try
            {
                token.ThrowIfCancellationRequested();
                var listing = new UniTaskCompletionSource<BackendContentTableReturnObject>();
                Backend.CDN.Content.Table.Get(response => listing.TrySetResult(response));
                var list = await listing.Task;
                token.ThrowIfCancellationRequested();
                if (!list.IsSuccess()) throw new InvalidOperationException("Chart list: " + list.GetStatusCode());
                var tables = list.GetContentTableItemList();
                ContentTableItem selected = null;
                foreach (var row in tables)
                {
                    if (row.chartName != chartName) continue;
                    if (selected != null) throw new InvalidOperationException("Duplicate chart name: " + chartName);
                    selected = row;
                }
                if (selected == null || string.IsNullOrEmpty(selected.selectedChartFileId) || selected.selectedChartFileId == "0")
                    throw new InvalidOperationException("Missing applied chart: " + chartName);

                // Pass the complete server list: Update also removes charts no longer applied.
                // Keep the gate until the SDK callback, even if the caller times out.
                var contents = new UniTaskCompletionSource<BackendContentReturnObject>();
                int downloadCount = 0;
                Backend.CDN.Content.Local.Update(tables,
                    (totalCount, remainCount, fileName) => downloadCount = Math.Max(downloadCount, totalCount),
                    response => contents.TrySetResult(response));
                var result = await contents.Task;
                token.ThrowIfCancellationRequested();
                if (!result.IsSuccess()) throw new InvalidOperationException("Chart update: " + result.GetStatusCode());
                if (!result.GetContentDictionarySortByChartId().TryGetValue(selected.chartId, out var content)
                    || content.selectedChartFileId != selected.selectedChartFileId
                    || !string.IsNullOrEmpty(content.errorString))
                    throw new InvalidOperationException("Chart contents unavailable: " + chartName);
                Debug.Log(downloadCount == 0
                    ? $"[BackendChart] 변경사항이 없어 기존 데이터를 적용합니다. (요청 차트: {chartName})"
                    : $"[BackendChart] 신규 또는 변경된 차트 {downloadCount}개를 다운로드했습니다. 데이터를 적용합니다. (요청 차트: {chartName})");
                return content.contentJson;
            }
            finally { CacheGate.Release(); }
        }
    }
}
