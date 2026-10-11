using System;
using UnityEngine;

/// <summary>
/// 현재 보스가 등장한 뒤 지정 시간 안에만 참이 되는 조건.
/// 경과 시간은 TotemBuffManager가 전투 진행 중에만 센다(일시정지·선택 유예 중 정지).
/// 조건부 버프에 넣으면 스탯·수치·범위만 바꿔 여러 토템이 재사용할 수 있다.
/// </summary>
[Serializable]
[DisplayName("보스 등장 후 N초")]
public class BossEncounterWindowCondition : ITotemBuffContextCondition
{
    /// <summary>보스 등장 후 조건이 유지되는 시간(초).</summary>
    [Min(0.1f)] public float seconds = 7f;

    /// <inheritdoc />
    public bool IsMet(TotemBase totem, GridCell cell) => false;

    /// <inheritdoc />
    public bool IsMet(TotemBase totem, GridCell cell, TotemBuffManager buffManager)
        => buffManager != null && buffManager.IsWithinBossEncounter(seconds);
}
