using UnityEngine;
using GaeGGUL.UI.Unit;

namespace GaeGGUL.Test
{
    /// <summary>
    /// 유닛 정보 UI 기능을 테스트하기 위한 스크립트입니다.
    /// 인스펙터에서 UnitData를 할당하고 버튼을 눌러 테스트할 수 있습니다.
    /// </summary>
    public class Test_UnitInfoUI : MonoBehaviour
    {
        [Header("Target UI")]
        [SerializeField] private UI_UnitInfoPanel unitInfoPanel;

        [Header("Test Data")]
        [SerializeField] private UnitData testData;

        private void Start()
        {
            if (unitInfoPanel == null)
            {
                unitInfoPanel = FindFirstObjectByType<UI_UnitInfoPanel>();
            }
        }

        [Button("Apply Unit Test Data")]
        public void ApplyTestData()
        {
            if (unitInfoPanel == null)
            {
                Debug.LogError("UI_UnitInfoPanel이 할당되지 않았습니다.");
                return;
            }

            if (testData == null)
            {
                Debug.LogError("테스트할 UnitData가 할당되지 않았습니다.");
                return;
            }

            unitInfoPanel.SetData(testData);
            Debug.Log($"<color=green>Test_UnitInfoUI</color>: Applied {testData.unitName} to UI.");
        }
    }
}
