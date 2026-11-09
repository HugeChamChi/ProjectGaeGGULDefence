using System;
using System.Collections.Generic;

/// <summary>
/// 선택지 추천 배지 판정 규칙(2026-11-09 사용자 확정). 규칙 변경은 이 파일 한 곳에서 한다.
/// 1. 지금까지 고른 카드의 테마 태그(자폭, 해킹, 글리치)를 센다. 공용은 세지 않고, 복수 태그는 각 태그에 1씩 더한다.
/// 2. 가장 많은 테마가 런 테마이며 동점이면 동점 테마 모두가 런 테마다.
/// 3. 테마 카드가 1장 이상이면 런 테마 태그를 하나라도 가진 제시 카드 전부를 추천한다. 공용 카드는 추천하지 않는다.
/// 4. 테마 카드가 0장이면 입문 추천 목록에 있는 제시 카드 전부를 추천한다.
/// 분류가 없는 카드는 세지도 추천하지도 않는다. 결과는 표시 전용이며 추첨 확률과 효과를 바꾸지 않는다.
/// </summary>
public static class ChoiceRecommendationRule
{
    /// <summary>테마로 세는 태그. 공용은 포함하지 않는다.</summary>
    public const DroneChoiceConcept ThemeMask =
        DroneChoiceConcept.SelfDestruct | DroneChoiceConcept.Glitch | DroneChoiceConcept.Hacking;

    private static readonly DroneChoiceConcept[] Themes =
        { DroneChoiceConcept.SelfDestruct, DroneChoiceConcept.Glitch, DroneChoiceConcept.Hacking };

    /// <summary>ID로 분류 태그를 조회한다. 누락이면 false.</summary>
    public delegate bool TagLookup(int chooseId, out DroneChoiceConcept tags);

    /// <summary>지금까지 고른 카드로 런 테마를 계산한다. 테마 카드가 없으면 None.</summary>
    public static DroneChoiceConcept GetRunThemes(IEnumerable<int> chosenIds, TagLookup lookup)
    {
        if (chosenIds == null || lookup == null) return DroneChoiceConcept.None;
        var counts = new int[Themes.Length];
        foreach (int id in chosenIds)
        {
            if (!lookup(id, out var tags)) continue;
            for (int i = 0; i < Themes.Length; i++)
                if ((tags & Themes[i]) != 0) counts[i]++;
        }

        int max = 0;
        foreach (int count in counts) max = Math.Max(max, count);
        if (max == 0) return DroneChoiceConcept.None;

        var result = DroneChoiceConcept.None;
        for (int i = 0; i < Themes.Length; i++)
            if (counts[i] == max) result |= Themes[i];
        return result;
    }

    /// <summary>제시된 카드마다 추천 배지 여부를 같은 순서로 돌려준다.</summary>
    public static bool[] Evaluate(IReadOnlyList<int> offeredIds, IEnumerable<int> chosenIds, TagLookup lookup,
        Func<int, bool> isStarter)
    {
        if (offeredIds == null) return Array.Empty<bool>();
        var result = new bool[offeredIds.Count];
        if (lookup == null) return result;

        var runThemes = GetRunThemes(chosenIds, lookup);
        for (int i = 0; i < offeredIds.Count; i++)
        {
            if (runThemes == DroneChoiceConcept.None)
                result[i] = isStarter != null && isStarter(offeredIds[i]);
            else
                result[i] = lookup(offeredIds[i], out var tags) && (tags & runThemes & ThemeMask) != 0;
        }
        return result;
    }
}
