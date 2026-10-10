using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>풀·획득 조건·부족·등급 정책을 받아 추첨한다. 씬 조회나 효과 적용을 하지 않는다.</summary>
public sealed class LevelUpDrawer
{
    private readonly ISelectionRandom _random;
    /// <summary>제품은 기존 Unity 난수 공급을 사용하고 검사는 고정 공급을 주입한다.</summary>
    public LevelUpDrawer(ISelectionRandom random) => _random = random ?? throw new ArgumentNullException(nameof(random));
    /// <summary>등급 우선 추첨, 부족 필터, 무중복 슬롯 및 후보 부족 보충을 수행한다.</summary>
    public List<LevelUpData> Draw(IReadOnlyList<LevelUpData> pool, Func<int,bool> hasAcquired,
        HashSet<UnitTribe> presentTribes, LevelUpPoolData settings, int count, bool forceLegend)
    {
        if (pool == null) throw new ArgumentNullException(nameof(pool));
        if (hasAcquired == null) throw new ArgumentNullException(nameof(hasAcquired));
        if (presentTribes == null) throw new ArgumentNullException(nameof(presentTribes));
        if (count <= 0) return new List<LevelUpData>();
        var filtered = new List<LevelUpData>();

        foreach (var data in pool)
        {
            if (data != null && data.spawnRate > 0f && !float.IsInfinity(data.spawnRate)
                && (data.tier == Tier.Rare || data.tier == Tier.Epic || data.tier == Tier.Legend)
                && (!forceLegend || data.tier == Tier.Legend)
                && !hasAcquired(data.chooseId) && IsApplicable(data, presentTribes))
                filtered.Add(data);
        }

        var tierGroups = new Dictionary<Tier, List<LevelUpData>>();
        var tierWeights = new Dictionary<Tier, float>();

        foreach (var d in filtered)
        {
            if (!tierGroups.ContainsKey(d.tier))
            {
                tierGroups[d.tier] = new List<LevelUpData>();
                tierWeights[d.tier] = 0f;
            }
            tierGroups[d.tier].Add(d);
            tierWeights[d.tier] = forceLegend ? 1f : GetTierWeight(d.tier, settings);
        }

        if (tierGroups.Count == 0) return new List<LevelUpData>();

        // 1. 등급(Tier) 추첨
        float totalWeight = 0f;
        foreach (var w in tierWeights.Values) totalWeight += w;

        if (totalWeight <= 0f) return new List<LevelUpData>();
        float roll = _random.Range(0f, totalWeight);
        float cumul = 0f;
        Tier selectedTier = Tier.Rare;

        foreach (var kvp in tierWeights)
        {
            if (kvp.Value <= 0f) continue;
            selectedTier = kvp.Key;
            cumul += kvp.Value;
            if (kvp.Value > 0f && roll < cumul)
            {
                selectedTier = kvp.Key;
                break;
            }
        }

        // 2. 선택된 등급 내에서 슬롯 뽑기
        var group = tierGroups[selectedTier];
        var result = new List<LevelUpData>();
        int pickCount = Mathf.Min(count, group.Count);

        for (int i = 0; i < pickCount; i++)
        {
            int idx = _random.Range(0, group.Count);
            result.Add(group[idx]);
            group.RemoveAt(idx);
        }

        // 등급 내 갯수가 모자라면 다른 등급에서라도 채움
        filtered.RemoveAll(x => result.Contains(x));
        while (result.Count < count && filtered.Count > 0)
        {
            int idx = _random.Range(0, filtered.Count);
            result.Add(filtered[idx]);
            filtered.RemoveAt(idx);
        }

        return result;
    }

    private static float GetTierWeight(Tier tier, LevelUpPoolData settings)
    {
        // SO 미연결 검사용 기본값 역시 실제 풀 SO의 기본 비율과 같다.
        float weight = tier switch
        {
            Tier.Rare => settings != null ? settings.RareWeight : LevelUpPoolData.DefaultRareWeight,
            Tier.Epic => settings != null ? settings.EpicWeight : LevelUpPoolData.DefaultEpicWeight,
            Tier.Legend => settings != null ? settings.LegendWeight : LevelUpPoolData.DefaultLegendWeight,
            _ => 0f
        };
        return float.IsNaN(weight) || float.IsInfinity(weight) ? 0f : Mathf.Max(0f, weight);
    }

    private static bool IsApplicable(LevelUpData data, HashSet<UnitTribe> presentTribes)
    {
        if (data.applicableTribes == null || data.applicableTribes.Length == 0) return true;
        foreach (var t in data.applicableTribes)
            if (presentTribes.Contains(t)) return true;
        return false;
    }

}
