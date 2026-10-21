using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>Prefab-authored device-local graphics/language supplement.</summary>
public sealed class LobbyDeviceSettingsView : UI_Base
{
    [Inject] private DeviceSettingsService _device;
    [SerializeField] private Button _qualityButton;
    [SerializeField] private Button _languageButton;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private TMP_Text _quality;
    [SerializeField] private TMP_Text _language;
    protected override void Awake()
    {
        base.Awake();
        _qualityButton.onClick.AddListener(ChangeQuality);
        _languageButton.onClick.AddListener(ChangeLanguage);
    }
    /// <inheritdoc />
    public override async UniTask OpenAsync()
    { gameObject.SetActive(true); _status.text = ""; transform.SetAsLastSibling(); Refresh(); await base.OpenAsync(); }
    /// <inheritdoc />
    public override async UniTask CloseAsync() { await base.CloseAsync(); gameObject.SetActive(false); }
    private void ChangeQuality() { _device.NextQuality(); Refresh(); }
    private void ChangeLanguage() { _status.text = _device.NextLanguage(); Refresh(); }
    private void Refresh() { _quality.text = "그래픽  " + _device.Quality; _language.text = "언어  " + _device.Language; }
    private void OnDestroy()
    { _qualityButton.onClick.RemoveListener(ChangeQuality); _languageButton.onClick.RemoveListener(ChangeLanguage); }
}
