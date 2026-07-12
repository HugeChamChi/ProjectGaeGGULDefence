using UnityEngine;
using VContainer;
using System.Collections.Generic;

/// <summary>
/// #40 보스 처치 누적 버프 토템
///
/// 보스가 처치될 때마다 공격력 + 속도 버프를 전역 누적.
/// BossBase.OnAnyBossDied 정적 이벤트로 처치 감지.
///
/// TotemData 설정:
///   attackBuffAmount = 처치당 공격력 증가율 (예: 0.1 = 10%)
///   speedBuffAmount  = 처치당 속도 증가율  (예: 0.1 = 10%)
///   effectRange      = 시각 표시 범위 (상3+하3, TotemEditor)
/// </summary>
public class TotemBossKillStack : TotemBase
{

    private int   _killCount;
    private float _appliedAttack;
    private float _appliedSpeed;

    protected override void ApplyBuff()
    {
        _killCount     = 0;
        _appliedAttack = 0f;
        _appliedSpeed  = 0f;
        BossBase.OnAnyBossDied += OnBossKilled;
    }

    protected override void RemoveBuff()
    {
        BossBase.OnAnyBossDied -= OnBossKilled;

        if (_appliedAttack > 0f) _totemBuffManager.RemoveAttackBuff(_appliedAttack);
        if (_appliedSpeed  > 0f) _totemBuffManager.RemoveSpeedBuff(_appliedSpeed);

        _killCount     = 0;
        _appliedAttack = 0f;
        _appliedSpeed  = 0f;
    }

    private void OnBossKilled()
    {
        if (totemData == null) return;

        _killCount++;

        float attackAmount = totemData.GetSimpleAmount(TotemBuffKind.Attack);
        float speedAmount  = totemData.GetSimpleAmount(TotemBuffKind.Speed);

        if (attackAmount > 0f)
        {
            _totemBuffManager.AddAttackBuff(attackAmount);
            _appliedAttack += attackAmount;
        }
        if (speedAmount > 0f)
        {
            _totemBuffManager.AddSpeedBuff(speedAmount);
            _appliedSpeed += speedAmount;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[TotemBossKillStack] 보스 처치 x{_killCount} → 공격 {_appliedAttack:F0}% / 속도 {_appliedSpeed:F0}%");
#endif
    }

    public override List<GridCell> GetAffectedCells()
    {
        if (CurrentCell == null || totemData == null) return new List<GridCell>();
        return totemData.GetEffectCells(this, _gridManager);
    }

    public override void PaintAffectedCells()
    {
        if (CurrentCell == null || totemData == null) return;

        bool hasAtk = totemData.GetSimpleAmount(TotemBuffKind.Attack) > 0f;
        bool hasSpd = totemData.GetSimpleAmount(TotemBuffKind.Speed)  > 0f;

        foreach (var cell in totemData.GetEffectCells(this, _gridManager))
        {
            cell.SetBuffFlags(
                atk: hasAtk || cell.HasAttackBuff,
                spd: hasSpd || cell.HasSpeedBuff);
        }
    }
}
