using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>A button's independently authored popup destination.</summary>
[RequireComponent(typeof(Button))]
public sealed class LobbyPopupAction : MonoBehaviour
{
    [SerializeField] private UI_Base _prefab;
    [SerializeField] private LobbyUtilityMenu _menu;
    [Inject] private LobbyPopupHost _host;
    private Button _button;
    /// <summary>Prefab edited independently from the lobby scene.</summary>
    public UI_Base Prefab => _prefab;
    private void Awake() { _button = GetComponent<Button>(); _button.onClick.AddListener(Execute); }
    /// <summary>Closes the optional menu and opens this button's popup.</summary>
    public void Execute() { _menu?.Close(); _host?.OpenPopup(_prefab); }
    private void OnDestroy() { if (_button != null) _button.onClick.RemoveListener(Execute); }
}
