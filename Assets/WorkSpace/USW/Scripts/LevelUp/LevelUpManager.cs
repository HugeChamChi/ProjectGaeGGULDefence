using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 레벨업 효과 관리
///
/// Inspector:
///   levelUpPool — 모든 LevelUpData 에셋 할당
///
/// 전방/후방 기준:
///   전방 = GridPosition.y 가 0, 1 (그리드 앞쪽 두 줄)
///   후방 = GridPosition.y 가 gridRows-2, gridRows-1 (그리드 뒤쪽 두 줄)
/// </summary>
public class LevelUpManager : InGameSingleton<LevelUpManager>
{
    [SerializeField] private LevelUpData[] levelUpPool;

    // ── 누적 스탯 ──────────────────────────────────────────────
    public float CritChance          { get; private set; } = 0f;   // 0~1
    public float CritDamageMultiplier { get; private set; } = 1.5f; // 기본 치명타 배율

    // 줄별 공격력 배율 (인덱스 = GridPosition.y)
    private readonly float[] _rowAttackMult  = { 1f, 1f, 1f, 1f };
    // 줄별 속도 배율 — 1보다 클수록 게이지 빠름 (interval에 나눔)
    private readonly float[] _rowSpeedMult   = { 1f, 1f, 1f, 1f };

    // ── 줄별 배율 조회 ─────────────────────────────────────────

    public float GetRowAttackMultiplier(int row)
    {
        if (row < 0 || row >= _rowAttackMult.Length) return 1f;
        return _rowAttackMult[row];
    }

    public float GetRowSpeedMultiplier(int row)
    {
        if (row < 0 || row >= _rowSpeedMult.Length) return 1f;
        return _rowSpeedMult[row];
    }

    // ── 랜덤 선택지 3개 뽑기 ──────────────────────────────────

    public List<LevelUpData> GetRandomChoices(int count = 3)
    {
        if (levelUpPool == null || levelUpPool.Length == 0)
        {
            Debug.LogError("LevelUpManager: levelUpPool이 비어있습니다.");
            return new List<LevelUpData>();
        }

        var pool = new List<LevelUpData>(levelUpPool);
        var result = new List<LevelUpData>();

        count = Mathf.Min(count, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        return result;
    }

    // ── 효과 적용 ──────────────────────────────────────────────

    public void ApplyEffect(LevelUpData data)
    {
        float v = data.value / 100f;
        int rows = Manager.Grid != null ? Manager.Grid.Rows : 4;

        switch (data.effectType)
        {
            case LevelUpEffectType.AttackPercent:
                Manager.Buff.AddAttackBuff(v);
                break;

            case LevelUpEffectType.AttackSpeedPercent:
                Manager.Buff.AddSpeedBuff(v);
                break;

            case LevelUpEffectType.TotemEfficiencyPercent:
                Manager.Buff.AddTotemEfficiency(v);
                break;

            case LevelUpEffectType.CritChancePercent:
                CritChance = Mathf.Clamp01(CritChance + v);
                break;

            case LevelUpEffectType.CritDamagePercent:
                CritDamageMultiplier += v;
                break;

            case LevelUpEffectType.FoodSpeedPercent:
                Manager.Buff.AddFoodSpeedBuff(v);
                break;

            case LevelUpEffectType.FrontRowAttackPercent:
                _rowAttackMult[0] += v;
                if (rows > 1) _rowAttackMult[1] += v;
                break;

            case LevelUpEffectType.BackRowAttackPercent:
                if (rows >= 2) _rowAttackMult[rows - 1] += v;
                if (rows >= 3) _rowAttackMult[rows - 2] += v;
                break;

            case LevelUpEffectType.FrontRowSpeedPercent:
                // interval에 나누므로 (1 + v)를 곱해서 빠르게
                _rowSpeedMult[0] *= (1f + v);
                if (rows > 1) _rowSpeedMult[1] *= (1f + v);
                break;

            case LevelUpEffectType.BackRowSpeedPercent:
                if (rows >= 2) _rowSpeedMult[rows - 1] *= (1f + v);
                if (rows >= 3) _rowSpeedMult[rows - 2] *= (1f + v);
                break;
        }

        Debug.Log($"[LevelUp] 효과 적용: {data.description}");
    }
}
