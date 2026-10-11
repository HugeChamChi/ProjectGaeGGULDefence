/// <summary>용어 팝업의 수치 한 줄을 어느 SO에서 어떻게 읽을지. 수치는 전투와 같은 SO에서 읽는다(SO 우선 원칙).</summary>
public enum TermFactKind
{
    /// <summary>수치 줄 없음.</summary>
    None = 0,
    /// <summary>족장 스킬 — <see cref="AlphanSkillData"/>의 드론 1기당 피해·쿨타임 (기본값).</summary>
    ChiefSkill = 1,
    /// <summary>자폭 드론 — 베탕 <see cref="UnitData"/>의 자폭 공격력 계수.</summary>
    SelfDestruct = 2,
    /// <summary>해킹스택 — <see cref="DroneHackingData"/>의 보스당 상한.</summary>
    HackStack = 3,
    /// <summary>시간조작 — <see cref="DisigmanData"/>의 스킬당 회복 초.</summary>
    TimeRecovery = 4,
}
