using UnityEngine;
using UnityEngine.UI;

public class InGame_UI_Controller : MonoBehaviour
{
    [Header("Setting")]
    [SerializeField] private UI_Base ui_Setting;
    [SerializeField] private Button btn_Setting;

    [Header("Upgrade")]
    [SerializeField] private UI_Base ui_Upgrade;
    [SerializeField] private Button btn_Upgrade;

    private void Start()
    {
        btn_Setting.onClick.AddListener(() => ui_Setting.Open());
        btn_Upgrade.onClick.AddListener(() => ui_Upgrade.Open());
    }

    private void OnDestroy()
    {
        btn_Setting.onClick.RemoveAllListeners();
        btn_Upgrade.onClick.RemoveAllListeners();
    }
}
