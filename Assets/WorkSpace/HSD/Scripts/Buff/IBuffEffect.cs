/// <summary>
/// 버프가 대상 유닛에 미치는 실제 효과. BuffController가 적용/스택 변경/제거 시점마다 호출한다.
/// </summary>
public interface IBuffEffect
{
    void OnApply(BuffInstance instance, UnitBase target);
    void OnStackChanged(BuffInstance instance, UnitBase target);
    void OnRemove(BuffInstance instance, UnitBase target);
}
