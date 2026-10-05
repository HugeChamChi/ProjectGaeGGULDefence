using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PartyData", menuName = "Game/PartyData")]
public class PartyDataSO : ScriptableObject
{
    public string partyName;
    public bool isUnlock = true;
    public List<UnitData> unitDataList;

    [Header("족장 데이터")]
    /// <summary>Deck card pool and independent chief skill, separate from combat units.</summary>
    public ChieftainData Chieftain;

    [HideInInspector] // 이전 자산 이관용으로 보존. 런타임에서는 더 이상 합산하지 않는다.
    public List<LevelUpData> exclusiveLevelUpChoices;
}
