using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>Reference-style profile sheet, cosmetic preview and free nickname editor.</summary>
public sealed class LobbyProfilePopup : UI_Base
{
    private const float MainHeight = 1000;
    private const float AppearanceHeight = 1440;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Button _close;
    [SerializeField] private Button _dim;
    [SerializeField] private GameObject _mainPage;
    [SerializeField] private GameObject _appearancePage;
    [SerializeField] private GameObject _nicknameOverlay;
    [SerializeField] private Button _portraitButton;
    [SerializeField] private Button _editName;
    [SerializeField] private Button _copyName;
    [SerializeField] private Button _copyUid;
    [SerializeField] private TMP_Text _nickname;
    [SerializeField] private TMP_Text _memberCode;
    [SerializeField] private TMP_Text _level;
    [SerializeField] private TMP_Text _experience;
    [SerializeField] private Image _experienceFill;
    [SerializeField] private TMP_Text _bestRound;
    [SerializeField] private TMP_Text _normalRecord;
    [SerializeField] private TMP_Text _message;
    [SerializeField] private Image _portrait;
    [SerializeField] private Image _frame;
    [SerializeField] private Image _previewPortrait;
    [SerializeField] private Image _previewFrame;
    [SerializeField] private TMP_Text _previewName;
    [SerializeField] private TMP_Text _selectionName;
    [SerializeField] private TMP_Text _appearanceMessage;
    [SerializeField] private Button _iconTab;
    [SerializeField] private Button _frameTab;
    [SerializeField] private Button _apply;
    [SerializeField] private ScrollRect _catalogScroll;
    [SerializeField] private ProfileCatalogSlot[] _slots;
    [SerializeField] private TMP_InputField _nameInput;
    [SerializeField] private TMP_Text _nameGuide;
    [SerializeField] private TMP_Text _nameMessage;
    [SerializeField] private Button _confirmName;
    [SerializeField] private Button _cancelName;

    private ProfileNicknameService _names;
    private ProfilePopupConfig _config;
    private LobbyProfilePresenter _presenter;
    private Tween _motion;
    private int _motionVersion;
    private bool _frames;
    private bool _changing;
    private bool _applying;
    private int _iconId;
    private int _frameId;
    private string _owner;

    /// <summary>Scene popup instances receive their scoped services through LobbyPopupHost.</summary>
    [Inject] public void Construct(ProfileNicknameService names, ProfilePopupConfig config)
    { _names = names; _config = config; }

    protected override void Awake()
    {
        base.Awake();
        _close.onClick.AddListener(Back);
        _dim.onClick.AddListener(Dismiss);
        _portraitButton.onClick.AddListener(OpenAppearance);
        _editName.onClick.AddListener(OpenNickname);
        _copyName.onClick.AddListener(CopyName);
        _copyUid.onClick.AddListener(CopyUid);
        _iconTab.onClick.AddListener(Icons);
        _frameTab.onClick.AddListener(Frames);
        _apply.onClick.AddListener(Apply);
        _confirmName.onClick.AddListener(ConfirmName);
        _cancelName.onClick.AddListener(CancelNickname);
    }

    /// <inheritdoc />
    public override async UniTask OpenAsync()
    {
        _presenter ??= new LobbyProfilePresenter(this, _config);
        _owner = BackEnd.Backend.UserInDate;
        ShowMain(); _message.text = ""; _presenter.Refresh();
        _canvas.enabled = true;
        int version = ++_motionVersion; _motion?.Kill();
        _panel.localScale = Vector3.one * .88f;
        _motion = _panel.DOScale(1, .22f).SetEase(Ease.OutBack).SetUpdate(true);
        RefreshNameAsync().Forget();
        try { await _motion.ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy()); }
        catch (OperationCanceledException) { return; }
        if (version == _motionVersion) OnOpened?.Invoke();
    }

    /// <inheritdoc />
    public override async UniTask CloseAsync()
    {
        if (_changing || _applying) return;
        int version = ++_motionVersion; _motion?.Kill();
        _motion = _panel.DOScale(.92f, .14f).SetEase(Ease.InCubic).SetUpdate(true);
        try { await _motion.ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy()); }
        catch (OperationCanceledException) { return; }
        if (version != _motionVersion) return;
        _canvas.enabled = false; _nicknameOverlay.SetActive(false); OnClosed?.Invoke();
    }

    private async UniTaskVoid RefreshNameAsync()
    {
        if (!Table.Profile.IsReady) await Table.Profile.InitializeAsync();
        if (!Player.Profile.IsReady) await Player.Profile.InitalizeAsync();
        await _names.RefreshAsync(this.GetCancellationTokenOnDestroy());
        if (this != null) _presenter.Refresh();
    }

    /// <summary>Refreshes every display from the same real account snapshot.</summary>
    public void Render(PlayerData data, int? normalBest)
    {
        _nickname.text = data == null ? "—" : string.IsNullOrEmpty(data.PlayerName) ? "플레이어" : data.PlayerName;
        _previewName.text = _nickname.text;
        _memberCode.text = string.IsNullOrEmpty(BackEnd.Backend.UID) ? "—" : BackEnd.Backend.UID;
        _level.text = data == null ? "—" : data.PlayerLevel.ToString();
        _experience.text = data == null ? "— / —" : data.MaxExp <= 0 ? "MAX" : $"{data.PlayerExp:N0} / {data.MaxExp:N0}";
        _experienceFill.fillAmount = data == null ? 0 : data.MaxExp <= 0 ? 1 : Mathf.Clamp01((float)data.PlayerExp / data.MaxExp);
        _bestRound.text = normalBest.HasValue ? normalBest.Value.ToString("N0") : "—";
        _normalRecord.text = _bestRound.text;
        SetImage(_portrait, _presenter.Artwork(Table.Profile.GetIcon(Player.Profile.Data.CurrentIconId)));
        SetImage(_frame, _presenter.Artwork(Table.Profile.GetFrame(Player.Profile.Data.CurrentFrameId)));
        if (_appearancePage.activeSelf) RenderCatalog();
    }
    private static void SetImage(Image image, Sprite sprite) { image.sprite = sprite; image.enabled = sprite != null; }
    private void Back() { if (_changing || _applying) return; if (_appearancePage.activeSelf) ShowMain(); else Dismiss(); }
    private void Dismiss() { if (!_changing && !_applying) Close(); }
    private void ShowMain()
    { _mainPage.SetActive(true); _appearancePage.SetActive(false); _nicknameOverlay.SetActive(false); SetHeight(MainHeight); }
    private void SetHeight(float height) => _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, height);
    private void CopyName() { if (Player.PlayerData.IsReady) { GUIUtility.systemCopyBuffer = _nickname.text; _message.text = "닉네임을 복사했어요."; } }
    private void CopyUid() { if (!string.IsNullOrEmpty(BackEnd.Backend.UID)) { GUIUtility.systemCopyBuffer = BackEnd.Backend.UID; _message.text = "회원 코드를 복사했어요."; } }
    private void OpenAppearance()
    {
        _iconId = Player.Profile.Data.CurrentIconId; _frameId = Player.Profile.Data.CurrentFrameId;
        _mainPage.SetActive(false); _appearancePage.SetActive(true); _frames = false;
        _appearanceMessage.text = ""; SetHeight(AppearanceHeight); ResetScroll(); RenderCatalog();
    }
    private void Icons() { _frames = false; ResetScroll(); RenderCatalog(); }
    private void Frames() { _frames = true; ResetScroll(); RenderCatalog(); }
    private void ResetScroll() { _catalogScroll.StopMovement(); _catalogScroll.verticalNormalizedPosition = 1; }
    private void Select(IProfileItem item)
    {
        if (item == null || _applying) return;
        if (!_presenter.IsOwned(item)) { _selectionName.text = _frames ? "미보유 프레임" : "미보유 프로필"; _appearanceMessage.text = "아직 보유하지 않은 항목이에요."; return; }
        if (_frames) _frameId = item.Id; else _iconId = item.Id;
        _appearanceMessage.text = ""; RenderCatalog();
    }
    private void RenderCatalog()
    {
        var items = _presenter.Catalog(_frames);
        int selected = _frames ? _frameId : _iconId;
        int equipped = _frames ? Player.Profile.Data.CurrentFrameId : Player.Profile.Data.CurrentIconId;
        for (int i = 0; i < _slots.Length; i++)
        {
            var item = i < items.Count ? items[i] : null;
            _slots[i].Render(item, _presenter.Artwork(item), _presenter.IsOwned(item), item != null && item.Id == selected,
                item != null && item.Id == equipped, Select);
        }
        SetImage(_previewPortrait, _presenter.Artwork(Table.Profile.GetIcon(_iconId)));
        SetImage(_previewFrame, _presenter.Artwork(Table.Profile.GetFrame(_frameId)));
        var current = _frames ? (IProfileItem)Table.Profile.GetFrame(selected) : Table.Profile.GetIcon(selected);
        _selectionName.text = current?.Name ?? "";
        _iconTab.image.color = _frames ? LobbyUtilityVisuals.Card : LobbyUtilityVisuals.CardFeatured;
        _frameTab.image.color = !_frames ? LobbyUtilityVisuals.Card : LobbyUtilityVisuals.CardFeatured;
        _apply.interactable = !_applying && (_iconId != Player.Profile.Data.CurrentIconId || _frameId != Player.Profile.Data.CurrentFrameId || Player.Profile.IsDirty);
    }
    private void Apply() => ApplyAsync().Forget();
    private async UniTaskVoid ApplyAsync()
    {
        if (_applying || _owner != BackEnd.Backend.UserInDate) return;
        if (!_presenter.IsOwned(Table.Profile.GetIcon(_iconId)) || !_presenter.IsOwned(Table.Profile.GetFrame(_frameId))) return;
        _applying = true;
        try
        {
            if (!Player.Profile.IsReady) await Player.Profile.InitalizeAsync();
            if (this == null || _owner != BackEnd.Backend.UserInDate) return;
            if (!Player.Profile.TrySelect(_iconId, _frameId)) { _appearanceMessage.text = "프로필 정보를 다시 불러와 주세요."; return; }
            RenderCatalog();
            bool saved = await Player.Profile.TrySaveAsync(this.GetCancellationTokenOnDestroy());
            if (this == null || _owner != BackEnd.Backend.UserInDate) return;
            _appearanceMessage.text = saved ? "변경했어요." : "저장하지 못했어요. 변경 버튼을 눌러 다시 시도해 주세요.";
            _presenter.Refresh();
        }
        catch (OperationCanceledException) { }
        finally { _applying = false; if (this != null) RenderCatalog(); }
    }
    private void OpenNickname()
    {
        if (!Player.PlayerData.IsReady) { _message.text = "계정 정보를 먼저 불러와 주세요."; return; }
        _nameInput.SetTextWithoutNotify(Player.PlayerData.Data.PlayerName ?? "");
        _nameGuide.text = _names.Guide; _nameMessage.text = ""; _nicknameOverlay.SetActive(true);
        _nameInput.Select(); _nameInput.ActivateInputField();
    }
    private void CancelNickname() { if (!_changing) _nicknameOverlay.SetActive(false); }
    private void ConfirmName() => ChangeNameAsync().Forget();
    private async UniTaskVoid ChangeNameAsync()
    {
        if (_changing || _owner != BackEnd.Backend.UserInDate) return;
        _changing = true; _confirmName.interactable = false; _cancelName.interactable = false; _nameInput.interactable = false;
        _nameMessage.text = "변경 중…";
        try
        {
            string result = await _names.ChangeAsync(_nameInput.text, this.GetCancellationTokenOnDestroy());
            if (this == null || _owner != BackEnd.Backend.UserInDate) return;
            _nameMessage.text = result; _presenter.Refresh();
            if (result.StartsWith("닉네임을 변경했어요.", StringComparison.Ordinal))
            { _nicknameOverlay.SetActive(false); _message.text = result; }
        }
        finally
        {
            _changing = false;
            if (this != null) { _confirmName.interactable = true; _cancelName.interactable = true; _nameInput.interactable = true; }
        }
    }
    private void OnDestroy()
    {
        _motion?.Kill(); _presenter?.Dispose();
        _close.onClick.RemoveListener(Back); _dim.onClick.RemoveListener(Dismiss);
        _portraitButton.onClick.RemoveListener(OpenAppearance); _editName.onClick.RemoveListener(OpenNickname);
        _copyName.onClick.RemoveListener(CopyName); _copyUid.onClick.RemoveListener(CopyUid);
        _iconTab.onClick.RemoveListener(Icons); _frameTab.onClick.RemoveListener(Frames); _apply.onClick.RemoveListener(Apply);
        _confirmName.onClick.RemoveListener(ConfirmName); _cancelName.onClick.RemoveListener(CancelNickname);
    }
}
