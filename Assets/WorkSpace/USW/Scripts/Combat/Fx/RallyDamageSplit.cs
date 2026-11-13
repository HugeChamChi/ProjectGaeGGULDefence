using System.Collections.Generic;

/// <summary>
/// 족장 집결 피해 숫자를 고정 개수 조각으로 나눈다 (TODO L40, 사용자 결정 2026-11-13: 4개).
/// 표시 전용이다. 피해는 한 번 계산되어 이미 들어갔고, 조각의 합은 항상 원래 합계와 같다.
/// 조각 크기 난수는 호출한 연출 쪽 난수를 쓰며 전투 난수(UnityEngine.Random)를 건드리지 않는다.
/// </summary>
public static class RallyDamageSplit
{
    private const float MinimumWeight = 0.5f;
    private const float MaximumWeight = 1.5f;

    /// <summary>
    /// total을 pieces개로 나눠 result에 채운다. 앞 조각은 합계 자릿수에 맞춰 반올림하고 마지막 조각이 나머지를 갖는다.
    /// 합계가 조각 수보다 작아 나눌 수 없으면 합계 하나만 넣는다.
    /// </summary>
    public static void Split(decimal total, int pieces, System.Random random, List<decimal> result)
    {
        result.Clear();
        if (total <= 0m) return;
        decimal unit = RoundingUnit(total);
        if (pieces <= 1 || random == null || total < unit * pieces) { result.Add(total); return; }

        var weights = new float[pieces];
        float weightSum = 0f;
        for (int i = 0; i < pieces; i++)
        {
            weights[i] = MinimumWeight + (float)random.NextDouble() * (MaximumWeight - MinimumWeight);
            weightSum += weights[i];
        }

        decimal assigned = 0m;
        for (int i = 0; i < pieces - 1; i++)
        {
            decimal share = total * (decimal)(weights[i] / weightSum);
            decimal piece = System.Math.Max(unit, System.Math.Round(share / unit, System.MidpointRounding.AwayFromZero) * unit);
            result.Add(piece);
            assigned += piece;
        }
        decimal last = total - assigned;
        // 반올림이 몰려 마지막 조각이 너무 작으면 가장 큰 조각에서 덜어 온다.
        while (last < unit)
        {
            int largest = 0;
            for (int i = 1; i < result.Count; i++) if (result[i] > result[largest]) largest = i;
            if (result[largest] <= unit) { result.Clear(); result.Add(total); return; }
            result[largest] -= unit;
            last += unit;
        }
        result.Add(last);
    }

    /// <summary>합계의 상위 세 자리까지만 남기는 반올림 단위. 14,000 → 100, 1,234 → 10, 85 → 1.</summary>
    private static decimal RoundingUnit(decimal total)
    {
        decimal unit = 1m;
        while (total >= unit * 1000m) unit *= 10m;
        return unit;
    }
}
