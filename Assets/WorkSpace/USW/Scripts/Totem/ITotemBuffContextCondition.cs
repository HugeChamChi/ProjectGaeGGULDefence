/// <summary>
/// 버프 관리자의 전투 상태(보스 등장 경과 시간 등)가 필요한 조건.
/// ConditionalBuffFunction은 이 인터페이스를 구현한 조건에 대해 이 오버로드를 우선 호출한다.
/// </summary>
public interface ITotemBuffContextCondition : ITotemCondition
{
    /// <summary>버프 관리자 상태를 함께 받아 조건을 판정한다.</summary>
    bool IsMet(TotemBase totem, GridCell cell, TotemBuffManager buffManager);
}
