using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>Starts the coupon feature from its own menu tile.</summary>
[RequireComponent(typeof(Button))]
public sealed class LobbyWebCouponAction : MonoBehaviour
{
    [SerializeField] private LobbyWebCouponController _prefab;
    [SerializeField] private LobbyUtilityMenu _menu;
    [Inject] private LobbyPopupHost _host;
    private Button _button;
    /// <summary>Feature prefab containing browser-return handling and feedback.</summary>
    public LobbyWebCouponController Prefab => _prefab;
    private void Awake() { _button = GetComponent<Button>(); _button.onClick.AddListener(Execute); }
    /// <summary>Launches the coupon feature independently of the hidden menu.</summary>
    public void Execute() { _menu?.Close(); _host?.GetOrCreate(_prefab)?.Launch(); }
    private void OnDestroy() { if (_button != null) _button.onClick.RemoveListener(Execute); }
}
