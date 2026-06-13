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
}
