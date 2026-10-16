using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;

/// <summary>Coupon browser lifecycle and prefab-authored transient feedback.</summary>
public sealed class LobbyWebCouponController : MonoBehaviour
{
    [Inject] private WebCouponService _webCoupons;
    [SerializeField] private GameObject _feedbackPanel;
    [SerializeField] private TMP_Text _feedbackText;
    private float _feedbackUntil;
    private const float FeedbackSeconds = 5;
    private void Awake() => _feedbackPanel.SetActive(false);
    /// <summary>Opens the UID-bound web page without granting rewards.</summary>
    public void Launch()
    {
        if (_webCoupons == null) { ShowFeedback("계정 정보를 불러오지 못했습니다. 다시 시도해 주세요."); return; }
        _webCoupons.TryOpen(out string message); ShowFeedback(message);
    }
    private void OnApplicationFocus(bool focused) { if (focused) CheckReturnAsync().Forget(); }
    private void OnApplicationPause(bool paused) { if (!paused) CheckReturnAsync().Forget(); }
    private async UniTaskVoid CheckReturnAsync()
    {
        if (_webCoupons == null) return;
        string message = await _webCoupons.ReturnAsync(this.GetCancellationTokenOnDestroy());
        if (this != null && message != null) ShowFeedback(message);
    }
    private void ShowFeedback(string message)
    { _feedbackText.text = message; _feedbackPanel.SetActive(true); _feedbackUntil = Time.unscaledTime + FeedbackSeconds; }
    private void Update() { if (_feedbackPanel.activeSelf && Time.unscaledTime >= _feedbackUntil) _feedbackPanel.SetActive(false); }
}
