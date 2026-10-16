using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One scene-authored mailbox with inline mail and automatic synchronization.</summary>
public class UI_MailboxPanel : UI_Base
{
    [SerializeField] private RectTransform _listContent;
    [SerializeField] private UI_MailItem _itemTemplate;
    [SerializeField] private TextMeshProUGUI _emptyText;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private Button _receiveAllButton;
    private readonly List<UI_MailItem> _rows = new();
    private CancellationTokenSource _openCts;
    private bool _visible;
    private float _nextRetry;
    private float _nextExpiry;
    private int _openVersion;
    private const float RetrySeconds = 30;

    /// <inheritdoc />
    protected override void Awake()
    {
        base.Awake();
        if (_itemTemplate != null) _itemTemplate.gameObject.SetActive(false);
        _receiveAllButton?.onClick.AddListener(ReceiveAll);
    }
    /// <inheritdoc />
    public override async UniTask OpenAsync()
    {
        gameObject.SetActive(true);
        if (_listContent == null || _itemTemplate == null) { Debug.LogError("[Mailbox] Authored view is missing."); return; }
        StopListening();
        _visible = true; int version = ++_openVersion;
        _openCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        var token = _openCts.Token;
        Player.Mail.OnChanged += Refresh;
        _nextRetry = Time.unscaledTime + RetrySeconds;
        await base.OpenAsync(); Refresh();
        await LoadAsync(version, token);
    }
    /// <summary>Web-coupon entry uses the same unified mailbox.</summary>
    public UniTask OpenCouponAsync() => OpenAsync();
    private async UniTask LoadAsync(int version, CancellationToken token)
    {
        try
        {
            await UniTask.WaitUntil(() => !Player.Mail.IsBusy, cancellationToken: token);
            if (!_visible || version != _openVersion) return;
            await Player.Mail.LoadAllAsync();
            if (_visible && version == _openVersion) Refresh();
        }
        catch (OperationCanceledException) { }
    }
    /// <inheritdoc />
    public override async UniTask CloseAsync()
    { _visible = false; StopListening(); await base.CloseAsync(); gameObject.SetActive(false); }
    private void StopListening()
    {
        Player.Mail.OnChanged -= Refresh;
        _openCts?.Cancel(); _openCts?.Dispose(); _openCts = null;
    }
    private void OnDisable() { _visible = false; StopListening(); }
    private void OnDestroy()
    {
        StopListening();
        _receiveAllButton?.onClick.RemoveListener(ReceiveAll);
    }
    private void ReceiveAll() => Player.Mail.ClaimAllAsync().Forget();
    private void OnApplicationFocus(bool focused) { if (focused) ReloadOnReturn(); }
    private void OnApplicationPause(bool paused) { if (!paused) ReloadOnReturn(); }
    private void ReloadOnReturn()
    {
        if (!_visible || _openCts == null || Player.Mail.IsBusy) return;
        _nextRetry = Time.unscaledTime + RetrySeconds;
        LoadAsync(_openVersion, _openCts.Token).Forget();
    }
    private void Update()
    {
        if (!_visible) return;
        if (Time.unscaledTime >= _nextExpiry)
        {
            _nextExpiry = Time.unscaledTime + 1;
            foreach (var row in _rows) if (row.gameObject.activeSelf) row.RefreshAvailability(Player.Mail.IsBusy);
            UpdateReceiveAll(Player.Mail.GetCurrentPostList());
        }
        if (Time.unscaledTime < _nextRetry || Player.Mail.IsBusy || _openCts == null) return;
        _nextRetry = Time.unscaledTime + RetrySeconds;
        if (Player.Mail.NeedsReload) LoadAsync(_openVersion, _openCts.Token).Forget();
        else if (Player.Mail.IsDirty && Player.Mail.HasRecoverableClaim) Player.Mail.RecoverPendingAsync().Forget();
    }
    private void Refresh()
    {
        if (!_visible || _itemTemplate == null) return;
        var posts = Player.Mail.GetCurrentPostList();
        _statusText.text = Player.Mail.Status == "우편 불러오는 중…" ? "" : Player.Mail.Status;
        _emptyText.gameObject.SetActive(posts.Count == 0);
        _emptyText.text = "우편함이 비어 있습니다";
        for (int i = 0; i < posts.Count; i++)
        {
            if (i >= _rows.Count) _rows.Add(Instantiate(_itemTemplate, _listContent));
            var post = posts[i]; var row = _rows[i]; row.gameObject.SetActive(true);
            row.Bind(post, Player.Mail.IsBusy, () => Player.Mail.MarkRead(post), () => Claim(post).Forget());
        }
        for (int i = posts.Count; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
        UpdateReceiveAll(posts);
    }
    private void UpdateReceiveAll(List<Post> posts) => _receiveAllButton.interactable = !Player.Mail.IsBusy && posts.Exists(post => post.isCanReceive && !post.IsExpired);
    private async UniTaskVoid Claim(Post post)
    { Player.Mail.MarkRead(post); await Player.Mail.ClaimAsync(post); }
}
