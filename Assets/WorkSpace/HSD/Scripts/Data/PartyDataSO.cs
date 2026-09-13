using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PartyData", menuName = "Game/PartyData")]
public class PartyDataSO : ScriptableObject
{
    public string partyName;
    public bool isUnlock = true;
    public List<UnitData> unitDataList;

    [Header("족장 데이터")]
    public UnitData chieftainData;

    [HideInInspector] // 이전 자산 이관용으로 보존. 런타임에서는 더 이상 합산하지 않는다.
    public List<LevelUpData> exclusiveLevelUpChoices;
}
