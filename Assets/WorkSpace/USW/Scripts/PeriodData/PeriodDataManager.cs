using System;
using System.Collections.Generic;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GGD.Periods
{
    /// <summary>One account row per period; local pending snapshots survive failed writes/restarts.</summary>
    public sealed class PeriodDataManager<T> : Global.IClearable where T : class, new()
    {
        [Serializable] private sealed class Journal
        {
            public string Base, Period, Payload;
            public List<string> Attempts = new();
        }
        private readonly string _table;
        private readonly bool _weekly;
        private T _data;
        private string _period, _owner, _inDate, _savedPayload, _savedPeriod, _persisted;
        private string _serverPayload;
        private bool _hasSnapshot;
        private int _generation;
        private bool _forcedDirty, _conflict, _needsReconcile;
        private readonly List<string> _attempts = new();
        private UniTaskCompletionSource _save;
        private string Key => "Period.Pending.v1." + _table + "." + _owner;
        public PeriodDataManager(string table, bool weekly) { _table = table; _weekly = weekly; }
        // For durable mutations use Mutate, or set IsDirty=true immediately after editing Data.
        public T Data { get { EnsurePeriod(); return _data; } }
        public bool IsDirty
        {
            get
            {
                if (_data == null) return false;
                EnsurePeriod();
                bool dirty = _forcedDirty || _period != _savedPeriod || PeriodSerialization.Write(_data) != _savedPayload;
                if (dirty && !_conflict) Persist();
                return dirty || _conflict;
            }
            set { _forcedDirty = value; if (value && _data != null) { EnsurePeriod(); Persist(); } }
        }
        public void Mutate(Action<T> change)
        {
            EnsurePeriod();
            var next = PeriodSerialization.Read<T>(PeriodSerialization.Write(_data));
            change(next);
            _data = next; Persist();
            Player.RequestDebouncedSave();
        }
        public async UniTask InitializeAsync()
        {
            Clear();
            if (!ServerClock.IsSynced) throw new InvalidOperationException("Period data requires synchronized server time.");
            var owner = Backend.UserInDate;
            if (string.IsNullOrEmpty(owner)) throw new InvalidOperationException("Login required.");
            int generation = _generation;
            var completion = new UniTaskCompletionSource<BackendReturnObject>();
            Backend.GameData.GetMyData(_table, new Where(), r => completion.TrySetResult(r));
            var response = await WaitAsync(completion);
            RequireSession(owner, generation);
            if (!response.IsSuccess()) throw new InvalidOperationException(_table + " load: " + response.GetStatusCode());
            var rows = response.FlattenRows();
            if (rows.Count > 1) throw new InvalidOperationException(_table + " has duplicate rows.");
            _owner = owner;
            if (rows.Count == 0)
            {
                _data = new T(); _period = CurrentKey();
                _savedPeriod = _savedPayload = null;
            }
            else
            {
                var row = rows[0];
                if (row.GetInt("SchemaVersion", 1) != 1) throw new InvalidOperationException("Unsupported period schema.");
                _data = PeriodSerialization.Read<T>(row.GetString("PayloadJson", "{}"));
                _inDate = row["inDate"].ToString();
                _period = _savedPeriod = row.GetString("PeriodKey", string.Empty);
                _savedPayload = PeriodSerialization.Write(_data);
                _serverPayload = row.GetString("PayloadJson", "{}");
                _hasSnapshot = row.ContainsKey("PayloadJson") && row.ContainsKey("PeriodKey");
            }
            RestorePending();
            EnsurePeriod();
            // Missing fields use model defaults without an unconditional write on every login.
            if (IsDirty) await SaveAsync();
        }
        private void RestorePending()
        {
            if (SignedPrefs.HasKey(Key))
            {
                // Never overwrite an unreadable/conflicting journal with freshly loaded defaults.
                try
                {
                    var pending = JsonUtility.FromJson<Journal>(SignedPrefs.GetString(Key));
                    if (pending == null || string.IsNullOrWhiteSpace(pending.Payload) || string.IsNullOrEmpty(pending.Period) || pending.Base == null) throw new FormatException("Invalid period journal.");
                    string remote = Signature(_savedPeriod, _savedPayload);
                    if (Signature(pending.Period, pending.Payload) == remote) DeleteJournal();
                    else if (pending.Base == remote || pending.Attempts?.Contains(remote) == true)
                    {
                        _data = PeriodSerialization.Read<T>(pending.Payload); _period = pending.Period;
                        if (pending.Attempts != null) _attempts.AddRange(pending.Attempts);
                    }
                    else throw new InvalidOperationException(_table + " local/server conflict; journal retained.");
                }
                catch { _conflict = true; throw; }
            }
        }
        public async UniTask SaveAsync()
        {
            if (_save == null)
            {
                RequireSession(_owner, _generation);
                if (!IsDirty) return;
                if (_conflict) throw new InvalidOperationException(_table + " conflict; save blocked.");
                var operation = _save = new UniTaskCompletionSource();
                SaveCoreAsync(operation, _owner, _generation).Forget();
                await WaitAsync(operation); return;
            }
            await WaitAsync(_save);
        }
        private async UniTaskVoid SaveCoreAsync(UniTaskCompletionSource operation, string owner, int generation)
        {
            try
            {
                if (_needsReconcile)
                {
                    // An unacknowledged write may have committed. Reconcile before retrying any snapshot.
                    var read = new UniTaskCompletionSource<BackendReturnObject>();
                    Backend.GameData.GetMyData(_table, new Where(), r => read.TrySetResult(r));
                    var response = await read.Task;
                    RequireSession(owner, generation);
                    if (!response.IsSuccess()) throw new InvalidOperationException(_table + " write reconciliation failed.");
                    var rows = response.FlattenRows();
                    if (rows.Count > 1) throw new InvalidOperationException(_table + " duplicate rows.");
                    if (rows.Count == 1)
                    {
                        var row = rows[0];
                        var signature = Signature(row.GetString("PeriodKey", ""), PeriodSerialization.Write(PeriodSerialization.Read<T>(row.GetString("PayloadJson", "{}"))));
                        if (!_attempts.Contains(signature) && signature != Signature(_savedPeriod, _savedPayload))
                        { _conflict = true; throw new InvalidOperationException(_table + " save conflict."); }
                        _inDate = row["inDate"].ToString();
                        _savedPeriod = row.GetString("PeriodKey", "");
                        _serverPayload = row.GetString("PayloadJson", "{}");
                        _savedPayload = PeriodSerialization.Write(PeriodSerialization.Read<T>(_serverPayload));
                        _hasSnapshot = row.ContainsKey("PayloadJson") && row.ContainsKey("PeriodKey");
                    }
                    else if (!string.IsNullOrEmpty(_inDate)) { _conflict = true; throw new InvalidOperationException(_table + " row disappeared."); }
                    _needsReconcile = false;
                    if (_period == _savedPeriod && PeriodSerialization.Write(_data) == _savedPayload)
                    { _forcedDirty = false; _attempts.Clear(); DeleteJournal(); operation.TrySetResult(); return; }
                }
                string payload = PeriodSerialization.Write(_data), period = _period;
                string attempt = Signature(period, payload);
                if (!_attempts.Contains(attempt)) _attempts.Add(attempt);
                Persist();
                var param = new Param();
                param.Add("SchemaVersion", 1); param.Add("PeriodKey", period); param.Add("PayloadJson", payload);
                var completion = new UniTaskCompletionSource<BackendReturnObject>();
                bool insert = string.IsNullOrEmpty(_inDate);
                _needsReconcile = true;
                if (insert) Backend.GameData.Insert(_table, param, r => completion.TrySetResult(r));
                else if (_hasSnapshot)
                {
                    var where = new Where(); where.Equal("PeriodKey", _savedPeriod); where.Equal("PayloadJson", _serverPayload);
                    Backend.GameData.Update(_table, where, param, r => completion.TrySetResult(r));
                }
                else Backend.GameData.UpdateV2(_table, _inDate, owner, param, r => completion.TrySetResult(r));
                // Keep ownership until the real SDK callback, regardless of a caller's timeout.
                var saved = await completion.Task;
                RequireSession(owner, generation);
                if (!saved.IsSuccess()) throw new InvalidOperationException(_table + " save: " + saved.GetStatusCode());
                if (insert) _inDate = saved.GetInDate();
                _needsReconcile = false;
                _savedPayload = payload; _savedPeriod = period; _forcedDirty = false; _attempts.Clear();
                _serverPayload = payload; _hasSnapshot = true;
                if (IsDirty) Persist(); else DeleteJournal();
                operation.TrySetResult();
            }
            catch (Exception error) { operation.TrySetException(error); }
            finally { if (ReferenceEquals(_save, operation)) _save = null; }
        }
        private static async UniTask<TResult> WaitAsync<TResult>(UniTaskCompletionSource<TResult> completion)
        {
            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(15), DelayType.Realtime);
            return await completion.Task.AttachExternalCancellation(timeout.Token);
        }
        private static async UniTask WaitAsync(UniTaskCompletionSource completion)
        {
            using var timeout = new CancellationTokenSource();
            using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(15), DelayType.Realtime);
            await completion.Task.AttachExternalCancellation(timeout.Token);
        }
        private static string Signature(string period, string payload) => (period ?? "") + "\n" + (payload ?? "");
        private void Persist()
        {
            if (_conflict) throw new InvalidOperationException(_table + " conflict; journal retained.");
            var json = JsonUtility.ToJson(new Journal { Base = Signature(_savedPeriod, _savedPayload), Period = _period, Payload = PeriodSerialization.Write(_data), Attempts = _attempts });
            if (json == _persisted) return;
            SignedPrefs.SetString(Key, json); PlayerPrefs.Save(); _persisted = json;
        }
        private void DeleteJournal() { SignedPrefs.DeleteKey(Key); PlayerPrefs.Save(); _persisted = null; }
        private string CurrentKey() => PeriodSerialization.Key(ServerClock.UtcNow, _weekly);
        private void EnsurePeriod()
        {
            if (_data == null || _owner != Backend.UserInDate || !ServerClock.IsSynced || _conflict)
                throw new InvalidOperationException("Period data is not initialized for this account.");
            string key = CurrentKey();
            if (key == _period) return;
            _data = new T(); _period = key; _forcedDirty = true; Persist();
        }
        private void RequireSession(string owner, int generation)
        {
            if (generation != _generation || string.IsNullOrEmpty(owner) || owner != Backend.UserInDate)
                throw new OperationCanceledException("Period data account changed.");
        }
        public void Clear()
        {
            _generation++; _data = null;
            _save?.TrySetCanceled(); _save = null;
            _owner = _inDate = _period = _savedPeriod = _savedPayload = _persisted = null;
            _serverPayload = null; _hasSnapshot = false;
            _forcedDirty = _conflict = _needsReconcile = false; _attempts.Clear();
        }
    }
}
