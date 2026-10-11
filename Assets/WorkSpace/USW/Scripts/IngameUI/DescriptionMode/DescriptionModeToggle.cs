using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Binds the shared Unity description switch to the session preference.</summary>
public sealed class DescriptionModeToggle : MonoBehaviour
{
    private Toggle _toggle;
    private DescriptionSwitchVisual _visual;
    private DescriptionModeState _state;

    /// <summary>The selected presentation mode.</summary>
    public DescriptionMode Current => _state?.Current ?? DescriptionMode.Detailed;

    /// <summary>Creates the same thumb switch used by selection cards.</summary>
    public static DescriptionModeToggle Create(Transform parent, TMP_Text fontSource, Sprite background, DescriptionModeState state)
    {
        DescriptionModeToggle control = null;
        var toggle = DescriptionToggleView.Create(parent, fontSource,
            detailed => control?._state?.Set(detailed ? DescriptionMode.Detailed : DescriptionMode.Brief));
        toggle.gameObject.name = "DescriptionModeToggle";
        control = toggle.gameObject.AddComponent<DescriptionModeToggle>();
        control._toggle = toggle;
        control._visual = toggle.GetComponent<DescriptionSwitchVisual>();
        var element = toggle.gameObject.AddComponent<LayoutElement>();
        element.minWidth = element.preferredWidth = 340f;
        element.minHeight = element.preferredHeight = 64f;
        control.Bind(state);
        return control;
    }

    /// <summary>Rebinds safely without retaining a previous owner's subscription.</summary>
    public void Bind(DescriptionModeState state)
    {
        if (_state != null) _state.OnChanged -= Refresh;
        _state = state;
        if (_state != null && isActiveAndEnabled) _state.OnChanged += Refresh;
        Refresh(Current);
    }

    private void Refresh(DescriptionMode mode)
    {
        if (_toggle == null) return;
        _toggle.SetIsOnWithoutNotify(mode == DescriptionMode.Detailed);
        _toggle.interactable = _state != null;
        _visual.Refresh();
    }

    private void OnEnable()
    {
        if (_state != null) { _state.OnChanged -= Refresh; _state.OnChanged += Refresh; }
        Refresh(Current);
    }

    private void OnDisable() { if (_state != null) _state.OnChanged -= Refresh; }
    private void OnDestroy() { if (_state != null) _state.OnChanged -= Refresh; }
}
