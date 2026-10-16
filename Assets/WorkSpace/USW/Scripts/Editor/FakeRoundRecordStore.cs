using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>Deterministic storage fault and race fixture, never calls the backend.</summary>
public sealed class FakeRoundRecordStore : IRoundRecordStore
{
    private readonly Dictionary<string, int> _values = new Dictionary<string, int>();
    public bool Fail;
    public Action BeforeMerge;
    public UniTaskCompletionSource<Dictionary<string, int>> Hold;
    public int Get(string account, string mode) => _values.TryGetValue(account + mode, out int value) ? value : 0;
    public UniTask<Dictionary<string, int>> LoadAsync(string account, CancellationToken token)
    {
        if (Fail) throw new InvalidOperationException("QA simulated transport failure");
        if (Hold != null) return Hold.Task; // Deliberately ignores cancellation to exercise stale-response guards.
        return UniTask.FromResult(new Dictionary<string, int> { { "NORMAL", Get(account, "NORMAL") }, { "HARD", Get(account, "HARD") } });
    }
    public UniTask<int> MergeAsync(string account, string mode, int round, CancellationToken token)
    {
        if (Fail) throw new InvalidOperationException("QA simulated transport failure");
        var action = BeforeMerge; BeforeMerge = null; action?.Invoke();
        int maximum = Math.Max(Get(account, mode), round);
        _values[account + mode] = maximum;
        return UniTask.FromResult(maximum);
    }
}
