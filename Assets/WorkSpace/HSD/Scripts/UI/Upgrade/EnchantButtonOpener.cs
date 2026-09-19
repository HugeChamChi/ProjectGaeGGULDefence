using UnityEngine;
using UnityEngine.UI;
using HSD.UI.Upgrade;

/// <summary>
/// Enchant 버튼 클릭 시 강화 패널(UI_UpgradePanel)을 연다. 패널이 평소 비활성 상태라
/// GameObject.Find로는 못 찾으므로, 비활성 오브젝트도 포함해서 씬에서 찾아 연결한다
/// (인스펙터 참조 연결 불필요).
/// </summary>
[RequireComponent(typeof(Button))]
public class EnchantButtonOpener : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OpenUpgradePanel);
    }

    private void OpenUpgradePanel()
    {
        var panel = FindPanel();
        if (panel == null)
        {
            Debug.LogWarning("[EnchantButtonOpener] UI_UpgradePanel을 씬에서 찾을 수 없습니다.");
            return;
        }
        panel.gameObject.SetActive(true);
        panel.Open();
    }

    private static UI_UpgradePanel FindPanel()
    {
        var panels = Object.FindObjectsByType<UI_UpgradePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return panels.Length > 0 ? panels[0] : null;
    }
}
