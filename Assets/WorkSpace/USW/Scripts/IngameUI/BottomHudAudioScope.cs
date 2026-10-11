using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicitly limits the HUD click sound to the authored bottom controls.</summary>
[DisallowMultipleComponent]
public sealed class BottomHudAudioScope : MonoBehaviour
{
    [SerializeField] private Button[] _buttons = Array.Empty<Button>();

    /// <summary>Popups and menu contents stay silent unless this exact button is authored here.</summary>
    public bool Contains(Selectable control) => control is Button button && Array.IndexOf(_buttons, button) >= 0;
}
