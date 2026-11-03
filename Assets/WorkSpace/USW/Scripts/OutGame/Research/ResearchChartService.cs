using System;
using System.Collections.Generic;
using System.Threading;
using GGD.BackendCharts;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>App-owned definitions and icon handles. One snapshot is shared by UI and saved progression until restart.</summary>
public sealed class ResearchChartService : IDisposable
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public const string ChartName = "DEV_ResearchBalance";
#else
    public const string ChartName = "LIVE_ResearchBalance";
#endif
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<AsyncOperationHandle<Sprite>> _icons = new();
    private Dictionary<string, ResearchTreeData> _trees;
    private UniTaskCompletionSource _loading;
    private bool _disposed;

    public async UniTask<ResearchTreeData> ResolveAsync(ResearchTreeData original, CancellationToken token)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ResearchChartService));
        if (original == null) throw new ArgumentNullException(nameof(original));
        await PrepareAsync(token);
        if (!_trees.TryGetValue(original.TreeKey, out var tree)) throw new InvalidOperationException("Missing research tree: " + original.TreeKey);
        if (tree.LegacySaveName != original.LegacySaveName) throw new InvalidOperationException("Legacy research save key changed");
        return tree;
    }

    public async UniTask PrepareAsync(CancellationToken token)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ResearchChartService));
        token.ThrowIfCancellationRequested();
        if (_trees == null)
        {
            var operation = _loading;
            if (operation == null) { operation = _loading = new UniTaskCompletionSource(); Load(operation).Forget(); }
            await operation.Task.AttachExternalCancellation(token);
        }
    }

    private async UniTaskVoid Load(UniTaskCompletionSource completion)
    {
        Dictionary<string, ResearchTreeData> pending = null;
        var handles = new List<AsyncOperationHandle<Sprite>>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(30), DelayType.Realtime);
        try
        {
            var config = Resources.Load<ResearchChartConfig>("ResearchChartConfig");
            if (config == null) throw new InvalidOperationException("ResearchChartConfig missing");
            JsonData rows;
            if (config.UseRemote) rows = await BackendChartReader.ReadAsync(ChartName, timeout.Token);
            else
            {
                if (config.BundledChart == null) throw new InvalidOperationException("Bundled research chart missing");
                rows = JsonMapper.ToObject(config.BundledChart.text);
            }
            pending = ResearchChartParser.Parse(rows);
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var tree in pending.Values)
                foreach (var node in tree.Nodes)
                {
                    if (!sprites.TryGetValue(node.IconAddress, out var sprite))
                    {
                        var handle = Addressables.LoadAssetAsync<Sprite>(node.IconAddress);
                        handles.Add(handle);
                        sprite = await handle.ToUniTask(cancellationToken: timeout.Token);
                        if (sprite == null) throw new InvalidOperationException("Missing research icon: " + node.IconAddress);
                        sprites.Add(node.IconAddress, sprite);
                    }
                    node.Icon = sprite;
                }
            timeout.Token.ThrowIfCancellationRequested();
            _icons.AddRange(handles); handles.Clear();
            _trees = pending; pending = null;
            completion.TrySetResult();
        }
        catch (Exception error) { completion.TrySetException(error); }
        finally
        {
            if (pending != null) ResearchChartParser.Destroy(pending);
            foreach (var handle in handles) if (handle.IsValid()) Addressables.Release(handle);
            _loading = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _lifetime.Cancel(); _lifetime.Dispose();
        if (_trees != null) ResearchChartParser.Destroy(_trees);
        foreach (var handle in _icons) if (handle.IsValid()) Addressables.Release(handle);
        _icons.Clear();
    }
}
