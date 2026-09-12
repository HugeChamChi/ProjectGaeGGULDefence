using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>Debuff 시트와 부여자 FK 헤더 파싱. 잘못된 행은 명시적으로 거부한다.</summary>
public static class DebuffSheetParser
{
    /// <summary>컬럼 순서에 의존하지 않는 이름 조회.</summary>
    public static int Column(string[] headers, string name)
    {
        int found = -1;
        for (int i = 0; i < headers.Length; i++)
            if (string.Equals(headers[i].Trim().TrimStart('\uFEFF'), name, StringComparison.OrdinalIgnoreCase))
            {
                if (found >= 0) throw new FormatException($"Duplicate column: {name}");
                found = i;
            }
        return found;
    }
    private static string Value(string[] headers, string[] row, string name)
    {
        int index = Column(headers, name);
        if (index < 0) throw new FormatException($"Missing column: {name}");
        return index < row.Length ? row[index].Trim() : "";
    }
    private static decimal Number(string raw) => decimal.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static T Named<T>(string raw) where T : struct
    {
        foreach (string name in Enum.GetNames(typeof(T)))
            if (name == raw) return (T)Enum.Parse(typeof(T), raw);
        throw new FormatException($"Unsupported {typeof(T).Name}: {raw}");
    }
    /// <summary>전체 행 검증은 카탈로그 교체 전에 완료한다.</summary>
    public static List<DebuffDefinition> Parse(IReadOnlyList<string[]> rows)
    {
        if (rows.Count < 2) throw new FormatException("Debuff sheet is empty.");
        string[] headers = rows[0];
        var definitions = new List<DebuffDefinition>();
        for (int i = 1; i < rows.Count; i++)
        {
            string[] row = rows[i];
            if (Array.TrueForAll(row, string.IsNullOrWhiteSpace)) continue;
            string Read(string name) => Value(headers, row, name);
            try
            {
                var kind = Named<DebuffKind>(Read("kind"));
                string lifetime = Read("lifetime"), policy = Read("reapply_policy");
                string expectedPolicy = kind == DebuffKind.Burn ? "Refresh" : kind == DebuffKind.ArmorBreak ? "AddStack" : "ReplaceAndRefresh";
                if (lifetime != (kind == DebuffKind.ArmorBreak ? "TargetLifetime" : "Timed") || policy != expectedPolicy)
                    throw new FormatException("Lifetime/reapply_policy does not match kind.");
                definitions.Add(new DebuffDefinition(int.Parse(Read("debuff_id"), CultureInfo.InvariantCulture),
                    Read("debuff_key"), kind, Read("stack_group"),
                    kind == DebuffKind.ArmorBreak ? 0 : (double)Number(Read("duration_sec")),
                    int.Parse(Read("max_stacks"), CultureInfo.InvariantCulture),
                    kind == DebuffKind.Burn ? (double)Number(Read("tick_interval_sec")) : 0,
                    kind == DebuffKind.Burn ? Number(Read("snapshot_hp_percent")) / 100 : 0,
                    kind == DebuffKind.Burn ? Number(Read("min_tick_damage")) : 0,
                    kind == DebuffKind.ArmorBreak ? (double)Number(Read("armor_strength")) : 0,
                    kind == DebuffKind.DamageTakenIncrease ? Number(Read("damage_taken_multiplier")) : 1));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException)
            { throw new FormatException($"Debuff row {i + 1}: {ex.Message}", ex); }
        }
        DebuffCatalog.Validate(definitions);
        return definitions;
    }
    /// <summary>새 컬럼 없는 기존 시트는 null(로컬 값 유지), 명시적 빈 FK는 빈 Binding(해제).</summary>
    public static DebuffBinding? ParseBinding(string[] headers, string[] row, bool unit, string location)
    {
        if (Column(headers, "debuff_id") < 0)
        {
            if (Column(headers, "debuff_trigger") >= 0 || Column(headers, "debuff_stacks_per_apply") >= 0)
                throw new FormatException($"{location}: missing debuff_id.");
            return null;
        }
        try
        {
            string id = Value(headers, row, "debuff_id");
            string trigger = Value(headers, row, "debuff_trigger");
            string stacks = Value(headers, row, "debuff_stacks_per_apply");
            if (id == "" || id == "0")
            {
                if (trigger != "" || stacks != "") throw new FormatException("Empty FK must have empty trigger/stacks.");
                return default(DebuffBinding);
            }
            var binding = new DebuffBinding(int.Parse(id, CultureInfo.InvariantCulture), Named<DebuffTrigger>(trigger), int.Parse(stacks, CultureInfo.InvariantCulture));
            if (unit ? binding.Trigger != DebuffTrigger.SkillActivated : binding.Trigger == DebuffTrigger.SkillActivated)
                throw new FormatException("Unsupported source/trigger combination.");
            if (binding.Trigger == DebuffTrigger.AffectedUnitBasicAttackAttempt && binding.StacksPerApply != 1)
                throw new FormatException("Armor attack trigger must add exactly one shared stack.");
            return binding;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException)
        { throw new FormatException($"{location}: {ex.Message}", ex); }
    }
}
