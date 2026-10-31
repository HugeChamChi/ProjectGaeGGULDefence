using UnityEngine;

/// <summary>
/// Player-facing wording and style for the small labels that name the top HUD values
/// (remaining time and boss health). All label text lives here, not in code.
/// </summary>
[CreateAssetMenu(fileName = "HudLabelSettings", menuName = "USW/UI/Hud Label Settings")]
public sealed class HudLabelSettings : ScriptableObject
{
    [Header("Wording")]
    /// <summary>Label shown next to the combat countdown.</summary>
    public string TimerLabel = "남은 시간";
    /// <summary>Label shown while the countdown shows the boss arrival delay instead.</summary>
    public string WaitTimerLabel = "보스 등장";
    /// <summary>Label shown above the boss health bar.</summary>
    public string HealthLabel = "체력";

    [Header("Icons")]
    /// <summary>Draw an hourglass / heart glyph before each label.</summary>
    public bool ShowIcons;
    public Color TimerIconColor = new Color(0.98f, 0.80f, 0.42f, 1f);
    public Color HealthIconColor = new Color(0.95f, 0.33f, 0.40f, 1f);
    public Color IconOutlineColor = new Color(0.10f, 0.08f, 0.14f, 1f);

    [Header("Low time warning")]
    /// <summary>Show a defeat warning beside the number and tint the timer label when little time remains.</summary>
    public bool WarnWhenLow;
    /// <summary>Remaining seconds at or below which the warning shows.</summary>
    [Min(0f)] public float LowTimeThresholdSeconds = 10f;
    public string LowTimeLabel = "0초가 되면 패배";
    public Color LowTimeColor = new Color(1f, 0.40f, 0.32f, 1f);
    /// <summary>Peak scale of the warning pulse (1 = no pulse).</summary>
    [Min(1f)] public float LowTimePulseScale = 1.1f;
    /// <summary>Seconds per warning pulse, on unscaled time.</summary>
    [Min(0.05f)] public float LowTimePulsePeriod = 0.6f;

    [Header("Style")]
    public Color LabelColor = new Color(1f, 0.965f, 0.90f, 1f);
}
