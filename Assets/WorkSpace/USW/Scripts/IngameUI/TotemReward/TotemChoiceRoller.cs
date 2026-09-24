using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 처치 보상 토템 선택지 뽑기 (TotemSelectUI · TotemRewardUI 공용).
/// 등급 하나를 후보 수에 비례해 고른 뒤 그 등급에서 먼저 뽑고, 모자라면 나머지 후보에서 채운다.
/// 이미 고른 토템(exclude)은 다시 나오지 않는다.
/// </summary>
public static class TotemChoiceRoller
{
    /// <summary>풀에서 exclude를 뺀 후보 중 최대 count개를 뽑는다. 풀이 비면 빈 목록.</summary>
    public static List<TotemData> Roll(IReadOnlyList<TotemData> pool, ICollection<int> exclude, int count)
    {
        var result = new List<TotemData>();
        if (pool == null || pool.Count == 0) return result;

        var filtered = new List<TotemData>();
        foreach (var data in pool)
        {
            if (data != null && (exclude == null || !exclude.Contains(data.totemId)))
                filtered.Add(data);
        }

        var tierGroups = new Dictionary<Tier, List<TotemData>>();
        foreach (var d in filtered)
        {
            if (!tierGroups.ContainsKey(d.tier)) tierGroups[d.tier] = new List<TotemData>();
            tierGroups[d.tier].Add(d);
        }

        if (tierGroups.Count == 0) return result;

        float roll = Random.Range(0f, filtered.Count);
        float cumul = 0f;
        Tier selectedTier = Tier.Normal;

        foreach (var kvp in tierGroups)
        {
            cumul += kvp.Value.Count;
            if (roll <= cumul)
            {
                selectedTier = kvp.Key;
                break;
            }
        }

        var group = tierGroups[selectedTier];
        int pickCount = Mathf.Min(count, group.Count);

        for (int i = 0; i < pickCount; i++)
        {
            int idx = Random.Range(0, group.Count);
            result.Add(group[idx]);
            group.RemoveAt(idx);
        }

        filtered.RemoveAll(x => result.Contains(x));
        while (result.Count < count && filtered.Count > 0)
        {
            int idx = Random.Range(0, filtered.Count);
            result.Add(filtered[idx]);
            filtered.RemoveAt(idx);
        }

        return result;
    }
}
