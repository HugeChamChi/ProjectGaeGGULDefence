/// <summary>글리치 원천 카드의 불변 설정과 이미 적립한 제물 공격력을 조회한다.</summary>
public interface IOverdriveEffectReader
{
    /// <summary>가속 운전의 기본 타이머 대비 추가 감소 비율.</summary>
    float ExtraTimeDrainRatio { get; }
    /// <summary>가속 운전의 공속 가산 비율.</summary>
    float AcceleratedAttackSpeedBonus { get; }
    /// <summary>제물의 자동 지출 초. 0이면 미획득.</summary>
    float SacrificeCostSeconds { get; }
    /// <summary>제물의 성공 지출당 공격력 증가율.</summary>
    float SacrificeAttackPerPayment { get; }
    /// <summary>이번 런의 이미 지불한 제물 공격력 증가율.</summary>
    float SacrificeAttackBonus { get; }
    /// <summary>시간 금고의 마지막 유예 초. 0이면 미획득.</summary>
    float TimeVaultSeconds { get; }
    /// <summary>과열 한 개당 필요한 초과 스택 수. 0이면 미획득.</summary>
    int OverheatOverflowPerStack { get; }
    /// <summary>과열 한 개의 시간 비용.</summary>
    float OverheatCostSeconds { get; }
    /// <summary>과열 스택의 피해 가중치.</summary>
    float OverheatDamageWeight { get; }
}
