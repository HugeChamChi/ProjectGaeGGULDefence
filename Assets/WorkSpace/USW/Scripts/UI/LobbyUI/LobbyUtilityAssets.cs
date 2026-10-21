using UnityEngine;

/// <summary>Shared authored assets for the lobby utilities; settings use the production ingame prefab.</summary>
[CreateAssetMenu(menuName = "UI/Lobby Utility Assets", fileName = "LobbyUtilityAssets")]
public sealed class LobbyUtilityAssets : ScriptableObject
{
    /// <summary>The same settings prefab referenced by IngameOptionsMenu.</summary>
    public GameObject SettingsPrefab;
    /// <summary>Prefab-authored 7-day attendance popup.</summary>
    public AttendancePopup AttendancePrefab;
    /// <summary>Prefab-authored account popup (member code, Google link, push).</summary>
    public AccountPopup AccountPrefab;
    /// <summary>Mailbox, settings, coupon, attendance, daily missions, notices, help and support icons.</summary>
    public Sprite[] Icons = new Sprite[8];
}
