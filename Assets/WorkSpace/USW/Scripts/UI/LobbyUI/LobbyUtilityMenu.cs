using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Opens and closes the utility grid without knowing its destinations.</summary>
public sealed class LobbyUtilityMenu : UI_Base
{
    [SerializeField] private RectTransform _sheet;
    [SerializeField] private GameObject _overlay;
    [SerializeField] private Button _trigger;
    private bool _initialized;
    /// <summary>Whether the utility grid currently blocks the lobby.</summary>
    public bool IsOpen => _overlay != null && _overlay.activeSelf;
    /// <summary>Binds only menu visibility controls, once.</summary>
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        _trigger?.onClick.AddListener(ToggleMenu);
        _overlay?.GetComponent<Button>()?.onClick.AddListener(Close);
        if (_overlay != null) _overlay.SetActive(false);
    }
    private void ToggleMenu() { if (IsOpen) Close(); else Open(); }
    /// <inheritdoc />
    public override UniTask OpenAsync() { _overlay.SetActive(true); return UniTask.CompletedTask; }
    /// <inheritdoc />
    public override UniTask CloseAsync() { if (_overlay != null) _overlay.SetActive(false); return UniTask.CompletedTask; }
    private void OnDisable() { if (_overlay != null) _overlay.SetActive(false); }
    private void OnDestroy()
    {
        _trigger?.onClick.RemoveListener(ToggleMenu);
        _overlay?.GetComponent<Button>()?.onClick.RemoveListener(Close);
    }
}
