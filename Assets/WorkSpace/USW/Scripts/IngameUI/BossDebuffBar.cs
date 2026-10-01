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
        internal TMP_Text Stacks;
        internal int Id = -1, Count = -1;
        internal double Seconds = double.NaN;
    }

    [Tooltip("디버프 슬롯 사이 가로 간격")]
    [SerializeField] private float _spacing = 6f;
    [SerializeField] private float _labelFontSize = 20f;
    [SerializeField] private float _stackFontSize = 22f;

    private readonly List<Slot> _slots = new List<Slot>();
    private readonly List<DebuffInstance> _ordered = new List<DebuffInstance>();
    private Comparison<DebuffInstance> _compare;
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
        _compare = CompareEffects;
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
        _ordered.Clear();
        if (controller != null) foreach (var effect in controller.Active) _ordered.Add(effect);
        if (_compare != null) _ordered.Sort(_compare);
        for (int i = 0; i < count; i++)
        {
            if (i == _slots.Count) _slots.Add(CreateSlot(i));
            var slot = _slots[i];
            var effect = _ordered[i];
            var data = _settings != null ? _settings.FindPresentation(effect.Definition.Id) : null;
            if (!slot.Root.activeSelf) slot.Root.SetActive(true);
            if (slot.Id != effect.Definition.Id)
            {
                slot.Id = effect.Definition.Id;
                slot.Count = -1; slot.Seconds = double.NaN;
                slot.Icon.sprite = data != null ? data.Icon : null;
                slot.Icon.enabled = slot.Icon.sprite != null;
                slot.Label.enabled = !slot.Icon.enabled;
            }
            string label = data != null && !string.IsNullOrEmpty(data.DisplayName)
                ? data.ShortName : effect.Definition.Kind.ToString();
            if (slot.Label.text != label) slot.Label.text = label;
            double seconds = double.IsPositiveInfinity(effect.ExpiresAt) ? double.PositiveInfinity :
                Math.Ceiling(Math.Max(0d, effect.ExpiresAt - controller.CurrentTime));
            if (slot.Seconds != seconds)
            {
                slot.Seconds = seconds;
                slot.Detail.text = double.IsPositiveInfinity(seconds) ? string.Empty : seconds.ToString("0") + "s";
            }
            int stacks = effect.Definition.Kind == DebuffKind.ArmorBreak || effect.Stacks > 1 ? effect.Stacks : 0;
            if (slot.Count != stacks)
            {
                slot.Count = stacks;
                slot.Stacks.transform.parent.gameObject.SetActive(stacks > 0);
                slot.Stacks.text = stacks > 0 ? stacks.ToString() : string.Empty;
            }
        }
        for (int i = count; i < _slots.Count; i++)
            if (_slots[i].Root.activeSelf) _slots[i].Root.SetActive(false);
    }

    private int CompareEffects(DebuffInstance a, DebuffInstance b)
    {
        int order = (_settings?.FindPresentation(a.Definition.Id)?.DisplayOrder ?? a.Definition.Id)
            .CompareTo(_settings?.FindPresentation(b.Definition.Id)?.DisplayOrder ?? b.Definition.Id);
        return order != 0 ? order : a.Definition.Id.CompareTo(b.Definition.Id);
    }

    private Slot CreateSlot(int index)
    {
        var root = new GameObject("Debuff" + index, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(transform, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = _size;
        rect.anchoredPosition = new Vector2(index * (_size.x + _spacing), 0f);
        var background = root.GetComponent<Image>();
        background.color = new Color(0.12f, 0.08f, 0.16f, 0.9f);
        background.raycastTarget = false;
        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(root.transform, false);
        Stretch((RectTransform)iconObject.transform, new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.95f));
        var icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var badge = new GameObject("StackBadge", typeof(RectTransform), typeof(Image));
        badge.transform.SetParent(root.transform, false);
        Stretch((RectTransform)badge.transform, new Vector2(0.54f, 0.27f), new Vector2(1f, 0.65f));
        badge.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.12f, 0.95f);
        badge.GetComponent<Image>().raycastTarget = false;
        var slot = new Slot
        {
            Root = root, Icon = icon,
            Label = CreateText(root.transform, "Label", new Vector2(0f, 0.3f), Vector2.one),
            Detail = CreateText(root.transform, "Duration", Vector2.zero, new Vector2(1f, 0.27f)),
            Stacks = CreateText(badge.transform, "Stacks", Vector2.zero, Vector2.one)
        };
        badge.transform.SetAsLastSibling();
        slot.Stacks.fontSize = _stackFontSize;
        slot.Stacks.fontStyle = FontStyles.Bold;
        return slot;
    }

    private TMP_Text CreateText(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        Stretch((RectTransform)obj.transform, min, max);
        var text = obj.GetComponent<TextMeshProUGUI>();
        if (_font != null) text.font = _font;
        text.fontSize = _labelFontSize;
        text.enableAutoSizing = false;
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
