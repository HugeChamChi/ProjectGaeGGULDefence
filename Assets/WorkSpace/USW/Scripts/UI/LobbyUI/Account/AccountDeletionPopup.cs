using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>Explicit account deletion confirmation, separate from the account information popup.</summary>
public sealed class AccountDeletionPopup : UI_Base
{
    [Inject] private AccountDeletionService _service;
    [SerializeField] private Button _confirm;
    [SerializeField] private TMP_Text _confirmLabel;
    [SerializeField] private TMP_Text _message;
    private string _owner;
    private bool _waiting;
    protected override void Awake() { base.Awake(); _confirm.onClick.AddListener(Confirm); }
    /// <inheritdoc />
    public override void Open()
    {
        _owner = _service.Owner;
        _message.text = "계정과 보유 재화, 유닛, 진행 기록을 삭제해요. 삭제 요청은 되돌릴 수 없어요.\n\n데이터 삭제까지 최대 1시간 걸릴 수 있어요. 계속할까요?";
        Refresh(); base.Open();
    }
    /// <inheritdoc />
    public override UniTask CloseAsync() => _waiting || _service.IsBusy || _service.IsDeleted ? UniTask.CompletedTask : base.CloseAsync();
    private void Confirm()
    {
        if (_service.IsDeleted) { Application.Quit(); return; }
        if (_waiting || _service.IsBusy) return;
        DeleteAsync().Forget();
    }
    private async UniTaskVoid DeleteAsync()
    {
        _waiting = true; Refresh(); _message.text = "계정 삭제를 요청하고 있어요…";
        try { _message.text = await _service.DeleteAsync(_owner, this.GetCancellationTokenOnDestroy()); }
        finally { _waiting = false; if (this != null) Refresh(); }
    }
    private void Update()
    {
        // A late server response still completes cleanup even after the UI's wait timed out.
        if (_service.IsDeleted && _confirmLabel.text != "게임 종료")
        { _message.text = "계정 삭제가 접수됐어요. 데이터 삭제까지 최대 1시간 걸릴 수 있어요.\n게임을 종료해 주세요. 다시 시작하면 새 계정으로 시작해요."; Refresh(); }
        else if (!_waiting && _confirm.interactable == _service.IsBusy) Refresh();
    }
    private void Refresh()
    {
        bool blocked = _waiting || _service.IsBusy;
        _confirm.interactable = !blocked;
        _confirmLabel.text = _service.IsDeleted ? "게임 종료" : "계정 삭제";
        if (btn_Close != null) btn_Close.interactable = !blocked && !_service.IsDeleted;
        if (btn_BackgroundClose != null) btn_BackgroundClose.interactable = !blocked && !_service.IsDeleted;
    }
    private void OnDestroy() { _confirm.onClick.RemoveListener(Confirm); }
}
