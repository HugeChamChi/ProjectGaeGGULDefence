using System;

/// <summary>
/// 스택 1당 kind 스탯에 amountPerStack만큼 배율을 누산하는 효과.
/// 실제 수치 집계는 BuffController.GetStatMultiplier가 활성 버프 목록을 스캔해 계산하므로
/// (TotemBuffManager의 dirty flag 캐시와 동일한 철학), 여기서는 별도 처리가 필요 없다.
/// </summary>
[Serializable]
public class StatModifierBuffEffect : IBuffEffect
{
    public StatKind kind;
    public float amountPerStack;

    public void OnApply(BuffInstance instance, UnitBase target) { }
    public void OnStackChanged(BuffInstance instance, UnitBase target) { }
    public void OnRemove(BuffInstance instance, UnitBase target) { }
}
