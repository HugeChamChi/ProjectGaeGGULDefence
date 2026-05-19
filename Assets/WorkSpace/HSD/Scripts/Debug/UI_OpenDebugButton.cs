using UnityEngine;
using UnityEngine.UI;

namespace HSD.InGameDebug
{
    /// <summary>
    /// 버튼에 부착하여 UI_IngameDebugPanel을 엽니다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UI_OpenDebugButton : MonoBehaviour
    {
        private void Start()
        {
            var btn = GetComponent<Button>();
            btn.onClick.AddListener(OpenDebugPanel);
        }

        private void OpenDebugPanel()
        {
            if (UI_IngameDebugPanel.Instance != null)
            {
                UI_IngameDebugPanel.Instance.Open();
            }
            else
            {
                // 인스턴스가 없는 경우 씬에서 직접 찾기 시도 (비활성화 된 경우 포함)
                var panel = Object.FindAnyObjectByType<UI_IngameDebugPanel>(FindObjectsInactive.Include);
                if (panel != null)
                {
                    panel.Open();
                }
                else
                {
                    Debug.LogWarning("[UI_OpenDebugButton] UI_IngameDebugPanel을 씬에서 찾을 수 없습니다.");
                }
            }
        }
    }
}