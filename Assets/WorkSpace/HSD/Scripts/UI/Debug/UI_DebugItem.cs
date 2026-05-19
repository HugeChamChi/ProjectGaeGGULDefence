using System;
using UnityEngine;
using UnityEngine.UI;

public class UI_DebugItem : MonoBehaviour
{
    [SerializeField] private Text txt_Name;
    [SerializeField] private Button btn_Action;
    [SerializeField] private Text txt_ActionLabel;

    private Action _onActionClicked;

    public void Setup(string itemName, string actionName, Action onActionClicked)
    {
        if (txt_Name != null) txt_Name.text = itemName;
        if (txt_ActionLabel != null) txt_ActionLabel.text = actionName;
        
        _onActionClicked = onActionClicked;
        
        if (btn_Action != null)
        {
            btn_Action.onClick.RemoveAllListeners();
            btn_Action.onClick.AddListener(OnBtnClicked);
        }
    }

    private void OnBtnClicked()
    {
        _onActionClicked?.Invoke();
    }
}
