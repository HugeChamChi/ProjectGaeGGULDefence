using System;
using System.Collections.Generic;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;

/// <summary>Account mailbox, serialized claims and durable response recovery.</summary>
public class MailManager : Global.IClearable
{
    private readonly List<Post> _posts = new();
    private CancellationTokenSource _lifetime = new();
    private string _owner;
    private int _generation;
    private bool _busy;
    private MailRewardConfig _config;
    private readonly string PendingPrefix;
    private readonly MailGateway _gateway;
    private readonly HashSet<PostType> _failedTypes = new();
    private static readonly PostType[] MailTypes = { PostType.Admin, PostType.Rank, PostType.Coupon };
    /// <summary>Only failed list requests require a background reload.</summary>
    public bool NeedsReload => _failedTypes.Count > 0;
    /// <summary>Whether a captured successful response can be retried without submitting a claim again.</summary>
    public bool HasRecoverableClaim
    {
        get
        {
            if (string.IsNullOrEmpty(_owner) || _owner != _gateway.Owner) return false;
            try { return !string.IsNullOrEmpty(JsonUtility.FromJson<MailClaimJournal>(PlayerPrefs.GetString(PendingPrefix + _owner, "{}"))?.Response); }
            catch (Exception) { return false; }
        }
    }
    /// <summary>Uses the production gateway unless an isolated backend is supplied.</summary>
    public MailManager(MailGateway gateway = null, string journalPrefix = "Mail.Pending.v1.")
    { _gateway = gateway ?? new MailGateway(); PendingPrefix = journalPrefix; }
    /// <summary>Current operation status shown by the mailbox.</summary>
    public string Status { get; private set; } = "";
    /// <summary>Prevents concurrent list/claim/batch requests.</summary>
    public bool IsBusy => _busy;
    /// <summary>Current owner's unresolved claim identity; empty after confirmed balance saving.</summary>
    public string PendingReceiptId
    {
        get
        {
            if (string.IsNullOrEmpty(_owner) || _owner != _gateway.Owner) return "";
            string key = PendingPrefix + _owner;
            if (!PlayerPrefs.HasKey(key)) return "";
            try { return JsonUtility.FromJson<MailClaimJournal>(PlayerPrefs.GetString(key))?.Id ?? ""; }
            catch (Exception) { return ""; }
        }
    }
    /// <summary>Notifies views after state changes.</summary>
    public event Action OnChanged;
    /// <inheritdoc />
    public bool IsDirty { get; set; }
    /// <inheritdoc />
    public async UniTask SaveAsync() { await RecoverPendingAsync(); }
    /// <summary>Recovers captured server responses after player balances have loaded.</summary>
    public async UniTask InitalizeAsync() { EnsureOwner(); await RecoverPendingAsync(); }
    /// <summary>Cancels old waits. Captured responses remain associated with the original account.</summary>
    public void Clear()
    {
        _generation++; _lifetime?.Cancel(); _lifetime?.Dispose(); _lifetime = null;
        _owner = null; _posts.Clear(); _failedTypes.Clear(); _busy = false; IsDirty = false; Status = ""; OnChanged?.Invoke();
    }
    private bool EnsureOwner()
    {
        if (!_gateway.Balances.IsReady || string.IsNullOrEmpty(_gateway.Owner)) return false;
        if (_owner != _gateway.Owner)
        {
            Clear(); _owner = _gateway.Owner; _lifetime = new CancellationTokenSource();
            _config = _gateway.RewardConfig;
        }
        return true;
    }
    private bool Current(string owner, int generation) => owner == _gateway.Owner && owner == _owner && generation == _generation;
    private void Publish(string status) { Status = status; OnChanged?.Invoke(); }
    /// <summary>Legacy callback boundary used by mailbox openers.</summary>
    public void GetPostList(PostType type, Action onComplete = null) => LoadCallbackAsync(type, onComplete).Forget();
    private async UniTaskVoid LoadCallbackAsync(PostType type, Action done) { await LoadAsync(type); done?.Invoke(); }
    /// <summary>Updates one source while preserving other sources already in the unified mailbox.</summary>
    public UniTask LoadAsync(PostType type) => LoadTypesAsync(new[] { type });
    /// <summary>Loads all supported sources under one account/request lock.</summary>
    public UniTask LoadAllAsync() => LoadTypesAsync(MailTypes);
    private async UniTask LoadTypesAsync(PostType[] types)
    {
        if (_busy) return;
        if (!EnsureOwner()) { Publish("로그인과 재화 조회를 완료해 주세요."); return; }
        string owner = _owner; int generation = _generation;
        _busy = true; Publish("우편 불러오는 중…");
        try
        {
            bool recovered = await RecoverCoreAsync(owner, generation);
            if (!Current(owner, generation)) return;
            foreach (var type in types)
            {
                _failedTypes.Add(type);
                try
                {
                    var completion = new UniTaskCompletionSource<MailResponse>();
                    _gateway.GetList(type, result => completion.TrySetResult(result));
                    var response = await WaitAsync(completion, _lifetime.Token);
                    if (!Current(owner, generation)) return;
                    if (!response.IsSuccess()) continue;
                    var json = response.GetReturnValuetoJSON();
                    if (!json.ContainsKey("postList") || !json["postList"].IsArray) throw new FormatException();
                    var loaded = new List<Post>();
                    foreach (JsonData entry in json["postList"])
                    {
                        var post = MailParser.Parse(entry, type, _config);
                        post.IsRead = PlayerPrefs.GetInt(ReadKey(owner, post.Key), 0) != 0;
                        if (_gateway.Balances.Data.CouponHistory?.Exists(receipt => receipt.Id == post.Key) == true) continue;
                        if (!loaded.Exists(item => item.Key == post.Key)) loaded.Add(post);
                    }
                    _posts.RemoveAll(post => post.Type == type); _posts.AddRange(loaded); _failedTypes.Remove(type);
                }
                catch (Exception error)
                {
                    if (!Current(owner, generation)) return;
                    Debug.LogWarning("[Mail] List " + error.GetType().Name);
                }
            }
            if (!Current(owner, generation)) return;
            if (NeedsReload) Publish("일부 우편을 확인하지 못했어요. 잠시 후 자동으로 다시 확인합니다.");
            else if (recovered) Publish("");
        }
        catch (OperationCanceledException) { if (Current(owner, generation)) Publish("연결을 기다리고 있어요. 잠시 후 자동으로 다시 확인합니다."); }
        catch (Exception error) { if (Current(owner, generation)) { Debug.LogWarning("[Mail] " + error.GetType().Name); Publish("우편 정보를 불러오지 못했습니다."); } }
        finally { if (Current(owner, generation)) { _busy = false; OnChanged?.Invoke(); } }
    }
    /// <summary>Records a detail view as read, scoped to the account on this device.</summary>
    public void MarkRead(Post post)
    {
        if (!EnsureOwner() || post == null || !_posts.Contains(post)) return;
        post.IsRead = true; PlayerPrefs.SetInt(ReadKey(_owner, post.Key), 1); PlayerPrefs.Save(); OnChanged?.Invoke();
    }
    private static string ReadKey(string owner, string id) => "Mail.Read.v1." + owner + "." + id;
    /// <summary>Compatibility index boundary with negative-index/type validation.</summary>
    public void ReceivePost(PostType type, int index)
    { var posts = GetCurrentPostList(); if (index >= 0 && index < posts.Count && posts[index].Type == type) ClaimAsync(posts[index]).Forget(); }
    /// <summary>Claims one stable identity and waits for reward persistence.</summary>
    public async UniTask<bool> ClaimAsync(Post post)
    {
        if (_busy || !EnsureOwner() || post == null || !_posts.Contains(post)) return false;
        string owner = _owner; int generation = _generation;
        _busy = true; OnChanged?.Invoke();
        try { return await ClaimCoreAsync(post, owner, generation); }
        catch (Exception error) { if (Current(owner, generation)) { Debug.LogWarning("[Mail] " + error.GetType().Name); Publish("수령 결과를 확인하고 있어요. 확인된 보상은 자동으로 저장합니다."); } return false; }
        finally { if (Current(owner, generation)) { _busy = false; OnChanged?.Invoke(); } }
    }
    private async UniTask<bool> ClaimCoreAsync(Post post, string owner, int generation)
    {
        if (!await RecoverCoreAsync(owner, generation) || !Current(owner, generation)) return false;
        if (_gateway.Balances.Data.CouponHistory?.Exists(receipt => receipt.Id == post.Key) == true)
        { _posts.RemoveAll(item => item.Key == post.Key); Publish("이미 수령한 우편입니다."); return true; }
        if (!post.isCanReceive || post.IsExpired) { Publish(post.IsExpired ? "기간이 만료된 우편입니다." : post.ValidationError); return false; }
        if (!await _gateway.Balances.TrySaveAsync() || !Current(owner, generation)) { if (Current(owner, generation)) Publish("기존 재화 저장을 완료한 뒤 다시 시도해 주세요."); return false; }
        var data = _gateway.Balances.Data;
        if (post.Reward == null || (long)data.Gold + post.Reward.Gold > int.MaxValue || (long)data.Diamond + post.Reward.Diamond > int.MaxValue || (long)data.Stamina + post.Reward.Stamina > int.MaxValue)
        { Publish("재화 보유 한도를 확인해 주세요."); return false; }
        string key = PendingPrefix + owner;
        var journal = new MailClaimJournal { Id = post.Key, Title = post.title };
        Persist(key, journal); IsDirty = true; Publish("보상 수령 중…");
        var completion = new UniTaskCompletionSource<MailResponse>();
        _gateway.Claim(post.Type, post.inDate, response =>
        {
            // Capture late success even after a UI timeout/account change, under the original owner.
            if (response.IsSuccess()) { journal.Response = response.GetReturnValuetoJSON().ToJson(); Persist(key, journal); }
            // A rejection proves this request did not grant. Ambiguous network/server errors retain evidence.
            else if (response.StatusCode >= 400 && response.StatusCode < 500 && response.StatusCode != 408 && response.StatusCode != 429)
            { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
            completion.TrySetResult(response);
        });
        var result = await WaitAsync(completion, _lifetime.Token);
        if (!Current(owner, generation)) return false;
        if (!result.IsSuccess()) { _failedTypes.Add(post.Type); Publish("수령을 완료하지 못했어요. 우편 상태를 다시 확인합니다."); return false; }
        return await RecoverCoreAsync(owner, generation);
    }
    /// <summary>Sequential claims stop on the first failure, preserving individual confirmations.</summary>
    public void ReceiveAllPost(PostType type) => ClaimAllAsync(type).Forget();
    /// <summary>Claims the currently loaded eligible mails of one type, never an unseen mailbox.</summary>
    public UniTask ClaimAllAsync(PostType type) => ClaimBatchAsync(type);
    /// <summary>Claims eligible currently loaded mail from every supported source.</summary>
    public UniTask ClaimAllAsync() => ClaimBatchAsync(null);
    private async UniTask ClaimBatchAsync(PostType? type)
    {
        if (_busy || !EnsureOwner()) return;
        string owner = _owner; int generation = _generation; int count = 0;
        _busy = true; OnChanged?.Invoke();
        try
        {
            foreach (var post in new List<Post>(_posts))
            {
                if ((type.HasValue && post.Type != type.Value) || !post.isCanReceive || post.IsExpired) continue;
                if (!await ClaimCoreAsync(post, owner, generation)) return;
                count++;
            }
            if (Current(owner, generation)) Publish(count > 0 ? $"우편 {count}개 수령 완료" : "수령 가능한 우편이 없습니다.");
        }
        catch (Exception error) { if (Current(owner, generation)) { Debug.LogWarning("[Mail] " + error.GetType().Name); Publish($"{count}개 완료, 남은 수령 결과 확인 필요"); } }
        finally { if (Current(owner, generation)) { _busy = false; OnChanged?.Invoke(); } }
    }
    /// <summary>Retries captured reward saving without resubmitting an uncertain claim.</summary>
    public async UniTask RecoverPendingAsync()
    {
        if (_busy || !EnsureOwner()) return;
        string owner = _owner; int generation = _generation; _busy = true; OnChanged?.Invoke();
        try { await RecoverCoreAsync(owner, generation); }
        catch (Exception error) { if (Current(owner, generation)) { Debug.LogWarning("[Mail] " + error.GetType().Name); Publish("이전 수령 기록 확인 필요, 고객지원에 문의해 주세요."); } }
        finally { if (Current(owner, generation)) { _busy = false; OnChanged?.Invoke(); } }
    }
    private async UniTask<bool> RecoverCoreAsync(string owner, int generation)
    {
        string key = PendingPrefix + owner;
        if (!Current(owner, generation)) return false;
        if (!PlayerPrefs.HasKey(key)) { IsDirty = false; return true; }
        IsDirty = true;
        var journal = JsonUtility.FromJson<MailClaimJournal>(PlayerPrefs.GetString(key));
        if (journal == null || string.IsNullOrEmpty(journal.Response)) { Publish("수령 결과 확인 필요, 고객지원에 문의해 주세요. 자동으로 다시 지급하지 않습니다."); return false; }
        var json = JsonMapper.ToObject(journal.Response);
        if (!json.ContainsKey("postItems") || !MailParser.TryRewards(json["postItems"], journal.Id, _config, out var receipt, out _))
        { Publish("보상 설정 확인 필요, 수령 응답은 보관 중입니다."); return false; }
        receipt.Title = journal.Title;
        // Existing balance+receipt snapshot is shared with coupons; mail IDs are explicitly namespaced.
        if (!_gateway.Balances.TryApplyCoupon(receipt)) { Publish("보상 적용 대기, 계정과 보유 한도 확인 필요"); return false; }
        if (!await _gateway.Balances.TrySaveAsync() || !Current(owner, generation)) { if (Current(owner, generation)) Publish("보상 저장을 기다리고 있어요. 연결되면 자동으로 이어집니다."); return false; }
        PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); IsDirty = false;
        _posts.RemoveAll(post => post.Key == journal.Id); Publish("보상 수령 완료"); return true;
    }
    private static async UniTask<MailResponse> WaitAsync(UniTaskCompletionSource<MailResponse> completion, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var timer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(15), DelayType.Realtime);
        return await completion.Task.AttachExternalCancellation(timeout.Token);
    }
    private static void Persist(string key, MailClaimJournal journal) { PlayerPrefs.SetString(key, JsonUtility.ToJson(journal)); PlayerPrefs.Save(); }
    /// <summary>Returns a snapshot so UI sorting cannot change claim identity.</summary>
    public List<Post> GetCurrentPostList()
    {
        var snapshot = new List<Post>(_posts);
        snapshot.Sort((left, right) => { int order = string.CompareOrdinal(right.inDate, left.inDate); return order != 0 ? order : left.Type.CompareTo(right.Type); });
        return snapshot;
    }
}
