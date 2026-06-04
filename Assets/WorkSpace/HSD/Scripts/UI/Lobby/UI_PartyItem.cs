using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_PartyItem : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private TextMeshProUGUI partyNameText;
    [SerializeField] private GameObject lockObject; // "To be continue..." 같은 미해금 상태를 덮어씌울 오브젝트
    [SerializeField] private GameObject highlightObject; // 선택되었을 때 강조 표시

    private PartyDataSO _partyData;
    private Action<PartyDataSO> _onSelect;

    public void Init(PartyDataSO partyData, Action<PartyDataSO> onSelect)
    {
        _partyData = partyData;
        _onSelect = onSelect;

        if (partyNameText != null)
        {
            partyNameText.text = _partyData.partyName;
        }

        if (_partyData.isUnlock)
        {
            lockObject.SetActive(false);
            selectButton.interactable = true;
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => _onSelect?.Invoke(_partyData));
        }
        else
        {
            lockObject.SetActive(true);
            selectButton.interactable = false;
        }

        SetSelected(false);
    }

    public void SetSelected(bool isSelected)
    {
        if (highlightObject != null)
        {
            highlightObject.SetActive(isSelected);
        }
    }

    public PartyDataSO GetPartyData() => _partyData;
}
