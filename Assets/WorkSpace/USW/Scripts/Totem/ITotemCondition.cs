/// <summary>
/// 조건부 버프(ConditionalBuffFunction)가 참조하는 조건 판정 인터페이스.
/// </summary>
public interface ITotemCondition
{
    bool IsMet(TotemBase totem, GridCell cell);
}
