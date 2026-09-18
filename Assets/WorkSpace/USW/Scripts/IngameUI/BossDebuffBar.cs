using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows only effects currently active on the boss tracked by the HP HUD.</summary>
public sealed class BossDebuffBar : MonoBehaviour
{
    private sealed class Slot
    {
        internal GameObject Root;
        internal Image Icon;
        internal TMP_Text Label;
        internal TMP_Text Detail;
    }

    private readonly List<Slot> _slots = new List<Slot>();
    private BossManager _manager;
    private DebuffSettings _settings;
    private Vector2 _size;
    private TMP_FontAsset _font;

    /// <summary>Supplies scene dependencies and the HP bar's font and layout.</summary>
    public void Configure(BossManager manager, DebuffSettings settings, Vector2 size, TMP_FontAsset font)
    {
        _manager = manager;
        _settings = settings;
        _size = size;
        _font = font;
        Refresh(null);
    }

    private void LateUpdate()
    {
        var boss = _manager != null ? _manager.CurrentBoss : null;
        Refresh(boss != null && boss.isActiveAndEnabled && !boss.IsDead ? boss.Debuffs : null);
    }

    private void OnDisable() => Refresh(null);

    /// <summary>Renders a read-only snapshot; never advances or changes combat state.</summary>
    public void Refresh(DebuffController controller)
    {
        int count = controller?.Active.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            if (i == _slots.Count) _slots.Add(CreateSlot(i));
            var slot = _slots[i];
            var effect = controller.Active[i];
            var data = _settings != null ? _settings.FindPresentation(effect.Definition.Id) : null;
            slot.Root.SetActive(true);
            slot.Icon.sprite = data != null ? data.Icon : null;
            slot.Icon.enabled = slot.Icon.sprite != null;
            slot.Label.enabled = !slot.Icon.enabled;
            string label = data != null && !string.IsNullOrEmpty(data.DisplayName)
                ? data.DisplayName : effect.Definition.Kind.ToString();
            if (slot.Label.text != label) slot.Label.text = label;
            string remaining = double.IsPositiveInfinity(effect.ExpiresAt) ? "" :
                Math.Ceiling(Math.Max(0d, effect.ExpiresAt - controller.CurrentTime)).ToString("0") + "s";
            string detail = effect.Stacks > 1 ? "x" + effect.Stacks + (remaining.Length > 0 ? " " + remaining : "") : remaining;
            if (slot.Detail.text != detail) slot.Detail.text = detail;
        }
        for (int i = count; i < _slots.Count; i++)
            if (_slots[i].Root.activeSelf) _slots[i].Root.SetActive(false);
    }

    private Slot CreateSlot(int index)
    {
        var root = new GameObject("Debuff" + index, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = _size;
        rect.anchoredPosition = new Vector2(index * (_size.x + 6f), 0f);
        var background = root.GetComponent<Image>();
        background.color = new Color(0.12f, 0.08f, 0.16f, 0.9f);
        background.raycastTarget = false;
        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(root.transform, false);
        Stretch((RectTransform)iconObject.transform, new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.95f));
        var icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        return new Slot
        {
            Root = root, Icon = icon,
            Label = CreateText(root.transform, "Label", new Vector2(0f, 0.3f), Vector2.one),
            Detail = CreateText(root.transform, "DurationStacks", Vector2.zero, new Vector2(1f, 0.3f))
        };
    }

    private TMP_Text CreateText(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        Stretch((RectTransform)obj.transform, min, max);
        var text = obj.GetComponent<TextMeshProUGUI>();
        if (_font != null) text.font = _font;
        text.fontSize = 16f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 8f;
        text.fontSizeMax = 16f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
