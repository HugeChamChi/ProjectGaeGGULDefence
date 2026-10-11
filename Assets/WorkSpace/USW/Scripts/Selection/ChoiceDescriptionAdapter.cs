using System.Collections.Generic;
using System.Globalization;

/// <summary>Captures actual effect values once at presentation, including the existing run-cached random roll.</summary>
public sealed class ChoiceDescriptionAdapter : IDescriptionSource
{
    private readonly LevelUpData _card;
    private readonly LevelUpManager _manager;
    public ChoiceDescriptionAdapter(LevelUpData card, LevelUpManager manager) { _card = card; _manager = manager; }
    public DescriptionSnapshot Capture()
    {
        if (_card == null) return new DescriptionSnapshot(null, null);
        string Number(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);
        var values = new Dictionary<string, string>
        {
            ["primaryValue"] = Number(_card.primaryValue),
            ["secondaryValue"] = Number(_card.secondaryValue),
            ["specialValue"] = Number(_card.specialValue)
        };
        string detailed = _card.description;
        var e = _card.droneEffect;
        if (e != null)
        {
            values["Interval"] = Number(e.Interval);
            values["Value"] = Number(e.Value);
            values["ValuePercent"] = Number(e.Value * 100f);
            values["MaxValuePercent"] = Number(e.MaxValue * 100f);
            values["Count"] = e.Count.ToString(CultureInfo.InvariantCulture);
            if (e.Kind == DroneSelectionKind.DeltanDamageTaken)
            {
                values["value"] = UnityEngine.Mathf.RoundToInt(_manager.DroneSelections.PreviewValue(_card) * 100f).ToString(CultureInfo.InvariantCulture);
                detailed = (detailed ?? string.Empty).Replace("[1~10%]", "{value}%");
            }
        }
        return new DescriptionSnapshot(detailed, _card.simpleDescription, values);
    }
}
