using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_DynamicPanel : UI_Base
{
    [SerializeField] Button closeButton;

    private void OnEnable()
    {
        closeButton.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        closeButton.onClick.RemoveAllListeners();
    }
}
