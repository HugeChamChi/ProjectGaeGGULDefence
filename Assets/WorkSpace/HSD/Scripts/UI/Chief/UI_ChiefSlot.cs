using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class UI_ChiefSlot : MonoBehaviour
{
    [SerializeField] Image img_Icon;
    [SerializeField] GameObject obj_Selected;
    [SerializeField] Button btn_Select;

    private ChiefData _data;
    private Action<ChiefData> _onClicked;

    public ChiefData Data => _data;

    public void SetData(ChiefData data, Action<ChiefData> onClicked)
    {
        _data = data;
        _onClicked = onClicked;
        
        if (img_Icon != null)
            img_Icon.sprite = data.Icon;
            
        btn_Select.onClick.RemoveAllListeners();
        btn_Select.onClick.AddListener(() => _onClicked?.Invoke(_data));
    }

    public void SetSelected(bool isSelected)
    {
        if (obj_Selected != null)
            obj_Selected.SetActive(isSelected);
    }
}
