using System;
using GGD.BackendCharts;

/// <summary>
/// 스킬 데이터의 숫자 칸에 끼우는 값. SkillBalance 차트의 <see cref="key"/> 값을 돌려주고,
/// 튜토리얼과 에디터 직접 실행에서는 아래 내장 값을 돌려준다 (TODO G12). 키는 <see cref="SkillBalance"/>에 등록된 등급별 키여야 한다.
/// </summary>
[Serializable]
[DisplayName("차트값(SkillBalance)")]
public class SkillBalanceFloat : IScaledFloat
{
    public string key;
    public float normal;
    public float rare;
    public float epic;
    public float legend;

    public float Get(Tier tier) => SkillBalance.Tiered(key, tier, Bundled(tier));

    /// <summary>앱 내장 값. 엑셀 반영 도구가 쓴다.</summary>
    public float Bundled(Tier tier) => tier switch
    {
        Tier.Rare => rare,
        Tier.Epic => epic,
        Tier.Legend => legend,
        _ => normal,
    };
}
