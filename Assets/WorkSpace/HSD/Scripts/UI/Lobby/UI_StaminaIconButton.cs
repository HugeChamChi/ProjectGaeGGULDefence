using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상단 스태미나 아이콘 클릭 시 StaminaInsufficientPopup을 상태 확인/즉시회복 용도로 엽니다.
/// </summary>
public class UI_StaminaIconButton : MonoBehaviour
{
    [SerializeField] private StaminaInsufficientPopup staminaPopup;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button == null)
            _button = GetComponentInChildren<Button>();

        if (_button != null)
            _button.onClick.AddListener(OnButtonClick);
        else
            Debug.LogWarning($"[UI_StaminaIconButton] Button 컴포넌트를 찾을 수 없습니다: {gameObject.name}");
    }

    private void OnButtonClick()
    {
        if (staminaPopup != null)
            staminaPopup.ShowStatus();
        else
            Debug.LogError($"[UI_StaminaIconButton] staminaPopup이 할당되지 않았습니다: {gameObject.name}");
    }
}
