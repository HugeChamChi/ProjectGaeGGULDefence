using System;
using System.IO;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

/// <summary>One authenticated private row per table. Failed reads never authorize inserts.</summary>
public class AccountJsonStore
{
    public sealed class Row { public string InDate; public string Json; }
    public virtual async UniTask<Row> ReadAsync(string table, string owner, CancellationToken token)
    {
        await UniTask.SwitchToMainThread(token);
        RequireOwner(owner, token);
        var done = new UniTaskCompletionSource<BackendReturnObject>();
        Backend.GameData.GetMyData(table, new Where(), result => done.TrySetResult(result));
        var response = await done.Task.AttachExternalCancellation(token);
        RequireOwner(owner, token);
        if (!response.IsSuccess()) throw new IOException(table + " read: " + response.GetStatusCode());
        var rows = response.FlattenRows();
        if (rows.Count > 1) throw new InvalidDataException(table + " contains duplicate account rows.");
        if (rows.Count == 0) return new Row();
        return new Row { InDate = rows[0]["inDate"].ToString(), Json = rows[0].GetString("Data", string.Empty) };
    }
    public virtual async UniTask<string> WriteAsync(string table, string owner, string inDate, string json, CancellationToken token)
    {
        await UniTask.SwitchToMainThread(token);
        RequireOwner(owner, token);
        var param = new Param(); param.Add("Data", json);
        var done = new UniTaskCompletionSource<BackendReturnObject>();
        if (string.IsNullOrEmpty(inDate)) Backend.GameData.Insert(table, param, result => done.TrySetResult(result));
        else Backend.GameData.UpdateV2(table, inDate, owner, param, result => done.TrySetResult(result));
        var response = await done.Task.AttachExternalCancellation(token);
        RequireOwner(owner, token);
        if (!response.IsSuccess()) throw new IOException(table + " write: " + response.GetStatusCode());
        return string.IsNullOrEmpty(inDate) ? response.GetInDate() : inDate;
    }
    public static void RequireOwner(string owner, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(owner) || !Backend.IsLogin || owner != Backend.UserInDate)
            throw new InvalidOperationException("Account session changed.");
    }
    public static T Parse<T>(string json)
    {
        var root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (root["SchemaVersion"]?.Type != JTokenType.Integer || (int)root["SchemaVersion"] != 1)
            throw new InvalidDataException("Unsupported account data version.");
        if (root["SaveId"]?.Type != JTokenType.String) throw new InvalidDataException("Missing save identifier.");
        if (typeof(T) == typeof(AccountCharacterData) && root["Characters"] is not JArray ||
            typeof(T) == typeof(AccountLobbyData) && (root["CompletedTutorials"] is not JArray || root["ReadMail"] is not JArray) ||
            typeof(T) == typeof(AccountResearchData) && root["Trees"] is not JArray)
            throw new InvalidDataException("Missing account entries.");
        if (typeof(T) == typeof(AccountResearchData))
            foreach (var tree in (JArray)root["Trees"])
                if (tree["Entries"] is not JArray || tree["SchemaVersion"]?.Type != JTokenType.Integer)
                    throw new InvalidDataException("Missing research entries.");
        return root.ToObject<T>();
    }
}
