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

    [Header("파티 전용 레벨업 선택지 (이 파티를 선택했을 때만 풀에 추가됨)")]
    public List<LevelUpData> exclusiveLevelUpChoices;
}
