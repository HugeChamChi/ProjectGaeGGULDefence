using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class UI_PlayerProfilePanel : UI_Base
{
    [SerializeField] UI_ProfilePanel ui_ProfilePanel;

    public override async UniTask OpenAsync()
    {
        gameObject.SetActive(true);
        ui_ProfilePanel.Open();
    }

    public override async UniTask CloseAsync()
    {
        gameObject.SetActive(false);
        ui_ProfilePanel.Close();
    }
}
