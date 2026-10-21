using Cysharp.Threading.Tasks;
using HSD.UI.Setting;
using UnityEngine;

/// <summary>Lobby wrapper around the unchanged nested in-game settings prefab.</summary>
public sealed class LobbySettingsPopup : UI_Base
{
    [SerializeField] private UI_SettingPanel_InGame _settings;
    protected override void Awake()
    { base.Awake(); _settings.GetComponent<SafeAreaContentFrame>()?.UseFullSafeArea(); }
    /// <inheritdoc />
    public override UniTask OpenAsync() => _settings.OpenAsync();
    /// <inheritdoc />
    public override UniTask CloseAsync() => _settings.CloseAsync();
}
