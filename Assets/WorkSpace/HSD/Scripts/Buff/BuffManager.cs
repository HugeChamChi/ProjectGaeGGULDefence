using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>
/// 전역 버프 매니저 — TotemBuffManager와 동급의 VContainer 등록 서비스.
/// 글로벌 버프를 별도 float 필드로 관리하지 않고, GridManager에 배치된 모든 유닛의
/// BuffController에 동일한 BuffData를 적용/해제하는 방식으로 구현한다.
/// </summary>
public class BuffManager : MonoBehaviour
{
    [Inject] private IObjectResolver _resolver;

    private GridManager _gridManager;

    // "지금 켜져있는 글로벌 버프 정의"만 기억한다 — 별도의 유닛 레지스트리는 만들지 않고
    // 적용 대상은 항상 GridManager.AllCells()에서 조회한다.
    private readonly List<(BuffData data, UnitBase source)> _activeGlobalBuffs = new();

    public void Init()
    {
        if (_gridManager == null) _gridManager = _resolver.Resolve<GridManager>();
    }

    public void ApplyGlobalBuff(BuffData data, UnitBase source = null)
    {
        if (data == null || _gridManager == null) return;

        _activeGlobalBuffs.Add((data, source));

        foreach (var cell in _gridManager.AllCells())
            cell?.OccupyingUnit?.Buffs?.ApplyBuff(data, source);
    }

    public void RemoveGlobalBuff(BuffData data)
    {
        if (data == null || _gridManager == null) return;

        _activeGlobalBuffs.RemoveAll(b => b.data == data);

        foreach (var cell in _gridManager.AllCells())
            cell?.OccupyingUnit?.Buffs?.RemoveBuff(data);
    }

    /// <summary>새 유닛 스폰 시(UnitBase.Init()) 현재 활성 글로벌 버프를 그 유닛에게도 적용한다.</summary>
    public void ApplyActiveGlobalBuffsTo(UnitBase unit)
    {
        if (unit == null || unit.Buffs == null) return;

        foreach (var (data, source) in _activeGlobalBuffs)
            unit.Buffs.ApplyBuff(data, source);
    }
}
