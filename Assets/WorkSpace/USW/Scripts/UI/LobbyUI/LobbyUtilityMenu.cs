using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Opens and closes the utility grid without knowing its destinations.</summary>
public sealed class LobbyUtilityMenu : UI_Base
{
    [SerializeField] private RectTransform _sheet;
    [SerializeField] private GameObject _overlay;
    [SerializeField] private Button _trigger;
    [Header("Open/close tween")]
    [SerializeField] private float _openDuration = 0.22f;
    [SerializeField] private float _closeDuration = 0.14f;
    [SerializeField] private float _closedScale = 0.85f;
    [SerializeField] private Ease _openEase = Ease.OutBack;
    [SerializeField] private Ease _closeEase = Ease.InQuad;
    private CanvasGroup _fade;
    private Sequence _tween;
    private bool _open;
    private bool _initialized;
    private bool _available = true;
    /// <summary>Whether the utility grid currently blocks the lobby (false as soon as closing starts).</summary>
    public bool IsOpen => _open;
    /// <summary>Shows the trigger only on the battle page and closes the sheet immediately when leaving it.</summary>
    public void SetAvailable(bool available)
    {
        _available = available;
        if (_trigger != null) _trigger.gameObject.SetActive(available);
        if (!available) HideImmediately();
    }
    /// <summary>Binds only menu visibility controls, once.</summary>
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        _trigger?.onClick.AddListener(ToggleMenu);
        _overlay?.GetComponent<Button>()?.onClick.AddListener(Close);
        if (_overlay != null) _overlay.SetActive(false);
    }
    // The overlay's dim and the sheet fade together; the sheet pops from its top-right pivot.
    private CanvasGroup Fade()
    {
        if (_fade == null && !_overlay.TryGetComponent(out _fade)) _fade = _overlay.AddComponent<CanvasGroup>();
        return _fade;
    }
    private void ToggleMenu() { if (IsOpen) Close(); else Open(); }
    /// <inheritdoc />
    public override UniTask OpenAsync()
    {
        if (_overlay == null || !_available) return UniTask.CompletedTask;
        _open = true;
        _tween?.Kill();
        _overlay.SetActive(true);
        var fade = Fade();
        fade.interactable = fade.blocksRaycasts = true;
        fade.alpha = 0f;
        if (_sheet != null) _sheet.localScale = Vector3.one * _closedScale;
        _tween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Join(fade.DOFade(1f, _openDuration).SetEase(Ease.OutQuad));
        if (_sheet != null) _tween.Join(_sheet.DOScale(1f, _openDuration).SetEase(_openEase));
        return UniTask.CompletedTask;
    }
    /// <inheritdoc />
    public override UniTask CloseAsync()
    {
        if (_overlay == null || !_overlay.activeSelf) { _open = false; return UniTask.CompletedTask; }
        _open = false;
        _tween?.Kill();
        // Stop taps during the fade so a second tap can't hit a vanishing button.
        var fade = Fade();
        fade.interactable = fade.blocksRaycasts = false;
        _tween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Join(fade.DOFade(0f, _closeDuration).SetEase(_closeEase))
            .OnComplete(HideImmediately);
        if (_sheet != null) _tween.Join(_sheet.DOScale(_closedScale, _closeDuration).SetEase(_closeEase));
        return UniTask.CompletedTask;
    }
    private void HideImmediately()
    {
        _tween?.Kill(); _tween = null;
        _open = false;
        if (_overlay != null) _overlay.SetActive(false);
        if (_sheet != null) _sheet.localScale = Vector3.one;
    }
    private void OnDisable() => HideImmediately();
    private void OnDestroy()
    {
        _tween?.Kill();
        _trigger?.onClick.RemoveListener(ToggleMenu);
        _overlay?.GetComponent<Button>()?.onClick.RemoveListener(Close);
    }
}
