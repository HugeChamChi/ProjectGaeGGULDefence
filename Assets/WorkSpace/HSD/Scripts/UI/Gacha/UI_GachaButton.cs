using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class UI_GachaButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Button button;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color insufficientColor = Color.gray;
    int cost;

    public void Refresh(int cost, Action onClick)
    {
        this.cost = cost;
        int userDiamond = Player.PlayerData.Data.Diamond;
        bool isEnough = userDiamond >= cost;

        costText.text = $"{userDiamond} / {cost.ToString()}";
        costText.color = isEnough ? normalColor : insufficientColor;

        button.interactable = isEnough;
        button.onClick.RemoveAllListeners();
        if (isEnough)
        {
            button.onClick.AddListener(() => onClick?.Invoke());
        }
    }

    public void Refresh()
    {
        int userDiamond = Player.PlayerData.Data.Diamond;
        bool isEnough = userDiamond >= cost;

        costText.text = $"{userDiamond} / {cost.ToString()}";
        costText.color = isEnough ? normalColor : insufficientColor;
    }

    public void SetInteractable(bool interactable)
    {
        // 재화가 있을 때만 전체 잠금/해제에 반응하도록 함
        if (Player.PlayerData.Data.Diamond >= cost)
        {
            button.interactable = interactable;
        }
    }
}