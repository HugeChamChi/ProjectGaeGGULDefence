using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PartyLobbyData", menuName = "Game/PartyLobbyData")]
public class PartyLobbyDataSO : ScriptableObject
{
    public List<PartyDataSO> partyList;
}
