using System;
using System.Globalization;
using System.Text;

/// <summary>Resolves SO wording using the same immutable definitions as combat.</summary>
public sealed class DebuffInfoPresenter
{
    private readonly DebuffSettings _settings;
    private readonly DebuffCatalog _catalog;
    /// <summary>Uses root-owned data; never loads or replaces gameplay configuration.</summary>
    public DebuffInfoPresenter(DebuffSettings settings, DebuffCatalog catalog)
    { _settings = settings; _catalog = catalog; }

    /// <summary>Missing presentation data stays plain text instead of opening an empty window.</summary>
    public bool TryBuild(int id, DebuffBinding binding, out DebuffInfoModel model)
    {
        model = null;
        var data = _settings.FindPresentation(id);
        if (data == null || string.IsNullOrWhiteSpace(data.Description) || !_catalog.TryGet(id, out var definition)) return false;
        string body = Format(data.Description, definition) + "\n\n" + Format(data.Rules, definition);
        if (binding.IsConfigured && binding.DebuffId == id)
        {
            string trigger = binding.Trigger switch
            {
                DebuffTrigger.SkillActivated => "스킬 발동 시",
                DebuffTrigger.ProjectileHit => "투사체 적중 시",
                DebuffTrigger.AffectedUnitBasicAttackAttempt => "효과 범위 안 유닛의 일반 공격 시도 시",
                _ => string.Empty
            };
            if (trigger.Length > 0) body += $"\n\n이 효과의 부여 조건\n{trigger} {binding.StacksPerApply}중첩 부여";
        }
        model = new DebuffInfoModel(data.DisplayName, body.Trim());
        return true;
    }

    /// <summary>Only decorates known terms from the source's binding, preserving all existing range colors.</summary>
    public string AddLinks(string text, DebuffBinding binding)
    {
        if (string.IsNullOrEmpty(text) || !binding.IsConfigured || !TryBuild(binding.DebuffId, binding, out _)) return text;
        var data = _settings.FindPresentation(binding.DebuffId);
        var output = new StringBuilder(text.Length + 64);
        bool insideLink = false;
        for (int i = 0; i < text.Length;)
        {
            if (text[i] == '<')
            {
                int end = text.IndexOf('>', i);
                if (end < 0) { output.Append(text, i, text.Length - i); break; }
                string tag = text.Substring(i, end - i + 1);
                if (tag.StartsWith("<link", StringComparison.OrdinalIgnoreCase)) insideLink = true;
                else if (tag.StartsWith("</link", StringComparison.OrdinalIgnoreCase)) insideLink = false;
                output.Append(tag); i = end + 1; continue;
            }
            string match = null;
            if (!insideLink)
            {
                Match(data.DisplayName);
                foreach (string alias in data.TermAliases) Match(alias);
            }
            if (match == null) { output.Append(text[i++]); continue; }
            output.Append("<link=\"debuff:").Append(binding.DebuffId).Append("\"><u>").Append(match).Append("</u></link>");
            i += match.Length;

            void Match(string term)
            {
                if (!string.IsNullOrEmpty(term) && (match == null || term.Length > match.Length) &&
                    i + term.Length <= text.Length && string.CompareOrdinal(text, i, term, 0, term.Length) == 0) match = term;
            }
        }
        return output.ToString();
    }

    private static string Format(string text, DebuffDefinition definition)
    {
        string Number(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
        return (text ?? string.Empty)
            .Replace("{MaxStacks}", definition.MaxStacks.ToString(CultureInfo.InvariantCulture))
            .Replace("{Duration}", Number((decimal)definition.Duration))
            .Replace("{TickInterval}", Number((decimal)definition.TickInterval))
            .Replace("{SnapshotPercent}", Number(definition.SnapshotRatio * 100))
            .Replace("{MinTickDamage}", Number((decimal)definition.MinTickUnits / CombatHealth.Scale))
            .Replace("{DamageIncreasePercent}", Number((definition.DamageMultiplier - 1) * 100));
    }
}
