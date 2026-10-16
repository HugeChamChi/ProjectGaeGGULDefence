using System;
using System.Collections.Generic;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;

/// <summary>Updates only round columns on the existing PlayerData row; ordinary saves never write these columns.</summary>
public sealed class RoundRecordBackendStore : IRoundRecordStore
{
    private const string TableName = "PlayerData";
    private const int ConflictAttempts = 3;
    private static string Column(string mode) => "BestRound_" + mode;

    /// <inheritdoc />
    public async UniTask<Dictionary<string, int>> LoadAsync(string account, CancellationToken token)
    {
        var row = await ReadAsync(account, token);
        return new Dictionary<string, int>
        {
            { "NORMAL", Math.Max(0, row.GetInt(Column("NORMAL"), 0)) },
            { "HARD", Math.Max(0, row.GetInt(Column("HARD"), 0)) }
        };
    }

    /// <inheritdoc />
    public async UniTask<int> MergeAsync(string account, string mode, int round, CancellationToken token)
    {
        if (mode != "NORMAL" && mode != "HARD") throw new ArgumentException("Unsupported record mode.");
        string column = Column(mode);
        for (int attempt = 0; attempt < ConflictAttempts; attempt++)
        {
            var row = await ReadAsync(account, token);
            int current = Math.Max(0, row.GetInt(column, 0));
            if (current >= round) return current;
            var where = new Where();
            // The SDK rejects primary-key attributes in Where. PlayerData owns one row per account;
            // ReadAsync rejects ambiguous duplicate rows, and Update only searches the authenticated owner.
            bool hasColumn = row.ContainsKey(column) && row[column] != null;
            if (hasColumn) where.Less(column, round);
            var param = new Param();
            param.Add(column, round);
            EnsureOwner(account, token);
            var completion = new UniTaskCompletionSource<BackendReturnObject>();
            // IsNull does not match absent columns on the live backend. First use adds the column
            // on the known row; subsequent writes use a lower-value condition.
            if (hasColumn) Backend.GameData.Update(TableName, where, param, result => completion.TrySetResult(result));
            else Backend.GameData.UpdateV2(TableName, row["inDate"].ToString(), account, param, result => completion.TrySetResult(result));
            var response = await completion.Task.AttachExternalCancellation(token);
            EnsureOwner(account, token);
            if (response.IsSuccess())
            {
                var confirmed = await ReadAsync(account, token);
                int maximum = Math.Max(0, confirmed.GetInt(column, 0));
                if (maximum >= round) return maximum;
                continue;
            }
            // A different device may have raised the record since our read. Re-read before retrying.
            if (response.GetStatusCode() != "404") throw new InvalidOperationException("Round record update: " + response.GetStatusCode());
        }
        throw new InvalidOperationException("Round record conflict; retained for retry.");
    }

    private static async UniTask<JsonData> ReadAsync(string account, CancellationToken token)
    {
        EnsureOwner(account, token);
        var completion = new UniTaskCompletionSource<BackendReturnObject>();
        Backend.GameData.GetMyData(TableName, new Where(), result => completion.TrySetResult(result));
        var response = await completion.Task.AttachExternalCancellation(token);
        EnsureOwner(account, token);
        if (!response.IsSuccess()) throw new InvalidOperationException("Round record load: " + response.GetStatusCode());
        var rows = response.FlattenRows();
        if (rows.Count == 0) throw new InvalidOperationException("PlayerData must be initialized before round records.");
        if (rows.Count != 1) throw new InvalidOperationException("Duplicate PlayerData rows; round records require repair.");
        return rows[0];
    }

    private static void EnsureOwner(string account, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(account) || Backend.UserInDate != account)
            throw new OperationCanceledException("Round record account changed.", token);
    }
}
