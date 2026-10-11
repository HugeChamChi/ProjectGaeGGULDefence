using System.Globalization;
using UnityEngine;

/// <summary>용어 팝업 수치 줄을 SO에서 만든다. 선택지·버프가 적용되기 전의 기본값임을 문구에 드러낸다.</summary>
public static class TermFacts
{
    /// <summary>Uses the same authored source for optional body values and the separate basic-stat line.</summary>
    public static string FormatBody(string template, ScriptableObject source)
    {
        if (source is UnitData unit)
            template = template.Replace("{coefficient}", Number(unit.SelfDestructAttackCoefficient));
        if (source is DroneHackingData hacking)
            template = template.Replace("{capacity}", hacking.Capacity.ToString(CultureInfo.InvariantCulture));
        return template;
    }

    /// <summary>출처 SO가 FactKind와 맞지 않으면 빈 문자열.</summary>
    public static string Build(TermFactKind kind, ScriptableObject source)
    {
        switch (kind)
        {
            case TermFactKind.ChiefSkill when source is AlphanSkillData chief:
                return $"기본 드론 1기당 피해 {Number(chief.DamagePerDrone)} · 기본 쿨타임 {Number(chief.CooldownSeconds)}초";
            case TermFactKind.SelfDestruct when source is UnitData betan:
                return $"기본 폭발 피해 = 베탕 공격력 ×{Number(betan.SelfDestructAttackCoefficient)}";
            case TermFactKind.HackStack when source is DroneHackingData hacking:
                return $"보스 1마리당 최대 {hacking.Capacity}개";
            case TermFactKind.TimeRecovery when source is DisigmanData disigman:
                return $"디시그망 스킬 1회당 {PerTier(disigman.SecondsRecovered)}초 회복";
            default:
                return string.Empty;
        }
    }

    // 모든 등급이 같으면 한 값, 다르면 등급별로.
    private static string PerTier(PerTierFloat value)
    {
        if (value == null) return "?";
        if (Mathf.Approximately(value.normal, value.rare) && Mathf.Approximately(value.rare, value.epic) &&
            Mathf.Approximately(value.epic, value.legend))
            return "+" + Number(value.normal);
        return $"+{Number(value.normal)} / {Number(value.rare)} / {Number(value.epic)} / {Number(value.legend)}";
    }

    private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
