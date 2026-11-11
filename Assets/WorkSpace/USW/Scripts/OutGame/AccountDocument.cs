using System;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public class AccountDocumentData { public int SchemaVersion = 1; public string SaveId = ""; }

/// <summary>Account-scoped JSON snapshot with durable retries and stale callback protection.</summary>
public sealed class AccountDocument<T> : Global.IClearable where T : AccountDocumentData, new()
{
    [Serializable] private sealed class Journal { public string BaseSaveId; public string AttemptSaveId; public T Data; }
    private readonly string _table;
    private readonly AccountJsonStore _store;
    private readonly Action<T> _validate;
    private string _owner, _confirmed = "", _attemptSaveId = "";
    private readonly Func<string> _ownerProvider;
    private readonly Action _requestSave;
    private int _generation;
    public int Generation => _generation;
    private UniTaskCompletionSource _save;
    public T Data { get; private set; }
    public bool IsDirty { get; set; }
    public bool IsLocked { get; private set; }
    public bool IsReady => !IsLocked && Data != null && _owner == _ownerProvider();
    public AccountDocument(string table, Action<T> validate, AccountJsonStore store = null, Func<string> ownerProvider = null, Action requestSave = null)
    { _table = table; _validate = validate; _store = store ?? new AccountJsonStore(); _ownerProvider = ownerProvider ?? (() => Backend.UserInDate); _requestSave = requestSave ?? Player.RequestDebouncedSave; }
    private string Key => "Account.Pending.v1." + _table + "." + _owner;
    public async UniTask InitializeAsync()
    {
        Clear();
        int generation = _generation;
        string owner = _owner = _ownerProvider();
        using var timeout = new CancellationTokenSource();
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(20), DelayType.Realtime);
        var row = await _store.ReadAsync(_table, owner, timeout.Token);
        if (generation != _generation) throw new OperationCanceledException();
        var data = string.IsNullOrEmpty(row.InDate) ? new T() : AccountJsonStore.Parse<T>(row.Json);
        _validate(data);
        _confirmed = data.SaveId ?? "";
        if (SignedPrefs.HasKey(Key))
        {
            var pending = JsonConvert.DeserializeObject<Journal>(SignedPrefs.GetString(Key));
            if (pending?.Data == null) throw new InvalidOperationException("Invalid pending account data.");
            _validate(pending.Data);
            if (data.SaveId == pending.Data.SaveId) { SignedPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
            else if (_confirmed == pending.BaseSaveId || _confirmed == pending.AttemptSaveId) { data = pending.Data; IsDirty = true; }
            else throw new InvalidOperationException("Account save conflicts with pending local changes: " + _table);
        }
        Data = data;
        if (IsDirty) await SaveAsync();
    }
    public void Changed()
    {
        if (!IsReady) throw new InvalidOperationException("Account data is not ready: " + _table);
        _validate(Data); Data.SaveId = Guid.NewGuid().ToString("N"); IsDirty = true; Persist();
        _requestSave();
    }
    private void Persist()
    { SignedPrefs.SetString(Key, JsonConvert.SerializeObject(new Journal { BaseSaveId = _confirmed, AttemptSaveId = _attemptSaveId, Data = Data })); PlayerPrefs.Save(); }
    public async UniTask SaveAsync()
    {
        if (_save != null) { await _save.Task; return; }
        if (!IsReady || !IsDirty) return;
        var done = _save = new UniTaskCompletionSource();
        int generation = _generation;
        string owner = _owner;
        try
        {
            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(20), DelayType.Realtime);
            while (IsDirty)
            {
                string json = JsonConvert.SerializeObject(Data), saveId = Data.SaveId;
                // Re-read before retry: an insert/update may have committed despite a lost response.
                var row = await _store.ReadAsync(_table, owner, timeout.Token);
                if (generation != _generation) return;
                string remoteId = "";
                if (!string.IsNullOrEmpty(row.InDate))
                { var remote = AccountJsonStore.Parse<T>(row.Json); _validate(remote); remoteId = remote.SaveId ?? ""; }
                if (!string.IsNullOrEmpty(_attemptSaveId) && remoteId == _attemptSaveId)
                { _confirmed = remoteId; _attemptSaveId = ""; Persist(); }
                if (remoteId != saveId && remoteId != _confirmed)
                    throw new InvalidOperationException("Account save conflict: " + _table);
                string inDate = row.InDate;
                if (remoteId != saveId)
                {
                    _attemptSaveId = saveId; Persist();
                    inDate = await _store.WriteAsync(_table, owner, inDate, json, timeout.Token);
                }
                if (generation != _generation) return;
                _confirmed = saveId; _attemptSaveId = "";
                if (Data.SaveId == saveId) { IsDirty = false; SignedPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
                else Persist();
            }
        }
        catch (Exception error) { if (generation == _generation) Debug.LogWarning("[AccountSave] " + _table + ": " + error.Message); }
        finally { done.TrySetResult(); if (ReferenceEquals(_save, done)) _save = null; }
    }
    public async UniTask<AccountJsonStore.Row> ReadCleanRowAsync(CancellationToken token)
    {
        if (!IsReady) throw new InvalidOperationException("Account document is not ready.");
        await SaveAsync();
        if (IsDirty) throw new InvalidOperationException("Account document save is pending.");
        var row = await _store.ReadAsync(_table, _owner, token);
        var remote = AccountJsonStore.Parse<T>(row.Json); _validate(remote);
        if (!IsReady || remote.SaveId != Data.SaveId) throw new InvalidOperationException("Account document changed.");
        return row;
    }
    public void Lock() { if (!IsReady || IsDirty) throw new InvalidOperationException("Account document is busy."); IsLocked = true; }
    public void Unlock() => IsLocked = false;
    public void ApplyCommitted(T value)
    {
        if (!IsLocked || _owner != _ownerProvider()) throw new InvalidOperationException("Account document changed.");
        _validate(value); Data = value; _confirmed = value.SaveId; _attemptSaveId = "";
        IsDirty = false; SignedPrefs.DeleteKey(Key); PlayerPrefs.Save();
    }
    public void Clear() { _generation++; IsLocked = false; Data = null; _owner = null; _confirmed = ""; _attemptSaveId = ""; IsDirty = false; _save = null; }
}
