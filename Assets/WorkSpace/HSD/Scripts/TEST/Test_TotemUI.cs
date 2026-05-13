using UnityEngine;
using GaeGGUL.UI.Totem;

namespace GaeGGUL.Test
{
    /// <summary>
    /// 토템 UI 기능을 테스트하기 위한 스크립트입니다.
    /// 인스펙터에서 TotemData를 할당하고 버튼을 눌러 테스트할 수 있습니다.
    /// </summary>
    public class Test_TotemUI : MonoBehaviour
    {
        [Header("Target UI")]
        [SerializeField] private UI_TotemInfoPanel totemInfoPanel;

        [Header("Test Data")]
        [SerializeField] private TotemData testData;

        private void Start()
        {
            if (totemInfoPanel == null)
            {
                totemInfoPanel = FindFirstObjectByType<UI_TotemInfoPanel>();
            }
        }

        [Button("Apply Test Data")]
        public void ApplyTestData()
        {
            if (totemInfoPanel == null)
            {
                Debug.LogError("UI_TotemInfoPanel이 할당되지 않았습니다.");
                return;
            }

            if (testData == null)
            {
                Debug.LogError("테스트할 TotemData가 할당되지 않았습니다.");
                return;
            }

            totemInfoPanel.SetData(testData);
            Debug.Log($"<color=cyan>Test_TotemUI</color>: Applied {testData.totemName} to UI.");
        }
    }
}
