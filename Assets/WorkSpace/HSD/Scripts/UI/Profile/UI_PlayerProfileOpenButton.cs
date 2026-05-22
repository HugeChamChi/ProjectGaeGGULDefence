using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// IconButton에 부착하여 UI_PlayerProfilePanel을 여는 스크립트입니다.
/// </summary>
public class UI_PlayerProfileOpenButton : MonoBehaviour
{
    [Header("Target UI")]
    [SerializeField] private UI_PlayerProfilePanel profilePanel;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        
        // 현재 오브젝트에 Button이 없다면 자식 오브젝트에서 검색합니다.
        if (_button == null)
            _button = GetComponentInChildren<Button>();

        if (_button != null)
        {
            _button.onClick.AddListener(OnButtonClick);
        }
        else
        {
            Debug.LogWarning($"[UI_PlayerProfileOpenButton] Button 컴포넌트를 찾을 수 없습니다: {gameObject.name}");
        }
    }

    [Button]
    public void OnButtonClick()
    {
        if (profilePanel != null)
        {
            profilePanel.Open();
        }
        else
        {
            Debug.LogError($"[UI_PlayerProfileOpenButton] profilePanel이 할당되지 않았습니다: {gameObject.name}");
        }
    }
}
