using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// IconButton에 부착하여 UI_MailboxPanel을 여는 스크립트입니다.
/// </summary>
public class UI_MailboxOpenButton : MonoBehaviour
{
    [SerializeField] private UI_MailboxPanel mailboxPanel;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button == null)
            _button = GetComponentInChildren<Button>();

        if (_button != null)
            _button.onClick.AddListener(OnButtonClick);
        else
            Debug.LogWarning($"[UI_MailboxOpenButton] Button 컴포넌트를 찾을 수 없습니다: {gameObject.name}");
    }

    public void OnButtonClick()
    {
        if (mailboxPanel != null)
            mailboxPanel.Open();
        else
            Debug.LogError($"[UI_MailboxOpenButton] mailboxPanel이 할당되지 않았습니다: {gameObject.name}");
    }
}
