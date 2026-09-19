using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// characterId를 유닛별 독립 강화 키에 연결하는 SO.
/// 미등록 유닛은 강화 보정을 받지 않는다. 비용과 효과는 UpgradeSettings에서 관리한다.
/// </summary>
[CreateAssetMenu(fileName = "UpgradeKeyOverrides", menuName = "Game/UpgradeKeyOverrides")]
public class UpgradeKeyOverrides : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public int characterId;
        public string upgradeKey;
    }

    [SerializeField] private List<Entry> overrides = new List<Entry>();

    public bool TryGetOverride(int characterId, out string upgradeKey)
    {
        foreach (var entry in overrides)
        {
            if (entry.characterId == characterId)
            {
                upgradeKey = entry.upgradeKey;
                return true;
            }
        }
        upgradeKey = null;
        return false;
    }
}
