using UnityEngine;
using UnityEngine.UI;
using System;

public class UI_ChiefSlot : MonoBehaviour
{
    [SerializeField] Image img_Icon;
    [SerializeField] GameObject obj_Selected;
    [SerializeField] Button btn_Select;

    private ChiefData _data;
    private Action<ChiefData> _onClicked;

    public ChiefData Data => _data;

    private void Awake()
    {
        // 람다 대신 직접 함수를 연결하여 GC Alloc 방지
        if (btn_Select != null)
            btn_Select.onClick.AddListener(OnBtnClick);
    }

    public void SetData(ChiefData data, Action<ChiefData> onClicked)
    {
        _data = data;
        _onClicked = onClicked;
        
        if (img_Icon != null)
            img_Icon.sprite = data.Icon;
    }

    public void SetSelected(bool isSelected)
    {
        if (obj_Selected != null && obj_Selected.activeSelf != isSelected)
            obj_Selected.SetActive(isSelected);
    }

    private void OnBtnClick()
    {
        _onClicked?.Invoke(_data);
    }
}
