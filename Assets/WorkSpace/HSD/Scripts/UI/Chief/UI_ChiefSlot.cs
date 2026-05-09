using UnityEngine;
using UnityEngine.UI;
using System;

public class UI_ChiefSlot : UI_SlotBase<ChiefData>
{
    [SerializeField] Image img_Icon;
    [SerializeField] GameObject obj_Selected;
    [SerializeField] Button btn_Select;

    private Action<ChiefData> _onClicked;

    private void Awake()
    {
        if (btn_Select != null)
            btn_Select.onClick.AddListener(OnBtnClick);
    }

    public void SetCallback(Action<ChiefData> onClicked)
    {
        _onClicked = onClicked;
    }

    protected override void OnBind()
    {
        if (img_Icon != null && _data != null)
            img_Icon.sprite = _data.Icon;
    }

    public override void SetSelected(bool isSelected)
    {
        if (obj_Selected != null && obj_Selected.activeSelf != isSelected)
            obj_Selected.SetActive(isSelected);
    }

    private void OnBtnClick()
    {
        _onClicked?.Invoke(_data);
    }
}
