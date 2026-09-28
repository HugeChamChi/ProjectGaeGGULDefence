using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>한 계정/트리의 로드와 직렬 저장 큐. 앱 수명으로 유지되어 화면을 닫아도 dirty를 잃지 않는다.</summary>
public sealed class ResearchSaveSession : IDisposable
{
    private readonly IResearchSaveStore _store;
    private readonly ResearchTreeData _tree;
    private readonly CancellationTokenSource _lifetime = new();
    private UniTaskCompletionSource _load;
    private UniTaskCompletionSource _save;
    private long _revision, _savedRevision;
    private bool _disposed;
    /// <summary>불변 저장 소유자.</summary>
    public ResearchSaveAddress Address { get; }
    /// <summary>로드 성공 후에만 제공하는 메모리 진행.</summary>
    public ResearchProgress Progress { get; private set; }
    /// <summary>아직 저장하지 못한 변경이 있는지.</summary>
    public bool IsDirty => _revision != _savedRevision;
    /// <summary>마지막 저장 오류. 재시도로 해소한다.</summary>
    public Exception SaveError { get; private set; }
    /// <summary>저장 상태가 바뀌면 알린다. 구독자는 자신의 계정 세대를 확인해야 한다.</summary>
    public event Action OnSaveStateChanged;
    /// <summary>주입된 저장소와 고정 소유자로 세션을 구성한다.</summary>
    public ResearchSaveSession(IResearchSaveStore store, ResearchSaveAddress address, ResearchTreeData tree)
    { _store = store; Address = address; _tree = tree; }

    /// <summary>동시 로드는 하나로 합친다. 화면 취소는 앱 소유 로드를 중단하지 않는다.</summary>
    public async UniTask LoadAsync(CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ResearchSaveSession));
        cancellationToken.ThrowIfCancellationRequested();
        if (Progress != null) return;
        var operation = _load;
        if (operation == null)
        {
            operation = _load = new UniTaskCompletionSource();
            LoadCore(operation).Forget();
        }
        await operation.Task.AttachExternalCancellation(cancellationToken);
    }

    private async UniTask LoadCore(UniTaskCompletionSource completion)
    {
        var token = _lifetime.Token;
        try
        {
            var data = await _store.LoadAsync(Address, token);
            token.ThrowIfCancellationRequested();
            Progress = new ResearchProgress(_tree, data.GetLevels(Address.TreeKey));
            Progress.OnChanged += Changed;
            completion.TrySetResult();
        }
        catch (Exception error) { completion.TrySetException(error); }
        finally { _load = null; }
    }

    private void Changed(ResearchNodeData node)
    {
        if (_disposed) return;
        _revision++;
        FlushAsync(_lifetime.Token).Forget();
    }

    /// <summary>진행 중 저장은 기다리고 후속 변경은 최신 스냅샷 하나로 합친다. 실패 시 dirty를 유지한다.</summary>
    public async UniTask FlushAsync(CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ResearchSaveSession));
        cancellationToken.ThrowIfCancellationRequested();
        var operation = _save;
        if (operation == null && Progress != null && IsDirty)
        {
            operation = _save = new UniTaskCompletionSource();
            SaveCore(operation).Forget();
        }
        if (operation != null) await operation.Task.AttachExternalCancellation(cancellationToken);
    }

    private async UniTask SaveCore(UniTaskCompletionSource completion)
    {
        var token = _lifetime.Token;
        SaveError = null;
        try
        {
            while (IsDirty)
            {
                token.ThrowIfCancellationRequested();
                long revision = _revision;
                var snapshot = Progress.Capture();
                await _store.SaveAsync(Address, snapshot, token);
                token.ThrowIfCancellationRequested();
                _savedRevision = revision;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            SaveError = error;
            Debug.LogWarning("[Research] 강화 저장 실패. 진행을 유지하며 재시도할 수 있습니다: " + error.Message);
        }
        finally
        {
            _save = null;
            completion.TrySetResult();
            if (!_disposed) OnSaveStateChanged?.Invoke();
        }
    }

    /// <summary>앱 서비스가 끝나면 비동기 수명과 구독을 정리한다.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Progress != null) Progress.OnChanged -= Changed;
        OnSaveStateChanged = null;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
