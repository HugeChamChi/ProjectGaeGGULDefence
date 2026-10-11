using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reusable two-option control. Its owner supplies the shared session preference.</summary>
public sealed class DescriptionModeToggle : MonoBehaviour
{
    private static readonly Color Selected = new Color(1f, 0.76f, 0.30f);
    private static readonly Color Unselected = new Color(0.16f, 0.17f, 0.23f);
    private readonly Button[] _buttons = new Button[2];
    private readonly TMP_Text[] _labels = new TMP_Text[2];
    private DescriptionModeState _state;

    /// <summary>The selected presentation mode.</summary>
    public DescriptionMode Current => _state?.Current ?? DescriptionMode.Brief;

    /// <summary>Creates a control using the caller's font and optional sliced background.</summary>
    public static DescriptionModeToggle Create(Transform parent, TMP_Text fontSource, Sprite background, DescriptionModeState state)
    {
        var go = new GameObject("DescriptionModeToggle", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(340f, 104f);
        var element = go.AddComponent<LayoutElement>();
        element.minWidth = element.preferredWidth = 340f;
        element.minHeight = element.preferredHeight = 104f;
        var control = go.AddComponent<DescriptionModeToggle>();
        for (int i = 0; i < 2; i++)
        {
            var option = new GameObject(i == 0 ? "Brief" : "Detailed", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)option.transform;
            rt.SetParent(rect, false);
            rt.anchorMin = new Vector2(i * 0.5f, 0f);
            rt.anchorMax = new Vector2((i + 1) * 0.5f, 1f);
            rt.offsetMin = new Vector2(3f, 3f);
            rt.offsetMax = new Vector2(-3f, -3f);
            var graphic = option.GetComponent<Image>();
            graphic.sprite = background;
            if (background != null) graphic.type = Image.Type.Sliced;
            var button = option.GetComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.None;
            int index = i;
            button.onClick.AddListener(() => control._state?.Set((DescriptionMode)index));
            control._buttons[i] = button;
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(rt, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            if (fontSource != null) { label.font = fontSource.font; label.fontSharedMaterial = fontSource.fontSharedMaterial; }
            label.fontSize = 32f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = i == 0 ? "간단히" : "자세히";
            label.raycastTarget = false;
            control._labels[i] = label;
        }
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
        for (int i = 0; i < _buttons.Length; i++)
        {
            if (_buttons[i] == null) continue;
            bool selected = i == (int)mode;
            _buttons[i].targetGraphic.color = selected ? Selected : Unselected;
            _buttons[i].interactable = _state != null;
            _labels[i].color = Color.white;
            _labels[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private void OnEnable()
    {
        if (_state != null) { _state.OnChanged -= Refresh; _state.OnChanged += Refresh; }
        Refresh(Current);
    }

    private void OnDisable() { if (_state != null) _state.OnChanged -= Refresh; }
    private void OnDestroy() { if (_state != null) _state.OnChanged -= Refresh; }
}
