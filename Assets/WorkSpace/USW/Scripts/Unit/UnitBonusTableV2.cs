using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using LitJson;

/// <summary>Bonus/Effect relational chart -> existing typed effect parser. Legacy charts remain readable.</summary>
public static class UnitBonusTableV2
{
    private static readonly Regex Id = new("^[A-Za-z][A-Za-z0-9_]*$");
    private static readonly Regex Token = new(@"\{([^{}]+)\}");
    private static readonly Regex Reference = new(@"^([A-Za-z][A-Za-z0-9_]*)\.(Value|Every|DamageBonusPercent|ChargeReductionPercent)$");
    private static string Text(JsonData row, string key) => row.ContainsKey(key) && row[key] != null ? row[key].ToString() : "";
    public static string LegacyId(string key, int level) => key.Replace("UnitData_", "").Replace("Chief", "").ToLowerInvariant() + "_lv" + level;

    public static JsonData Expand(JsonData rows)
    {
        if (rows == null || !rows.IsArray) return rows;
        bool relational = false;
        foreach (JsonData row in rows) if (Text(row, "RecordType") is "Bonus" or "Effect") relational = true;
        if (!relational) return rows;
        var bonuses = new Dictionary<string, JsonData>(StringComparer.Ordinal);
        var effects = new Dictionary<string, SortedDictionary<string, JsonData>>(StringComparer.Ordinal);
        var slots = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonData row in rows)
        {
            string type = Text(row, "RecordType"), id = Text(row, "BonusId");
            if (!Id.IsMatch(id)) throw new FormatException("Invalid BonusId: " + id);
            if (type == "Bonus")
            {
                if (bonuses.ContainsKey(id)) throw new FormatException("Duplicate BonusId: " + id);
                string key = Text(row, "UnitKey"), level = Text(row, "Level");
                if (string.IsNullOrWhiteSpace(key) || !int.TryParse(level, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed < 1 || parsed > 1000
                    || string.IsNullOrWhiteSpace(Text(row, "Name")) || !slots.Add(key + "/" + parsed)) throw new FormatException("Bonus owner/level/name");
                bonuses.Add(id, row);
            }
            else if (type == "Effect")
            {
                string effectKey = Text(row, "EffectKey");
                if (!Id.IsMatch(effectKey)) throw new FormatException("Invalid EffectKey");
                if (!effects.TryGetValue(id, out var group)) effects.Add(id, group = new SortedDictionary<string, JsonData>(StringComparer.Ordinal));
                if (group.ContainsKey(effectKey)) throw new FormatException("Duplicate effect: " + id + "/" + effectKey);
                group.Add(effectKey, row);
            }
            else throw new FormatException("Mixed legacy and relational bonus rows");
        }
        foreach (var pair in effects) if (!bonuses.ContainsKey(pair.Key)) throw new FormatException("Missing bonus: " + pair.Key);
        var result = new JsonData(); result.SetJsonType(JsonType.Array);
        foreach (var bonus in bonuses)
        {
            if (!effects.TryGetValue(bonus.Key, out var group) || group.Count == 0) throw new FormatException("Bonus has no effects: " + bonus.Key);
            string description = Render(bonus.Value, group);
            int index = 0;
            var parameters = new HashSet<string>(StringComparer.Ordinal);
            foreach (var effect in group)
            {
                string parameter = Text(effect.Value, "EffectType") == "StatModifierBonus" ? Text(effect.Value, "Stat") + "/" + Text(effect.Value, "ValueMode") : Text(effect.Value, "CycleId") + "/" + Text(effect.Value, "Every");
                if (!parameters.Add(parameter)) throw new FormatException("Duplicate bonus parameter: " + bonus.Key + "/" + parameter);
                var row = JsonMapper.ToObject(effect.Value.ToJson());
                row["RecordType"] = "Part"; row["UnitKey"] = Text(bonus.Value, "UnitKey"); row["Level"] = Text(bonus.Value, "Level"); row["PartIndex"] = index++;
                row["Description"] = description; result.Add(row);
            }
            var presentation = JsonMapper.ToObject(bonus.Value.ToJson());
            presentation["RecordType"] = "Description"; presentation["Description"] = description; result.Add(presentation);
        }
        return result;
    }

    private static string Render(JsonData bonus, SortedDictionary<string, JsonData> effects)
    {
        string template = Text(bonus, "DescriptionTemplate");
        if (string.IsNullOrWhiteSpace(template) || template.Contains("<") || template.Contains(">")) throw new FormatException("Bonus description template");
        string text = Token.Replace(template, match =>
        {
            string key = match.Groups[1].Value;
            if (key == "공격속도효과")
            {
                if (Text(bonus, "EffectLink") != "ChiefAttackSpeed") throw new FormatException("Effect link required");
                return match.Value;
            }
            var reference = Reference.Match(key);
            if (!reference.Success || !effects.TryGetValue(reference.Groups[1].Value, out var effect)) throw new FormatException("Unknown description reference: " + key);
            string field = reference.Groups[2].Value, type = Text(effect, "EffectType");
            if (!(type == "StatModifierBonus" && field == "Value" || type == "EveryNthCycleBonus" && field != "Value")) throw new FormatException("Unsupported description field: " + key);
            if (!decimal.TryParse(Text(effect, field), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number < 0) throw new FormatException("Description number: " + key);
            return number.ToString("0.######", CultureInfo.InvariantCulture);
        });
        string remaining = text.Replace("{공격속도효과}", "");
        if (remaining.Contains("{") || remaining.Contains("}")) throw new FormatException("Unbalanced description reference");
        return text.Replace("%p", "%");
    }
}
