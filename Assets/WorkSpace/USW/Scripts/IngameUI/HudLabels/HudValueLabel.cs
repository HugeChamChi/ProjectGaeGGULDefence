using TMPro;
using UnityEngine;

/// <summary>
/// Small caption that names a top HUD value ("남은 시간", "체력"). Wording, icons and the
/// low-time warning come from <see cref="HudLabelSettings"/>. The timer label's single
/// entry point is <see cref="SetRemaining"/>, called wherever the timer number is refreshed.
/// </summary>
public sealed class HudValueLabel : MonoBehaviour
{
    /// <summary>Which HUD value this label names.</summary>
    public enum ValueKind { Timer, Health }

    [SerializeField] private HudLabelSettings _settings;
    [SerializeField] private ValueKind _kind;
    [SerializeField] private TMP_Text _text;
    /// <summary>Child of <see cref="_text"/> so it follows the text box.</summary>
    [SerializeField] private HudGlyph _icon;
    /// <summary>Timer only: low-time warning shown on the other side of the number.</summary>
    [SerializeField] private TMP_Text _warning;
    /// <summary>Text hugs the right edge (timer, left of the number) instead of the left (health).</summary>
    [SerializeField] private bool _alignRight;
    [SerializeField, Min(0f)] private float _iconGap = 6f;

    private bool _low;
    private bool _waiting;

    /// <summary>Settings in use; assigning re-applies wording and style immediately.</summary>
    public HudLabelSettings Settings
    {
        get => _settings;
        set { _settings = value; Apply(); }
    }

    /// <summary>Whether the low-time warning is currently shown.</summary>
    public bool IsWarning => _low;

    /// <summary>Label text currently shown, for checks.</summary>
    public string Text => _text != null ? _text.text : string.Empty;

    /// <summary>Warning text, empty while hidden.</summary>
    public string WarningText => _low && _warning != null ? _warning.text : string.Empty;

    private void Awake() => Apply();

    private void OnDisable() => ResetPulse();

    /// <summary>
    /// Timer entry point. Pass the same values the timer number receives;
    /// <paramref name="isWaitTime"/> is the boss arrival delay, which never warns.
    /// </summary>
    public void SetRemaining(float remaining, bool isWaitTime = false)
    {
        if (_kind != ValueKind.Timer || _settings == null) return;
        bool low = !isWaitTime && _settings.WarnWhenLow && _warning != null && remaining <= _settings.LowTimeThresholdSeconds;
        if (low == _low && isWaitTime == _waiting) return;
        _low = low;
        _waiting = isWaitTime;
        Apply();
    }

    /// <summary>Re-reads wording, colours and icon visibility from the settings.</summary>
    public void Apply()
    {
        if (_settings == null || _text == null) return;
        if (!_settings.WarnWhenLow) _low = false;
        _text.text = _kind == ValueKind.Health ? _settings.HealthLabel
            : _waiting ? _settings.WaitTimerLabel
            : _settings.TimerLabel;
        _text.color = _low ? _settings.LowTimeColor : _settings.LabelColor;
        _text.alignment = _alignRight ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        if (_warning != null)
        {
            _warning.gameObject.SetActive(_low);
            _warning.text = _settings.LowTimeLabel;
            _warning.color = _settings.LowTimeColor;
        }
        if (_icon != null)
        {
            _icon.gameObject.SetActive(_settings.ShowIcons);
            _icon.color = _low ? _settings.LowTimeColor
                : _kind == ValueKind.Health ? _settings.HealthIconColor : _settings.TimerIconColor;
            _icon.OutlineColor = _settings.IconOutlineColor;
        }
        LayoutIcon();
        if (!_low) ResetPulse();
    }

    private void LayoutIcon()
    {
        bool show = _icon != null && _settings.ShowIcons;
        float iconWidth = show ? _icon.rectTransform.rect.width + _iconGap : 0f;
        // Left-aligned text makes room with a margin; right-aligned text leaves the icon ahead of its first glyph.
        _text.margin = new Vector4(_alignRight ? 0f : iconWidth, 0f, 0f, 0f);
        if (!show) return;
        _text.ForceMeshUpdate();
        var iconRt = _icon.rectTransform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(_alignRight ? 1f : 0f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        float half = iconRt.rect.width * 0.5f;
        float textWidth = Mathf.Min(_text.textBounds.size.x, _text.rectTransform.rect.width);
        iconRt.anchoredPosition = new Vector2(_alignRight ? -(textWidth + _iconGap + half) : half, iconRt.anchoredPosition.y);
    }

    private void Update()
    {
        if (!_low || _warning == null) return;
        float phase = Mathf.Repeat(Time.unscaledTime / _settings.LowTimePulsePeriod, 1f);
        float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
        _warning.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, _settings.LowTimePulseScale, wave);
    }

    private void ResetPulse()
    {
        if (_warning != null) _warning.rectTransform.localScale = Vector3.one;
    }
}
