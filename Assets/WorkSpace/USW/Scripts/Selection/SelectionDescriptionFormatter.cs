using System;
using System.Globalization;

/// <summary>확정 정의만 읽어 카드 설명을 만든다. 난수를 소비하거나 SO를 변경하지 않는다.</summary>
public sealed class SelectionDescriptionFormatter
{
    /// <summary>기존 저작 문구와 확정 랜덤/마나/해킹 토큰을 표시한다.</summary>
    public string Format(SelectionCardSnapshot card)
    {
        if (card == null) return string.Empty;
        return FormatTemplate(card.DescriptionTemplate, card.CopyDefinition());
    }

    /// <summary>Rejects unresolved or malformed placeholders before a card enters the catalog.</summary>
    public static void ValidateTemplate(string template, SelectionCardDefinition definition)
        => FormatTemplate(template, definition);

    private static string FormatTemplate(string template, SelectionCardDefinition definition)
    {
        string text = template ?? string.Empty;
        foreach (var effect in definition.Effects)
        {
            switch (effect)
            {
                case HackingProductionRollDefinition d:
                    text = text.Replace("{value}", Percent(d.MinimumRatio)).Replace("[1~10%]", Percent(d.MinimumRatio) + "%"); break;
                case OverflowManaDefinition d: text = text.Replace("{count}", d.ManaPerStack.ToString(CultureInfo.InvariantCulture)); break;
                case BombHackingDefinition d: text = text.Replace("{count}", d.StacksPerBomb.ToString(CultureInfo.InvariantCulture)); break;
                case HackingCarryoverDefinition d: text = text.Replace("{value}", Percent(d.Ratio)); break;
                case BonusManaDefinition d: text = text.Replace("{value}", Percent(d.Chance)).Replace("{count}", d.ManaCount.ToString(CultureInfo.InvariantCulture)); break;
            }
        }
        if (text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0)
            throw new ArgumentException("Selection description contains an unresolved or malformed token: " + text);
        return text;
    }
    private static string Percent(float ratio) => (ratio * 100f).ToString("0.#", CultureInfo.InvariantCulture);
}
