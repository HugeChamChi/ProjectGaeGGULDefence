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
        private UI_IngameDebugPanel _panel;

        /// <summary>해당 씬의 디버그 패널을 주입받는다.</summary>
        [VContainer.Inject]
        public void Construct(System.Collections.Generic.IEnumerable<UI_IngameDebugPanel> panels)
        {
            foreach (var panel in panels) { _panel = panel; break; }
        }
        private void Start()
        {
            var btn = GetComponent<Button>();
            btn.onClick.AddListener(OpenDebugPanel);
        }

        private void OpenDebugPanel()
        {
            if (_panel != null)
            {
                _panel.Open();
            }
            else
            {
                Debug.LogWarning("[UI_OpenDebugButton] 해당 씬의 UI_IngameDebugPanel이 등록되지 않았습니다.");
            }
        }
    }
}
