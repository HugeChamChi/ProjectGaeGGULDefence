using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>Account-owned cloud storage for independent endless mode records.</summary>
public interface IRoundRecordStore
{
    /// <summary>Loads confirmed records; throws on transport/authentication failure.</summary>
    UniTask<Dictionary<string, int>> LoadAsync(string account, CancellationToken token);
    /// <summary>Merges a reached round and returns the confirmed maximum.</summary>
    UniTask<int> MergeAsync(string account, string mode, int round, CancellationToken token);
}
