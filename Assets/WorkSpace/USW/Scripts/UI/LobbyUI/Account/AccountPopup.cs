using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// Lobby account center: member code copy, Google link and push consent/OS state.
/// Also surfaces save-sync recovery only while account data has an unsaved or conflicting change.
/// </summary>
public sealed class AccountPopup : UI_Base
{
    private static readonly Color ToggleOn = new(1f, .74f, .26f, 1);
    private static readonly Color ToggleOff = new(.24f, .20f, .36f, 1);
    private static readonly Color ToggleOnLabel = new(.20f, .12f, .02f, 1);
    private static readonly Color ToggleOffLabel = new(.84f, .80f, .95f, 1);
    private const float PanelHeight = 880;
    private const float PanelHeightWithSync = 1000;

    [Inject] private AccountSettingsService _account;
    [Inject] private DeviceSettingsService _device;
    [Inject] private AccountSwitchService _switch;

    [SerializeField] private RectTransform _panel;
    [SerializeField] private TMP_Text _memberCode;
    [SerializeField] private Button _copy;
    [SerializeField] private Button _google;
    [SerializeField] private TMP_Text _googleLabel;
    [SerializeField] private TMP_Text _googleStatus;
    [SerializeField] private GameObject _googleLinkedMark;
    [SerializeField] private Button _pushToggle;
    [SerializeField] private Image _pushToggleBackground;
    [SerializeField] private TMP_Text _pushToggleLabel;
    [SerializeField] private TMP_Text _pushStatus;
    [SerializeField] private GameObject _syncRow;
    [SerializeField] private TMP_Text _syncStatus;
    [SerializeField] private Button _retrySave;
    [SerializeField] private Button _useServer;
    [SerializeField] private TMP_Text _message;
    [SerializeField] private Button _changeAccount;
    [SerializeField] private Button _deleteAccount;

    private bool _busy;
    private bool _confirmServer;
    private bool _confirmSwitch;

    protected override void Awake()
    {
        base.Awake();
        _copy?.onClick.AddListener(CopyMemberCode);
        _google?.onClick.AddListener(() => LinkGoogleAsync().Forget());
        _pushToggle?.onClick.AddListener(TogglePush);
        _retrySave?.onClick.AddListener(() => RetrySaveAsync().Forget());
        _useServer?.onClick.AddListener(UseServerRecord);
        _changeAccount?.onClick.AddListener(ChangeAccount);
        if (Player.PlayerData != null) Player.PlayerData.OnSaveStatusChanged += RefreshSync;
    }

    private void OnDestroy()
    {
        if (Player.PlayerData != null) Player.PlayerData.OnSaveStatusChanged -= RefreshSync;
        _changeAccount?.onClick.RemoveListener(ChangeAccount);
    }

    /// <inheritdoc />
    public override void Open()
    {
        base.Open();
        _confirmServer = false; _confirmSwitch = false; Message("");
        Refresh();
        LoadAsync().Forget();
    }

    private async UniTaskVoid LoadAsync()
    {
        if (_account == null) { Message("계정 정보를 불러오지 못했어요."); return; }
        string result = await _account.RefreshAsync(this.GetCancellationTokenOnDestroy());
        if (this == null) return;
        Refresh();
        if (!string.IsNullOrEmpty(result)) Message(result);
    }

    private void Refresh()
    {
        string code = _account?.MemberCode;
        if (_memberCode != null) _memberCode.text = string.IsNullOrEmpty(code) ? "로그인이 필요해요" : code;
        bool linked = _account != null && _account.IsLinked;
        if (_googleLabel != null) _googleLabel.text = linked ? "Google 연동됨" : "Google 연동";
        if (_googleStatus != null) { _googleStatus.text = ""; _googleStatus.gameObject.SetActive(false); }
        if (_googleLinkedMark != null) _googleLinkedMark.SetActive(linked);
        if (_google != null) _google.interactable = !linked && !_busy;
        if (_changeAccount != null) _changeAccount.interactable = !_busy && (_switch == null || !_switch.IsBusy);
        if (_deleteAccount != null) _deleteAccount.interactable = !_busy && (_switch == null || !_switch.NeedsRecovery);
        RefreshPush();
        RefreshSync();
    }

    private void RefreshPush()
    {
        if (_device == null) return;
        bool on = _device.PushConsent;
        if (_pushToggleBackground != null) _pushToggleBackground.color = on ? ToggleOn : ToggleOff;
        if (_pushToggleLabel != null) { _pushToggleLabel.text = on ? "ON" : "OFF"; _pushToggleLabel.color = on ? ToggleOnLabel : ToggleOffLabel; }
        if (_pushStatus != null) _pushStatus.text = _device.PushStatus;
    }

    private void RefreshSync()
    {
        var data = Player.PlayerData;
        bool show = data != null && (data.HasSaveConflict || (data.IsDirty && !string.IsNullOrEmpty(data.SaveStatus)));
        if (_syncRow != null) _syncRow.SetActive(show);
        if (_panel != null) _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, show ? PanelHeightWithSync : PanelHeight);
        if (show && _syncStatus != null) _syncStatus.text = data.HasSaveConflict ? "다른 기기의 저장 기록과 충돌했어요" : "데이터 저장: " + data.SaveStatus;
    }

    private void CopyMemberCode()
    {
        string code = _account?.MemberCode;
        if (string.IsNullOrEmpty(code)) { Message("로그인이 필요해요."); return; }
        GUIUtility.systemCopyBuffer = code; Message("회원 코드를 복사했어요.");
    }

    private async UniTaskVoid LinkGoogleAsync()
    {
        if (_busy || _account == null) return;
        _busy = true; Refresh(); Message("Google 연동 중…");
        try
        {
            string result = await _account.LinkGoogleAsync(this.GetCancellationTokenOnDestroy());
            if (this == null) return;
            Refresh(); Message(result);
        }
        finally { _busy = false; if (this != null) Refresh(); }
    }

    /// <inheritdoc />
    public override UniTask CloseAsync() => _busy || (_switch != null && (_switch.IsBusy || _switch.NeedsRecovery)) ? UniTask.CompletedTask : base.CloseAsync();

    private void ChangeAccount()
    {
        if (_busy || _switch == null) return;
        if (!_switch.NeedsRecovery && !_confirmSwitch)
        {
            _confirmSwitch = true;
            Message(_account != null && _account.CanLink
                ? "현재 게스트 진행은 Google에 연동되지 않았어요. 다른 계정으로 이동하려면 계정 변경을 한 번 더 눌러 주세요."
                : "다른 Google 계정으로 로그인할까요? 계정 변경을 한 번 더 눌러 주세요.");
            return;
        }
        _confirmSwitch = false; SwitchAccountAsync().Forget();
    }
    private async UniTaskVoid SwitchAccountAsync()
    {
        _busy = true; Refresh(); Message("Google 계정을 선택해 주세요.");
        try
        {
            string result = await _switch.SwitchAsync(this.GetCancellationTokenOnDestroy());
            if (this != null) { Refresh(); Message(result); }
        }
        catch (System.OperationCanceledException) { }
        finally { _busy = false; if (this != null) Refresh(); }
    }

    private void TogglePush()
    {
        if (_device == null) return;
        _device.SetPushConsent(!_device.PushConsent);
        RefreshPush();
        Message(_device.PushConsent ? "푸시 알림 수신에 동의했어요." : "푸시 알림을 받지 않아요.");
    }

    private async UniTaskVoid RetrySaveAsync()
    {
        if (Player.PlayerData == null) return;
        Message("저장 중…");
        bool saved = await Player.PlayerData.TrySaveAsync();
        if (this == null) return;
        RefreshSync(); Message(saved ? "저장했어요." : Player.PlayerData.SaveStatus);
    }

    private void UseServerRecord()
    {
        var data = Player.PlayerData;
        if (data == null || !data.HasSaveConflict) { Message("저장 충돌이 없어요."); return; }
        if (!_confirmServer) { _confirmServer = true; Message("이 기기에 남은 변경을 버리고 서버 기록을 사용해요. 한 번 더 누르면 적용돼요."); return; }
        _confirmServer = false; data.AcceptServerAfterConflict(); RefreshSync(); Message("서버 기록을 적용했어요.");
    }

    // Returning from the OS notification permission dialog or browser updates the shown state.
    private void OnApplicationFocus(bool focus) { if (focus && _canvas != null && _canvas.enabled) RefreshPush(); }

    private void Message(string text) { if (_message != null) _message.text = text; }
}
