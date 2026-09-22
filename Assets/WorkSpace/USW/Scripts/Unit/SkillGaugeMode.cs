/// <summary>유닛의 발밑 스킬 게이지 표시 정책입니다. 전투 동작은 변경하지 않습니다.</summary>
public enum SkillGaugeMode
{
    /// <summary>스킬 사용 가능 여부와 SO의 현재 등급 쿨다운으로 자동 판정합니다.</summary>
    Automatic = 0,
    /// <summary>패시브 전용 유닛: 동일한 프레임에 비활성 회색 바를 표시합니다.</summary>
    PassiveOnly = 1
}
