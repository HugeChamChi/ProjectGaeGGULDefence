using TMPro;
using UnityEngine;

/// <summary>
/// 가챠 확률 정보를 한 줄로 표시하는 개별 슬롯 클래스입니다.
/// </summary>
public class UI_GachaChanceSlot : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rarityText;
    [SerializeField] private TextMeshProUGUI percentText;

    public void Setup(string rarity, double percent)
    {
        if (rarityText != null) rarityText.text = rarity;
        if (percentText != null) percentText.text = $"{percent:F2}%"; // 소수점 2자리까지 표시
    }
}
