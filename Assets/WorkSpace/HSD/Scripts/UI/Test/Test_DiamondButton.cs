using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Test_DiamondButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private int amount = 1000;

    private void Awake()
    {
        button = GetComponent<Button>();

        button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        Player.PlayerData.AddDiamond(amount);
    }
}
