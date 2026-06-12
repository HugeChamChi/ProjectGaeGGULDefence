using UnityEngine;

public static class DamageCalculator
{
    /// <summary>
    /// 최종 데미지 계산 시 소수점 처리 방식을 결정합니다.
    /// 현재는 반올림(Round)으로 적용되어 있으며, 추후 기획 변경 시 이곳에서 일괄 수정할 수 있습니다.
    /// </summary>
    public static int ApplyRounding(float rawDamage)
    {
        return Mathf.RoundToInt(rawDamage);
        // return Mathf.FloorToInt(rawDamage); // 버림
        // return Mathf.CeilToInt(rawDamage);  // 올림
    }
}
