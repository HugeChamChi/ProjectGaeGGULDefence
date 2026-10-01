using UnityEngine;
using UnityEngine.UI;
using HSD.UI.Upgrade;

/// <summary>
/// Enchant 버튼 클릭 시 강화 패널(UI_UpgradePanel)을 연다. 패널이 평소 비활성 상태라
/// 해당 씬의 LifetimeScope가 비활성 패널을 등록하여 주입한다.
/// </summary>
[RequireComponent(typeof(Button))]
public class EnchantButtonOpener : MonoBehaviour
{
    private UI_UpgradePanel _panel;

    /// <summary>씬에 등록된 강화 패널을 주입받는다.</summary>
    [VContainer.Inject]
    public void Construct(System.Collections.Generic.IEnumerable<UI_UpgradePanel> panels)
    {
        foreach (var panel in panels) { _panel = panel; break; }
    }
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OpenUpgradePanel);
    }

    private void OpenUpgradePanel()
    {
        var panel = _panel;
        if (panel == null)
        {
            Debug.LogWarning("[EnchantButtonOpener] UI_UpgradePanel을 씬에서 찾을 수 없습니다.");
            return;
        }
        panel.gameObject.SetActive(true);
        panel.Open();
    }

}
